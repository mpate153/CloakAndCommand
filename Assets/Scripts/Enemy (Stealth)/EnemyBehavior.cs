using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Enemy brain: watcher/sprinter/bulwark. Sprinters summoned by a watcher use <see cref="EnemySprinterDeployment"/> + deploy/sweep/chase/leave (no Idle).
/// </summary>
[RequireComponent(typeof(EnemyVision))]
[RequireComponent(typeof(EnemyMovement))]
public class EnemyAI : MonoBehaviour
{
    public enum State
    {
        Idle, Suspicious, Search, Chase,
        SprinterDeploy, SprinterSweep, SprinterChase, SprinterLeave
    }

    [Header("Suspicious")]
    [SerializeField] private float suspiciousDuration = 2f;
    [SerializeField] private float coneFlashFrequency = 8f;

    [Header("Search")]
    [SerializeField] private float searchDuration = 5f;

    [Header("Chase & Attack")]
    [Tooltip("Enemy attempts a melee swing when the player is within this distance (animation always plays).")]
    [SerializeField] private float attackRange = 0.65f;
    [SerializeField] private float attackDamage = 1f;
    public float AttackDamage => attackDamage;
    [SerializeField] private float attackInterval = 1f;
    [Tooltip("2D overlap radius at the hit point (must overlap player colliders to kill).")]
    [SerializeField] private float attackHitRadius = 0.55f;
    [Tooltip("Hit circle center: this far from the enemy toward the player.")]
    [SerializeField] private float attackHitDistance = 0.35f;
    [SerializeField] private LayerMask attackHitMask = ~0;
    [Tooltip("Watcher/Bulwark chase: lose sight this long before Search.")]
    [SerializeField] private float chaseLoseSightDuration = 10f;

    [Header("Optional")]
    [SerializeField] private MonoBehaviour patrolBehaviour;

    [Header("Events")]
    public UnityEvent onEnterSuspicious;
    public UnityEvent onEnterSearch;
    public UnityEvent onEnterChase;
    public UnityEvent onReturnIdle;

