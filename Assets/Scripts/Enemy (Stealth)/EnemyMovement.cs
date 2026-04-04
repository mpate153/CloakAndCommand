using UnityEngine;

/// <summary>
/// Handles all Rigidbody2D movement for the enemy.
/// EnemyAI calls the public methods each FixedUpdate depending on current state.
/// No state logic lives here — this is purely "how to move."
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Chase")]
    [SerializeField] public float chaseSpeed = 3.5f;
    [SerializeField] public float chaseStopDistance = 0.15f;
    [SerializeField] public float chaseTurnSpeed = 360f;

    [Header("Suspicious")]
    [Tooltip("Turn speed while suspicious — slow, giving the player time to react.")]
    [SerializeField] public float suspiciousTurnSpeed = 95f;

    [Header("Search")]
    [Tooltip("Fraction of chaseSpeed used while walking to the last-known position.")]
    [SerializeField] public float searchSpeedMultiplier = 0.6f;
    [Tooltip("Distance at which the guard is considered 'arrived' at last-known position.")]
    [SerializeField] public float searchArrivalDistance = 0.35f;
    [Tooltip("Degrees per second the guard sweeps when scanning on the spot.")]
    [SerializeField] public float scanSpeed = 55f;
    [Tooltip("Half-angle swept in each direction during on-spot scanning.")]
    [SerializeField] public float scanHalfAngle = 50f;

    [Header("Idle look-around")]
    [Tooltip("Half-angle swept in each direction while idling in place.")]
    [SerializeField] private float idleLookHalfAngle = 10f;
    [Tooltip("Degrees/sec for idle look sweep (use lower values for slower, human-like scanning).")]
    [SerializeField] private float idleLookSweepSpeed = 18f;
    [Tooltip("Seconds to hold gaze at each side before turning back.")]
    [SerializeField] private float idleLookPauseAtEnds = 0.45f;

    [Header("Local avoidance")]
    [Tooltip("Helps prevent kinematic enemies from stacking on each other while moving.")]
    [SerializeField] private bool enableSeparation = true;
    [SerializeField] private float separationRadius = 0.7f;
    [SerializeField] private float separationWeight = 1.35f;
    [SerializeField] private LayerMask separationMask = ~0;

    /// <summary>
    /// While chasing, we do not shove the enemy away from the player after <see cref="MovePosition"/> — that was preventing melee range.
    /// Set by <see cref="EnemyAI"/> from <see cref="FixedUpdate"/>.
    /// </summary>
    public bool SuppressDepenetrationFromPlayerDuringChase { get; set; }

    // ── Private ──────────────────────────────────────────────────────────────
    private Rigidbody2D _body;
    private Collider2D _selfCollider;
    private Collider2D[] _enemyColliders;
    private bool _playerCollisionIgnoreActive;
    private Transform _ignoredPlayerBodyRoot;
    private Collider2D[] _ignoredPlayerColliders;
    private readonly Collider2D[] _separationHits = new Collider2D[24];

    private Vector2 _sprinterStaleChaseTarget;

    // Scan state — reset by EnemyAI whenever search begins
    private bool _arrivedAtLastKnown;
    private float _scanBaseRotation;
    private float _scanOffset;
    private float _scanVelocity;
    private bool _idleLookInitialized;
    private float _idleLookBaseRotation;
    private float _idleLookOffset;
    private float _idleLookVelocity;
    private float _idleLookPauseTimer;

    // ────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType = RigidbodyType2D.Kinematic;
        _selfCollider = GetComponent<Collider2D>();
        _enemyColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void OnDisable() => ClearPlayerCollisionIgnore();

    /// <summary>
    /// Kinematic <see cref="MovePosition"/> still resolves against a dynamic player <see cref="Rigidbody2D"/> and shoves them.
    /// While chasing, ignore enemy vs player collider pairs so melee range is stable. Overlap queries still see the player.
    /// </summary>
    public void SetIgnorePlayerCollisionsWhileChasing(bool chasing, Transform playerVisionTransform)
    {
        if (!chasing)
        {
            ClearPlayerCollisionIgnore();
            return;
        }

        if (playerVisionTransform == null)
        {
            ClearPlayerCollisionIgnore();
            return;
        }

        PlayerMovement pm = playerVisionTransform.GetComponentInParent<PlayerMovement>(true);
        if (pm == null)
        {
            ClearPlayerCollisionIgnore();
            return;
        }

        Transform bodyRoot = pm.transform;
        if (_playerCollisionIgnoreActive && _ignoredPlayerBodyRoot == bodyRoot)
            return;

        if (_playerCollisionIgnoreActive)
            ClearPlayerCollisionIgnore();

        _ignoredPlayerBodyRoot = bodyRoot;
        _ignoredPlayerColliders = bodyRoot.GetComponentsInChildren<Collider2D>(true);
        ApplyPlayerCollisionIgnore(true);
    }

    void ApplyPlayerCollisionIgnore(bool ignore)
    {
        if (_enemyColliders == null || _ignoredPlayerColliders == null)
            return;

        for (int e = 0; e < _enemyColliders.Length; e++)
        {
            Collider2D ec = _enemyColliders[e];
            if (ec == null) continue;
            for (int p = 0; p < _ignoredPlayerColliders.Length; p++)
            {
                Collider2D pc = _ignoredPlayerColliders[p];
                if (pc == null) continue;
                Physics2D.IgnoreCollision(ec, pc, ignore);
            }
        }

        _playerCollisionIgnoreActive = ignore;
    }

    void ClearPlayerCollisionIgnore()
    {
        if (!_playerCollisionIgnoreActive)
            return;
        ApplyPlayerCollisionIgnore(false);
        _ignoredPlayerBodyRoot = null;
        _ignoredPlayerColliders = null;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Public movement methods — called by EnemyAI from FixedUpdate
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Rotate smoothly toward the player at suspicious turn speed.</summary>
    public void RotateSuspicious(Vector2 targetPosition)
        => RotateTowards(targetPosition, suspiciousTurnSpeed);

    /// <summary>Rotate and move toward the player at full chase speed.</summary>
    public void ChasePlayer(Vector2 targetPosition)
    {
        Vector2 to = targetPosition - _body.position;
        float dist = to.magnitude;
        if (dist <= chaseStopDistance) return;
        Vector2 dir = to / dist;
        dir = ApplySeparation(dir);
        if (dir.sqrMagnitude < 1e-8f) return;
        dir.Normalize();
        RotateTowardsDirection(dir, chaseTurnSpeed);
        float step = Mathf.Min(chaseSpeed * Time.fixedDeltaTime, dist - chaseStopDistance);
        if (step <= 0f) return;
        _body.MovePosition(_body.position + dir * step);
        if (!SuppressDepenetrationFromPlayerDuringChase)
            TryDepenetrateFromPlayerIfOverlapping();
    }

    /// <summary>Planar move + turn (deploy / leave paths).</summary>
    public void WalkTowards(Vector2 worldTarget, float moveSpeed, float turnSpeedDegreesPerSec)
    {
        Vector2 to = worldTarget - _body.position;
        float dist = to.magnitude;
        if (dist < 0.0001f) return;
        Vector2 dir = to / dist;
        dir = ApplySeparation(dir);
        RotateTowardsDirection(dir, turnSpeedDegreesPerSec);
        _body.MovePosition(_body.position + dir * (moveSpeed * Time.fixedDeltaTime));
    }

    /// <summary>Call when entering chase so stale target starts at the player.</summary>
    public void ResetSprinterStaleChase(Vector2 worldPosition) => _sprinterStaleChaseTarget = worldPosition;

    /// <summary>
    /// Chase toward a target that lags behind the real position — overshoots corners, easier to juke.
    /// </summary>
    public void ChasePlayerWithStaleTarget(Vector2 trueTarget, float maxApproachUnitsPerSecond)
    {
        float step = Mathf.Max(0.01f, maxApproachUnitsPerSecond) * Time.fixedDeltaTime;
        _sprinterStaleChaseTarget = Vector2.MoveTowards(_sprinterStaleChaseTarget, trueTarget, step);
        ChasePlayer(_sprinterStaleChaseTarget);
    }

    /// <summary>
    /// Call this once when entering the Search state to reset internal scan data.
    /// </summary>
    public void BeginSearch()
    {
        _arrivedAtLastKnown = false;
        _scanOffset = 0f;
        _scanVelocity = scanSpeed;
    }

    /// <summary>
    /// Walk to lastKnownPos, then sweep the cone left/right.
    /// Returns true once the guard has arrived and is actively scanning.
    /// </summary>
    public bool SearchMove(Vector2 lastKnownPos)
    {
        if (!_arrivedAtLastKnown)
        {
            Vector2 toTarget = lastKnownPos - _body.position;
            float dist = toTarget.magnitude;

            if (dist <= searchArrivalDistance)
            {
                _arrivedAtLastKnown = true;
                _scanBaseRotation = _body.rotation;
                _scanOffset = 0f;
                _scanVelocity = scanSpeed;
            }
            else
            {
                Vector2 dir = toTarget / dist;
                dir = ApplySeparation(dir);
                if (dir.sqrMagnitude < 1e-8f)
                    return false;
                dir.Normalize();
                RotateTowardsDirection(dir, suspiciousTurnSpeed);
                float moveSpd = chaseSpeed * searchSpeedMultiplier;
                float step = Mathf.Min(moveSpd * Time.fixedDeltaTime, Mathf.Max(0f, dist - searchArrivalDistance));
                _body.MovePosition(_body.position + dir * step);
            }

            return false;
        }

        // On-spot sweep
        _scanOffset += _scanVelocity * Time.fixedDeltaTime;

        if (_scanOffset >= scanHalfAngle)
        {
            _scanOffset = scanHalfAngle;
            _scanVelocity = -scanSpeed;
        }
        else if (_scanOffset <= -scanHalfAngle)
        {
            _scanOffset = -scanHalfAngle;
            _scanVelocity = scanSpeed;
        }

        _body.MoveRotation(_scanBaseRotation + _scanOffset);
        return true; // is scanning
    }

    /// <summary>Called on entering Idle to start a gentle deterministic look sweep.</summary>
    public void BeginIdleLookAround(float halfAngleDeg = 10f, float sweepSpeed = 18f, float pauseAtEnds = 0.45f)
    {
        idleLookHalfAngle = Mathf.Max(0f, halfAngleDeg);
        idleLookSweepSpeed = Mathf.Max(1f, sweepSpeed);
        idleLookPauseAtEnds = Mathf.Max(0f, pauseAtEnds);
        _idleLookBaseRotation = _body.rotation;
        _idleLookOffset = 0f;
        _idleLookVelocity = idleLookSweepSpeed;
        _idleLookPauseTimer = 0f;
        _idleLookInitialized = true;
    }

    /// <summary>Search-like idle scanning, deterministic and slower.</summary>
    public void IdleLookAround(float dt)
    {
        if (!_idleLookInitialized)
            BeginIdleLookAround(idleLookHalfAngle, idleLookSweepSpeed, idleLookPauseAtEnds);

        if (_idleLookPauseTimer > 0f)
        {
            _idleLookPauseTimer -= dt;
            return;
        }

        _idleLookOffset += _idleLookVelocity * dt;

        if (_idleLookOffset >= idleLookHalfAngle)
        {
            _idleLookOffset = idleLookHalfAngle;
            _idleLookVelocity = -idleLookSweepSpeed;
            _idleLookPauseTimer = idleLookPauseAtEnds;
        }
        else if (_idleLookOffset <= -idleLookHalfAngle)
        {
            _idleLookOffset = -idleLookHalfAngle;
            _idleLookVelocity = idleLookSweepSpeed;
            _idleLookPauseTimer = idleLookPauseAtEnds;
        }

        _body.MoveRotation(_idleLookBaseRotation + _idleLookOffset);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Internal helpers
    // ────────────────────────────────────────────────────────────────────────

    private void RotateTowards(Vector2 targetPosition, float degreesPerSecond)
    {
        Vector2 to = targetPosition - _body.position;
        if (to.sqrMagnitude < 0.0001f) return;

        RotateTowardsDirection(to.normalized, degreesPerSecond);
    }

    private void RotateTowardsDirection(Vector2 dir, float degreesPerSecond)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        float targetDeg = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
        _body.MoveRotation(Mathf.MoveTowardsAngle(
            _body.rotation, targetDeg, degreesPerSecond * Time.fixedDeltaTime));
    }

    static bool IsPlayerCollider(Collider2D col) =>
        col != null && col.GetComponentInParent<PlayerMovement>(true) != null;

    int OverlapSeparation(Vector2 position, float radius)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(separationMask);
        filter.useTriggers = Physics2D.queriesHitTriggers;
        return Physics2D.OverlapCircle(position, radius, filter, _separationHits);
    }

    void TryDepenetrateFromPlayerIfOverlapping()
    {
        if (_selfCollider == null) return;
        float r = Mathf.Max(separationRadius, 0.55f);
        int n = OverlapSeparation(_body.position, r);
        for (int i = 0; i < n; i++)
        {
            var col = _separationHits[i];
            if (col == null || col == _selfCollider || col.isTrigger || !IsPlayerCollider(col))
                continue;
            ColliderDistance2D cd = _selfCollider.Distance(col);
            if (!cd.isOverlapped && cd.distance > 0.03f)
                continue;
            Vector2 nrm = cd.normal.sqrMagnitude > 1e-6f ? cd.normal : Vector2.right;
            float push = cd.isOverlapped ? Mathf.Max(0.06f, -cd.distance + 0.03f) : Mathf.Max(0f, 0.05f - cd.distance);
            if (push > 0f)
                _body.MovePosition(_body.position + nrm * push);
            return;
        }
    }

    private Vector2 ApplySeparation(Vector2 desiredDir)
    {
        if (!enableSeparation || desiredDir.sqrMagnitude < 1e-6f)
            return desiredDir;

        float radius = Mathf.Max(0.05f, separationRadius);
        int count = OverlapSeparation(_body.position, radius);
        if (count <= 0)
            return desiredDir;

        Vector2 away = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            var col = _separationHits[i];
            if (col == null) continue;
            if (_selfCollider != null && col == _selfCollider) continue;
            if (IsPlayerCollider(col))
                continue;

            var rb = col.attachedRigidbody;
            if ((rb == null || rb == _body) && !IsPlayerCollider(col)) continue;

            Vector2 nearest = col.ClosestPoint(_body.position);
            Vector2 toOther = nearest - _body.position;
            float d = toOther.magnitude;
            if (d < 0.0001f)
            {
                float ang = transform.GetInstanceID() * 0.913f;
                away -= new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                continue;
            }

            float w = 1f - Mathf.Clamp01(d / radius);
            away -= (toOther / d) * w;
        }

        if (away.sqrMagnitude < 1e-6f)
            return desiredDir;

        Vector2 combined = desiredDir + away * Mathf.Max(0f, separationWeight);
        return combined.sqrMagnitude > 1e-6f ? combined.normalized : desiredDir;
    }
}