    [Header("Audio")]
    [SerializeField] private AudioClip[] playerSpottedClips;
    [SerializeField] private AudioClip[] chaseStartClips;
    [SerializeField] private AudioClip[] chaseEndClips;
    [Tooltip("Played on Suspicious after the first time this guard has gone suspicious (any route).")]
    [FormerlySerializedAs("reSpottedAfterChaseClips")]
    [SerializeField] private AudioClip[] reSuspiciousClips;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] [Range(0f, 1f)] private float spottedVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float chaseStartVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float chaseEndVolume = 1f;
    [FormerlySerializedAs("reSpottedAfterChaseVolume")]
    [SerializeField] [Range(0f, 1f)] private float reSuspiciousVolume = 1f;

    private EnemyVision _vision;
    private EnemyMovement _movement;
    private EnemyArchetype _archetype;
    private EnemyAnimatorDriver _animDriver;

    private State _state = State.Idle;
    private float _suspiciousTimer;
    private float _searchTimer;
    private Vector2 _lastKnownPos;
    private float _attackCooldown;
    private float _chaseLoseSightTimer;
    private bool _searchFollowedChase;
    private Coroutine _flashRoutine;

    private bool _hasEnteredChaseSequence;

    /// <summary>Watcher reinforcement gating flags/state.</summary>
    private bool _watcherFirstChaseHasEnded;
    private bool _watcherBulwarksSpawnedThisChase;
    private float _watcherCurrentChaseElapsed;

    private bool _sprinterFlow;
    private Vector2 _sprInvestigateCenter;
    private Vector2 _sprDeployTarget;
    private Vector2 _sprLeaveTarget;
    private float _sprSweepRadius;
    private float _sprSweepTheta;
    private float _sprOffscreenMargin;
    private float _sprinterChaseBlindTimer;

    static readonly Collider2D[] MeleeHitBuffer = new Collider2D[24];

    public State CurrentState => _state;

    /// <summary>
    /// Same detection rules as patrol/chase (cone + LOS, or Bulwark omni LOS). Used for stealth melee gating.
    /// </summary>
    public bool IsPlayerCurrentlyDetected() => PlayerIsDetected();

    /// <summary>Called by <see cref="SaveManager"/> after it restores private fields via reflection.</summary>
    public void RestoreVisualsAfterSave(bool patrolBehaviourEnabled)
    {
        SyncVisionAndMovementAfterSaveLoad(patrolBehaviourEnabled);
    }

    void SyncVisionAndMovementAfterSaveLoad(bool patrolWasEnabled)
    {
        if (_sprinterFlow)
        {
            if (patrolBehaviour != null)
                patrolBehaviour.enabled = false;

            switch (_state)
            {
                case State.SprinterDeploy:
                case State.SprinterSweep:
                case State.SprinterLeave:
                    _vision.SetIdleConeSway(false);
                    _vision.SetConeColor(_vision.SuspiciousFlashColor);
                    break;
                case State.SprinterChase:
                    _vision.SetIdleConeSway(false);
                    _vision.SetConeColor(_vision.ChaseConeColor);
                    if (_vision.PlayerTransform != null)
                        _movement.ResetSprinterStaleChase(_vision.PlayerTransform.position);
                    break;
            }

            return;
        }

        _vision.SetIdleConeSway(false);

        switch (_state)
        {
            case State.Idle:
                StopFlash();
                _vision.SetConeColor(_vision.CalmConeColor);
                if (patrolBehaviour != null)
                    patrolBehaviour.enabled = patrolWasEnabled;
                if (_archetype != null && _archetype.Kind == EnemyKind.Watcher)
                {
                    float half = Mathf.Clamp(_archetype.IdleSwayDegrees, 2f, 24f);
                    float sweep = Mathf.Max(4f, _archetype.IdleSwaySpeed * 10f);
                    _movement.BeginIdleLookAround(half, sweep, 0.55f);
                }
                else
                    _movement.BeginIdleLookAround(8f, 10f, 0.5f);
                break;
            case State.Suspicious:
                if (patrolBehaviour != null)
                    patrolBehaviour.enabled = false;
                StartFlash();
                break;
            case State.Search:
                if (patrolBehaviour != null)
                    patrolBehaviour.enabled = false;
                _movement.BeginSearch();
                _vision.SetConeColor(_vision.SearchConeColor);
                break;
            case State.Chase:
                if (patrolBehaviour != null)
                    patrolBehaviour.enabled = false;
                _vision.SetConeColor(_vision.ChaseConeColor);
                break;
        }
    }

    /// <summary>
    /// True while this unit is actively chasing the player (watcher <see cref="State.Chase"/> or <see cref="State.SprinterChase"/>).
    /// <see cref="DynamicMusic"/> uses this to pick explore vs chase layers.
    /// </summary>
    public bool IsInChaseMusicState => _state == State.Chase || _state == State.SprinterChase;

    static readonly List<EnemyAI> MusicQueryRegistry = new List<EnemyAI>();

    /// <summary>Whether any registered <see cref="EnemyAI"/> is currently in a chase music state.</summary>
    public static bool AnyEnemyInChaseMusicState()
    {
        for (int i = MusicQueryRegistry.Count - 1; i >= 0; i--)
        {
            var e = MusicQueryRegistry[i];
            if (e == null)
            {
                MusicQueryRegistry.RemoveAt(i);
                continue;
            }
            if (e.IsInChaseMusicState)
                return true;
        }
        return false;
    }

    void OnEnable()
    {
        if (!MusicQueryRegistry.Contains(this))
            MusicQueryRegistry.Add(this);
    }

    void OnDisable()
    {
        MusicQueryRegistry.Remove(this);
    }

    private void Awake()
    {
        _vision = GetComponent<EnemyVision>();
        _movement = GetComponent<EnemyMovement>();
        TryGetComponent(out _archetype);
        if (!TryGetComponent(out _animDriver))
            _animDriver = GetComponentInChildren<EnemyAnimatorDriver>(true);
        EnsureAudioSource();
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        if (!TryGetComponent(out audioSource))
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;
    }

    private void Start()
    {
        // Summoned flow should key off deployment payload, not prefab kind alone.
        // This avoids silent failure if a variant is misconfigured as non-Sprinter.
        if (GetComponent<EnemySprinterDeployment>() != null)
        {
            if (_archetype != null && _archetype.Kind != EnemyKind.Sprinter)
            {
                Debug.LogWarning(
                    $"{nameof(EnemyAI)} on '{name}': has {nameof(EnemySprinterDeployment)} but archetype kind is {_archetype.Kind}. Running sprinter summoned flow anyway.",
                    this);
            }
            TryBeginSprinterSummonedFlow();
        }
        else if (_archetype != null && _archetype.Kind == EnemyKind.Sprinter)
            TryBeginSprinterSummonedFlow();
    }

    private void TryBeginSprinterSummonedFlow()
    {
        var dep = GetComponent<EnemySprinterDeployment>();
        if (dep != null)
        {
            _sprinterFlow = true;
            _sprInvestigateCenter = dep.InvestigationCenter;
            _sprOffscreenMargin = dep.OffscreenMargin;
            _sprSweepRadius = dep.SweepOrbitRadius;
            // Summoned sprinters should converge on the watcher's detected player location first.
            _sprDeployTarget = _sprInvestigateCenter;
            _sprSweepTheta = Random.Range(0f, Mathf.PI * 2f);
            _state = State.SprinterDeploy;
            _vision.SetConeColor(_vision.SuspiciousFlashColor);
            Destroy(dep);
        }
        else
        {
            _sprinterFlow = true;
            _sprInvestigateCenter = transform.position;
            _sprSweepRadius = 2.5f;
            _sprOffscreenMargin = _archetype != null ? _archetype.OffscreenMarginWorld : 2f;
            _state = State.SprinterSweep;
            _sprSweepTheta = Random.Range(0f, Mathf.PI * 2f);
            _vision.SetConeColor(_vision.SuspiciousFlashColor);
        }

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = false;
    }

    private void PickSprinterDeployTarget(float rMin, float rMax)
    {
        float ang = Random.Range(0f, Mathf.PI * 2f);
        float rad = Random.Range(rMin, rMax);
        _sprDeployTarget = _sprInvestigateCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
    }

    private void Update()
    {
        if (IsSprinterFlow())
        {
            UpdateSprinterFlowVisionSway();
            UpdateSprinterFlow();
            return;
        }

        RefreshVisionIdleSway();
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Suspicious: UpdateSuspicious(); break;
            case State.Search: UpdateSearch(); break;
            case State.Chase: UpdateChase(); break;
        }
    }

    private void FixedUpdate()
    {
        bool chasingPlayer = IsSprinterFlow()
            ? _state == State.SprinterChase
            : _state == State.Chase;
        _movement.SuppressDepenetrationFromPlayerDuringChase = chasingPlayer;
        _movement.SetIgnorePlayerCollisionsWhileChasing(chasingPlayer, _vision.PlayerTransform);

        if (IsSprinterFlow())
        {
            FixedSprinterFlow();
            return;
        }

        switch (_state)
        {
            case State.Idle:
                // Only do idle in-place look if patrol is not currently driving movement.
                if (patrolBehaviour == null || !patrolBehaviour.enabled)
                    _movement.IdleLookAround(Time.fixedDeltaTime);
                break;
            case State.Suspicious:
                _movement.RotateSuspicious(_vision.PlayerTransform.position);
                break;
            case State.Search:
                _movement.SearchMove(_lastKnownPos);
                break;
            case State.Chase:
                _movement.ChasePlayer(_vision.PlayerTransform.position);
                break;
        }
    }

    private bool IsSprinterFlow() => _sprinterFlow;

    private void UpdateSprinterFlowVisionSway()
    {
        // Mesh-only cone sway (EnemyVision) does not rotate the rigidbody/sprite — leave off so the cone matches facing.
        if (_state == State.SprinterSweep && _archetype != null && _archetype.SprinterSweepMeshConeSway)
        {
            _vision.SetIdleConeSway(true,
                _archetype.SprinterSweepConeSwayDegrees,
                _archetype.SprinterSweepConeSwaySpeed);
        }
        else
            _vision.SetIdleConeSway(false);
    }

    private void UpdateSprinterFlow()
    {
        switch (_state)
        {
            case State.SprinterDeploy:
                if (_vision.CanSeePlayer)
                    EnterSprinterChase();
                break;
            case State.SprinterSweep:
                if (_vision.CanSeePlayer)
                    EnterSprinterChase();
                break;
            case State.SprinterChase:
                UpdateSprinterChase();
                break;
            case State.SprinterLeave:
                if (OffScreenSpawn2D.IsBeyondView(transform.position, Camera.main, _sprOffscreenMargin))
                    Destroy(gameObject);
                break;
        }
    }

    private void FixedSprinterFlow()
    {
        float moveSpeed = _movement.chaseSpeed * _movement.searchSpeedMultiplier;
        float turn = _movement.suspiciousTurnSpeed;

        switch (_state)
        {
            case State.SprinterDeploy:
                if (Vector2.Distance((Vector2)transform.position, _sprDeployTarget)
                    <= (_archetype != null ? _archetype.SprinterDeployArriveDistance : 0.42f))
                    EnterSprinterSweep();
                else
                    _movement.WalkTowards(_sprDeployTarget, moveSpeed, turn);
                break;
            case State.SprinterSweep:
            {
                float w = _archetype != null ? _archetype.SprinterSweepAngularSpeed : 52f;
                _sprSweepTheta += w * Mathf.Deg2Rad * Time.fixedDeltaTime;
                Vector2 orbit = _sprInvestigateCenter
                    + new Vector2(Mathf.Cos(_sprSweepTheta), Mathf.Sin(_sprSweepTheta)) * _sprSweepRadius;
                _movement.WalkTowards(orbit, moveSpeed * 0.85f, turn);
                break;
            }
            case State.SprinterChase:
                if (_vision.PlayerTransform != null)
                {
                    _movement.ChasePlayerWithStaleTarget(
                        _vision.PlayerTransform.position,
                        _archetype != null ? _archetype.SprinterStaleChaseApproachPerSecond : 2.45f);
                }
                break;
            case State.SprinterLeave:
                _movement.WalkTowards(_sprLeaveTarget, _movement.chaseSpeed * 1.05f, turn * 1.1f);
                break;
        }
    }

    private void UpdateSprinterChase()
    {
        if (_vision.PlayerTransform == null) return;

        float blind = _archetype != null ? _archetype.SprinterChaseLoseSightDuration : 0.4f;
        if (_vision.CanSeePlayer)
            _sprinterChaseBlindTimer = 0f;
        else
        {
            _sprinterChaseBlindTimer += Time.deltaTime;
            if (_sprinterChaseBlindTimer >= blind)
                EnterSprinterLeave();
        }

        if (_attackCooldown > 0f)
            _attackCooldown -= Time.deltaTime;
        float dist = GetDistanceToPlayerForMelee();
        if (dist <= attackRange && _attackCooldown <= 0f)
        {
            _attackCooldown = attackInterval;
            _animDriver?.PlayAttackAnimation();
            TryMeleeHitPlayer(dist);
        }
    }

    private void EnterSprinterSweep()
    {
        _state = State.SprinterSweep;
        _vision.SetConeColor(_vision.SuspiciousFlashColor);
    }

    private void EnterSprinterChase()
    {
        _state = State.SprinterChase;
        _hasEnteredChaseSequence = true;
        _sprinterChaseBlindTimer = 0f;
        StopFlash();
        _vision.SetConeColor(_vision.ChaseConeColor);
        if (_vision.PlayerTransform != null)
            _movement.ResetSprinterStaleChase(_vision.PlayerTransform.position);
        PlayRandomOneShot(chaseStartClips, chaseStartVolume);
        onEnterChase?.Invoke();
    }

    private void EnterSprinterLeave()
    {
        _state = State.SprinterLeave;
        _vision.SetConeColor(_vision.SuspiciousFlashColor);
        _sprLeaveTarget = OffScreenSpawn2D.RandomBeyondView(Camera.main, _sprOffscreenMargin);
    }

    private void UpdateIdle()
    {
        if (PlayerIsDetected())
            EnterSuspicious();
        else if (LureAttracts(out Transform lureTf))
            EnterSearchFromLure(lureTf);
    }

    private void UpdateSuspicious()
    {
        if (!PlayerIsDetected())
        {
            EnterSearch();
            return;
        }

        _suspiciousTimer += Time.deltaTime;
        if (_suspiciousTimer >= suspiciousDuration)
            EnterChase();
    }

    private void UpdateSearch()
    {
        if (PlayerIsDetected())
        {
            EnterSuspicious();
            return;
        }

        _searchTimer -= Time.deltaTime;
        if (_searchTimer <= 0f)
            EnterIdle();
    }

    private void UpdateChase()
    {
        if (_vision.PlayerTransform == null) return;

        if (_archetype != null && _archetype.Kind == EnemyKind.Watcher)
            TickWatcherBulwarkSpawnWhileInChase();

        if (PlayerIsDetected())
            _chaseLoseSightTimer = 0f;
        else
        {
            _chaseLoseSightTimer += Time.deltaTime;
            if (_chaseLoseSightTimer >= chaseLoseSightDuration)
            {
                _chaseLoseSightTimer = 0f;
                EnterSearch();
                return;
            }
        }

        if (_attackCooldown > 0f)
            _attackCooldown -= Time.deltaTime;

        float dist = GetDistanceToPlayerForMelee();
        if (dist <= attackRange && _attackCooldown <= 0f)
        {
            _attackCooldown = attackInterval;
            _animDriver?.PlayAttackAnimation();
            TryMeleeHitPlayer(dist);
        }
    }

    private void EnterIdle()
    {
        _state = State.Idle;
        _searchFollowedChase = false;
        StopFlash();
        _vision.SetConeColor(_vision.CalmConeColor);
        if (_archetype != null && _archetype.Kind == EnemyKind.Watcher)
        {
            float half = Mathf.Clamp(_archetype.IdleSwayDegrees, 2f, 24f);
            float sweep = Mathf.Max(4f, _archetype.IdleSwaySpeed * 10f);
            _movement.BeginIdleLookAround(half, sweep, 0.55f);
        }
        else
            _movement.BeginIdleLookAround(8f, 10f, 0.5f);

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = true;

        onReturnIdle?.Invoke();
    }

    private void EnterSuspicious()
    {
        bool fromIdle = _state == State.Idle;

        _state = State.Suspicious;
        _suspiciousTimer = 0f;
        _searchFollowedChase = false;

        if (fromIdle && patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        StartFlash();
        if (!_hasEnteredChaseSequence)
        {
            if (HasAnyClip(playerSpottedClips))
                PlayRandomOneShot(playerSpottedClips, spottedVolume);
            else if (HasAnyClip(reSuspiciousClips))
                PlayRandomOneShot(reSuspiciousClips, reSuspiciousVolume);
        }
        else if (HasAnyClip(reSuspiciousClips))
            PlayRandomOneShot(reSuspiciousClips, reSuspiciousVolume);
        else if (HasAnyClip(playerSpottedClips))
            PlayRandomOneShot(playerSpottedClips, spottedVolume);
        onEnterSuspicious?.Invoke();
    }

    private void EnterSearch()
    {
        bool fromChase = _state == State.Chase;
        Vector2 lastKnown = _vision.PlayerTransform != null
            ? (Vector2)_vision.PlayerTransform.position
            : Vector2.zero;
        EnterSearchInternal(fromChase, lastKnown, notifyMusic: true);
    }

    private void EnterSearchFromLure(Transform lureTransform)
    {
        if (lureTransform == null) return;
        EnterSearchInternal(fromChase: false, (Vector2)lureTransform.position, notifyMusic: false);
    }

    private void EnterSearchInternal(bool fromChase, Vector2 lastKnown, bool notifyMusic)
    {
        State previous = _state;
        _state = State.Search;
        _searchFollowedChase = fromChase;
        _lastKnownPos = lastKnown;
        _searchTimer = searchDuration;

        if (previous == State.Idle && patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        _movement.BeginSearch();
        StopFlash();
        _vision.SetConeColor(_vision.SearchConeColor);

        if (fromChase)
            PlayRandomOneShot(chaseEndClips, chaseEndVolume);

        if (fromChase && _archetype != null && _archetype.Kind == EnemyKind.Watcher)
            _watcherFirstChaseHasEnded = true;

        onEnterSearch?.Invoke();
        if (notifyMusic)
            DynamicMusic.NotifyEnemyEnteredSearch();
    }

    private bool LureAttracts(out Transform lureTransform)
    {
        lureTransform = null;
        if (_archetype != null && _archetype.Kind == EnemyKind.Bulwark)
            return _vision.TryGetVisibleLureOmni(out lureTransform);
        return _vision.TryGetVisibleLureInCone(out lureTransform);
    }

    private void EnterChase()
    {
        _state = State.Chase;
        _hasEnteredChaseSequence = true;
        _watcherCurrentChaseElapsed = 0f;
        _watcherBulwarksSpawnedThisChase = false;
        _searchFollowedChase = false;
        _chaseLoseSightTimer = 0f;
        StopFlash();
        _vision.SetConeColor(_vision.ChaseConeColor);

        if (patrolBehaviour != null)
            patrolBehaviour.enabled = false;

        PlayRandomOneShot(chaseStartClips, chaseStartVolume);
        if (_archetype != null && _archetype.Kind == EnemyKind.Watcher)
            TryWatcherReinforcementsOnEnterChase();
        onEnterChase?.Invoke();
    }

    private void TryWatcherReinforcementsOnEnterChase()
    {
        if (_archetype == null || _vision.PlayerTransform == null) return;

        // Sprinters: roll every chase entry.
        if (Random.value <= _archetype.SprinterReinforceChanceAfterFirstCycle)
            _archetype.SpawnWatcherSprinterReinforcements(transform, _vision.PlayerTransform.position, Camera.main);

        // Bulwarks: only after first chase has ended, OR during first chase once it lasts long enough.
        if (_watcherFirstChaseHasEnded)
            SpawnWatcherBulwarksNow();
    }

    private void TickWatcherBulwarkSpawnWhileInChase()
    {
        if (_watcherBulwarksSpawnedThisChase || _watcherFirstChaseHasEnded)
            return;

        _watcherCurrentChaseElapsed += Time.deltaTime;
        if (_watcherCurrentChaseElapsed >= 30f)
            SpawnWatcherBulwarksNow();
    }

    private void SpawnWatcherBulwarksNow()
    {
        if (_watcherBulwarksSpawnedThisChase || _archetype == null) return;
        _archetype.SpawnWatcherBulwarksOnEnterChase(transform);
        _watcherBulwarksSpawnedThisChase = true;
    }

    private bool PlayerIsDetected()
    {
        if (_archetype != null && _archetype.Kind == EnemyKind.Bulwark)
            return OmnidirectionalLosAware();
        return _vision.CanSeePlayer;
    }

    bool IsInChaseOrSprinterChase() =>
        _state == State.Chase || _state == State.SprinterChase;

    /// <summary>
    /// Shortest distance from this enemy to the player’s colliders (or vision target if none), so range checks match overlap even when the Player tag sits on an offset empty.
    /// </summary>
    float GetDistanceToPlayerForMelee()
    {
        if (_vision.PlayerTransform == null)
            return float.MaxValue;
        Vector2 ep = transform.position;
        PlayerHealth health = PlayerHealth.FindForGameplayTransform(_vision.PlayerTransform);
        Transform root = health != null ? health.transform : _vision.PlayerTransform.root;
        var cols = root.GetComponentsInChildren<Collider2D>(true);
        float best = Vector2.Distance(ep, (Vector2)_vision.PlayerTransform.position);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider2D c = cols[i];
            if (c == null || !c.enabled || c.isTrigger)
                continue;
            best = Mathf.Min(best, Vector2.Distance(ep, c.ClosestPoint(ep)));
        }
        return best;
    }

    static PlayerHealth ResolvePlayerHealthForKill(Transform visionPlayer)
    {
        if (visionPlayer == null)
            return null;
        PlayerHealth h = PlayerHealth.FindForGameplayTransform(visionPlayer);
        if (h != null)
            return h;
        return Object.FindFirstObjectByType<PlayerHealth>();
    }

    /// <summary>
    /// Tries physics hits first; if none land, chase/sprinter-chase still kills in <see cref="attackRange"/> so game over always works.
    /// </summary>
    void TryMeleeHitPlayer(float distToPlayer)
    {
        if (distToPlayer > attackRange || _vision.PlayerTransform == null)
            return;

        if (TryMeleeHitPlayerViaPhysics())
            return;

        if (IsInChaseOrSprinterChase())
        {
            PlayerHealth h = ResolvePlayerHealthForKill(_vision.PlayerTransform);
            h?.DieFromEnemy();
        }
    }

    bool TryMeleeHitPlayerViaPhysics()
    {
        Vector2 enemyPos = transform.position;
        Vector2 playerPos = _vision.PlayerTransform.position;
        Vector2 dir = playerPos - enemyPos;
        if (dir.sqrMagnitude < 1e-8f)
            dir = Vector2.up;
        else
            dir.Normalize();

        Vector2 origin = enemyPos + dir * attackHitDistance;

        var filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = attackHitMask;
        filter.useTriggers = true;

        int n = Physics2D.OverlapCircle(origin, attackHitRadius, filter, MeleeHitBuffer);
        for (int i = 0; i < n; i++)
        {
            if (TryConsumeHitOnPlayerCollider(MeleeHitBuffer[i]))
                return true;
        }

        return TryMeleeHitPlayerByColliderDistance();
    }

    bool TryConsumeHitOnPlayerCollider(Collider2D col)
    {
        if (col == null || _vision.PlayerTransform == null)
            return false;
        PlayerHealth health = PlayerHealth.FindForGameplayTransform(col.transform);
        if (health == null)
            return false;
        PlayerHealth visionHealth = PlayerHealth.FindForGameplayTransform(_vision.PlayerTransform);
        if (visionHealth != health)
            return false;
        health.DieFromEnemy();
        return true;
    }

    bool TryMeleeHitPlayerByColliderDistance()
    {
        if (_vision.PlayerTransform == null)
            return false;

        Vector2 ep = transform.position;
        float maxGap = Mathf.Max(attackHitRadius, 0.12f);
        PlayerHealth expected = ResolvePlayerHealthForKill(_vision.PlayerTransform);
        if (expected == null)
            return false;

        Collider2D selfCol = GetComponent<Collider2D>();
        var playerCols = expected.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < playerCols.Length; i++)
        {
            var pc = playerCols[i];
            if (pc == null || !pc.enabled || pc.isTrigger || pc == selfCol)
                continue;
            // Do not filter by attackHitMask here — wrong layer would block kills while chase overlap is ignored (Physics2D.IgnoreCollision).

            if (selfCol != null && selfCol.enabled)
            {
                ColliderDistance2D d = Physics2D.Distance(selfCol, pc);
                if (d.isOverlapped || d.distance <= maxGap)
                {
                    expected.DieFromEnemy();
                    return true;
                }
            }
            else
            {
                float gap = Vector2.Distance(ep, pc.ClosestPoint(ep));
                if (gap <= maxGap)
                {
                    expected.DieFromEnemy();
                    return true;
                }
            }
        }

        return false;
    }

    private bool OmnidirectionalLosAware()
    {
        if (_vision.PlayerTransform == null) return false;
        float d = Vector2.Distance(transform.position, _vision.PlayerTransform.position);
        if (d > _vision.GetOmniLosAwarenessDistance()) return false;
        return _vision.HasUnobstructedLineToPlayer();
    }

    private void RefreshVisionIdleSway()
    {
        // Idle scanning now turns the enemy body; cone-only sway is disabled.
        _vision.SetIdleConeSway(false);
    }

    private static bool HasAnyClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) return true;
        }
        return false;
    }

    private void PlayRandomOneShot(AudioClip[] clips, float volume)
    {
        if (!HasAnyClip(clips)) return;
        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) count++;
        }
        int pick = Random.Range(0, count);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (pick-- == 0)
            {
                PlayOneShot(clips[i], volume);
                return;
            }
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null) return;
        EnsureAudioSource();
        if (audioSource != null)
        {
            if (GameAudio.SfxOutputGroup != null)
                audioSource.outputAudioMixerGroup = GameAudio.SfxOutputGroup;
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            return;
        }
        GameAudio.PlaySfx(clip, transform.position, volume);
    }

    private void StartFlash()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private void StopFlash()
    {
        if (_flashRoutine == null) return;
        StopCoroutine(_flashRoutine);
        _flashRoutine = null;
    }

    private IEnumerator FlashRoutine()
    {
        while (ShouldFlashSuspiciousCone())
        {
            float t = (Mathf.Sin(Time.time * coneFlashFrequency) + 1f) * 0.5f;
            _vision.SetConeColor(Color.Lerp(_vision.CalmConeColor, _vision.SuspiciousFlashColor, t));
            yield return null;
        }
        _flashRoutine = null;
    }

    private bool ShouldFlashSuspiciousCone()
    {
        return _state == State.Suspicious;
    }

}
