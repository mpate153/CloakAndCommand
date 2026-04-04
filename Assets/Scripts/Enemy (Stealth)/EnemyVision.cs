using UnityEngine;

/// <summary>
/// Builds and renders a vision-cone mesh.
/// Exposes CanSeePlayer and a SetConeColor API so EnemyAI can drive visuals.
/// This component has zero knowledge of state — it only detects and draws.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyVision : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float visionRange = 6f;
    [Tooltip("Full cone angle in degrees.")]
    [SerializeField] private float visionAngleDegrees = 70f;
    [Tooltip("Player must be this much inside max range before detection triggers.")]
    [SerializeField] private float detectionRangeInset = 0.1f;
    [Tooltip("Player must be this many degrees inside cone edge before detection triggers.")]
    [SerializeField] private float detectionAngleInsetDegrees = 2f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Cone Visuals")]
    [SerializeField] private Color calmConeColor = new Color(1f, 0.20f, 0.20f, 0.25f);
    [SerializeField] private Color suspiciousFlashColor = new Color(1f, 0.55f, 0.00f, 0.55f);
    [SerializeField] private Color searchConeColor = new Color(1f, 0.90f, 0.00f, 0.35f);
    [SerializeField] private Color chaseConeColor = new Color(1f, 0.08f, 0.08f, 0.55f);

    public Color CalmConeColor => calmConeColor;
    public Color SuspiciousFlashColor => suspiciousFlashColor;
    public Color SearchConeColor => searchConeColor;
    public Color ChaseConeColor => chaseConeColor;

    [Header("Cone Mesh")]
    [SerializeField] private int coneMeshSegments = 24;
    [SerializeField] private string coneSortingLayer = "Default";
    [SerializeField] private int coneSortingOrder = -10;
    [SerializeField] private Material coneMaterial;
    [SerializeField] private bool clipConeAgainstObstacles = true;
    [Tooltip("Layers that visually clip the cone mesh. Leave empty to use Obstacle Mask.")]
    [SerializeField] private LayerMask coneClipMask;
    [Tooltip("Small visual extension after hit point. Keep near 0 for stable clipping.")]
    [SerializeField] private float coneClipPadding = 0f;
    [Tooltip("Ignore trigger colliders when clipping cone rays.")]
    [SerializeField] private bool ignoreTriggerCollidersForConeClip = true;
    [Header("Debug")]
    [SerializeField] private bool debugDrawConeRays = false;
    [SerializeField] private Color debugRayNoHitColor = new Color(0.2f, 1f, 0.2f, 0.8f);
    [SerializeField] private Color debugRayHitColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    // ── Public read-only ─────────────────────────────────────────────────────
    public bool CanSeePlayer { get; private set; }
    public Transform PlayerTransform { get; private set; }

    // ── Private ──────────────────────────────────────────────────────────────
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private Collider2D[] _selfColliders;
    private Vector3[] _debugRayEnds;
    private bool[] _debugRayHit;

    private float _rangeMultiplier = 1f;
    private float _angleMultiplier = 1f;
    private bool _coneVisionEnabled = true;
    private bool _coneVisualEnabled = true;
    private bool _idleConeSwayActive;
    private float _idleSwayAmplitudeDegrees = 6f;
    private float _idleSwaySpeed = 1.1f;

    // ────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _selfColliders = GetComponentsInChildren<Collider2D>();
        BuildConeVisuals();
    }

    private void Start()
    {
        FindPlayer();
        RebuildConeMesh();
        SetConeColor(calmConeColor);
    }

    private void Update()
    {
        if (PlayerTransform == null) FindPlayer();
        CanSeePlayer = _coneVisionEnabled && PlayerTransform != null && CheckLineOfSight();
        RebuildConeMesh();
    }

    private void OnValidate()
    {
        visionRange = Mathf.Max(0.1f, visionRange);
        visionAngleDegrees = Mathf.Clamp(visionAngleDegrees, 1f, 359f);
        detectionRangeInset = Mathf.Max(0f, detectionRangeInset);
        detectionAngleInsetDegrees = Mathf.Max(0f, detectionAngleInsetDegrees);
        coneMeshSegments = Mathf.Clamp(coneMeshSegments, 3, 64);
        coneClipPadding = Mathf.Max(0f, coneClipPadding);
        if (Application.isPlaying && _mesh != null)
            RebuildConeMesh();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────────────────────────────────

    public void SetConeColor(Color c)
    {
        if (_meshRenderer == null) return;
        _meshRenderer.material.color = c;
    }

    public void SetDetectionMultipliers(float rangeMultiplier, float angleMultiplier)
    {
        _rangeMultiplier = Mathf.Max(0.05f, rangeMultiplier);
        _angleMultiplier = Mathf.Max(0.05f, angleMultiplier);
    }

    /// <summary>Nearest thrown <see cref="SpawnLure"/> in the vision cone with clear LOS (same rules as the player).</summary>
    public bool TryGetVisibleLureInCone(out Transform lureTransform)
    {
        lureTransform = null;
        if (!_coneVisionEnabled)
            return false;

        Vector2 origin = transform.position;
        float bestDist = float.MaxValue;
        Transform best = null;

        for (int i = SpawnLure.Active.Count - 1; i >= 0; i--)
        {
            var lure = SpawnLure.Active[i];
            if (lure == null)
            {
                SpawnLure.Active.RemoveAt(i);
                continue;
            }

            Vector2 to = (Vector2)lure.transform.position - origin;
            float dist = to.magnitude;
            if (!IsInsideDetectionCore(to, dist))
                continue;

            if (!LineClearToPointIgnoringLures(origin, lure.transform.position))
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = lure.transform;
            }
        }

        lureTransform = best;
        return best != null;
    }

    /// <summary>Nearest <see cref="SpawnLure"/> within omnidirectional awareness range with clear LOS (Bulwarks).</summary>
    public bool TryGetVisibleLureOmni(out Transform lureTransform)
    {
        lureTransform = null;
        float maxD = GetOmniLosAwarenessDistance();
        Vector2 origin = transform.position;
        float bestDist = float.MaxValue;
        Transform best = null;

        for (int i = SpawnLure.Active.Count - 1; i >= 0; i--)
        {
            var lure = SpawnLure.Active[i];
            if (lure == null)
            {
                SpawnLure.Active.RemoveAt(i);
                continue;
            }

            Vector2 to = (Vector2)lure.transform.position - origin;
            float dist = to.magnitude;
            if (dist < 0.001f || dist > maxD)
                continue;

            if (!LineClearToPointIgnoringLures(origin, lure.transform.position))
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = lure.transform;
            }
        }

        lureTransform = best;
        return best != null;
    }

    /// <summary>Cone raycasts / <see cref="CanSeePlayer"/>. Bulwarks disable this; <see cref="EnemyAI"/> uses omnidirectional LOS for them.</summary>
    public void SetConeVisionEnabled(bool enabled) => _coneVisionEnabled = enabled;

    public void SetConeVisualEnabled(bool enabled)
    {
        _coneVisualEnabled = enabled;
        if (_meshRenderer != null)
            _meshRenderer.enabled = enabled;
    }

    /// <summary>
    /// Bakes a sinusoidal yaw into the cone mesh only (not <see cref="Transform"/> / rigidbody).
    /// Fine for a stationary guard; with a moving sprite it looks detached unless the body rotates the same way.
    /// </summary>
    public void SetIdleConeSway(bool active, float amplitudeDegrees = 6f, float speed = 1.1f)
    {
        _idleConeSwayActive = active;
        _idleSwayAmplitudeDegrees = amplitudeDegrees;
        _idleSwaySpeed = speed;
    }

    float EffectiveVisionRange => visionRange * _rangeMultiplier;
    float EffectiveVisionAngleDegrees => Mathf.Clamp(visionAngleDegrees * _angleMultiplier, 1f, 359f);

    /// <summary>Sprinters / bulwarks: max distance for omnidirectional line-of-sight awareness (same inset as cone).</summary>
    public float GetOmniLosAwarenessDistance() => Mathf.Max(0.01f, EffectiveVisionRange - detectionRangeInset);

    Vector2 VisionUpForDetection()
    {
        if (!_idleConeSwayActive)
            return transform.up;

        float swayDeg = Mathf.Sin(Time.time * _idleSwaySpeed) * _idleSwayAmplitudeDegrees;
        return Quaternion.AngleAxis(swayDeg, Vector3.forward) * (Vector2)transform.up;
    }

    Quaternion IdleSwayRotation()
    {
        if (!_idleConeSwayActive) return Quaternion.identity;
        float swayDeg = Mathf.Sin(Time.time * _idleSwaySpeed) * _idleSwayAmplitudeDegrees;
        return Quaternion.AngleAxis(swayDeg, Vector3.forward);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Detection
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Obstacle raycast only — ignores cone shape. Used by Bulwark proximity + LOS.</summary>
    public bool HasUnobstructedLineToPlayer()
    {
        if (PlayerTransform == null) return false;
        Vector2 origin = transform.position;
        Vector2 toPlayer = (Vector2)PlayerTransform.position - origin;
        float dist = toPlayer.magnitude;
        if (dist < 0.001f) return true;
        RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, dist, obstacleMask);
        return hit.collider == null;
    }

    /// <summary>LOS for lure tests: ignores hits on <see cref="SpawnLure"/> so the lure collider / shared layers cannot block sight.</summary>
    bool LineClearToPointIgnoringLures(Vector2 origin, Vector2 targetWorld)
    {
        Vector2 to = targetWorld - origin;
        float dist = to.magnitude;
        if (dist < 0.001f)
            return true;
        Vector2 dir = to / dist;
        var hits = Physics2D.RaycastAll(origin, dir, dist, obstacleMask);
        float nearestBlock = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D h = hits[i];
            if (h.collider == null) continue;
            if (h.collider.GetComponentInParent<SpawnLure>() != null)
                continue;
            if (h.distance < nearestBlock)
                nearestBlock = h.distance;
        }
        return nearestBlock >= dist - 0.001f;
    }

    private bool CheckLineOfSight()
    {
        Vector2 origin = transform.position;
        Vector2 toPlayer = (Vector2)PlayerTransform.position - origin;
        float dist = toPlayer.magnitude;

        if (!IsInsideDetectionCore(toPlayer, dist)) return false;

        RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, dist, obstacleMask);
        return hit.collider == null;
    }

    private bool IsInsideDetectionCore(Vector2 toPoint, float dist)
    {
        if (dist < 0.001f) return false;

        float vr = EffectiveVisionRange;
        float va = EffectiveVisionAngleDegrees;
        float effectiveRange = Mathf.Max(0.01f, vr - detectionRangeInset);
        float effectiveHalfAngle = Mathf.Max(0.1f, (va * 0.5f) - detectionAngleInsetDegrees);

        if (dist > effectiveRange) return false;
        if (Vector2.Angle(VisionUpForDetection(), toPoint) > effectiveHalfAngle) return false;
        return true;
    }

    private void FindPlayer()
    {
        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null) PlayerTransform = go.transform;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Cone mesh
    // ────────────────────────────────────────────────────────────────────────

    private void BuildConeVisuals()
    {
        var child = new GameObject("VisionCone");
        child.transform.SetParent(transform, false);

        _meshFilter = child.AddComponent<MeshFilter>();
        _meshRenderer = child.AddComponent<MeshRenderer>();
        _mesh = new Mesh { name = "VisionConeMesh" };
        _meshFilter.sharedMesh = _mesh;

        _meshRenderer.sharedMaterial = coneMaterial != null
            ? coneMaterial
            : new Material(Shader.Find("Sprites/Default"));

        _meshRenderer.sortingLayerName = coneSortingLayer;
        _meshRenderer.sortingOrder = coneSortingOrder;
    }

    private void RebuildConeMesh()
    {
        if (_mesh == null || !_coneVisualEnabled) return;

        int n = coneMeshSegments;
        float va = EffectiveVisionAngleDegrees;
        float halfRad = va * 0.5f * Mathf.Deg2Rad;
        Quaternion sway = IdleSwayRotation();

        var verts = new Vector3[n + 2];
        var uvs = new Vector2[n + 2];
        var tris = new int[n * 3];
        if (_debugRayEnds == null || _debugRayEnds.Length != n + 1)
        {
            _debugRayEnds = new Vector3[n + 1];
            _debugRayHit = new bool[n + 1];
        }

        verts[0] = Vector3.zero;
        uvs[0] = Vector2.zero;

        float vr = EffectiveVisionRange;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float a = Mathf.Lerp(-halfRad, halfRad, t);
            Vector3 ld3 = sway * new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
            Vector2 localDir = new Vector2(ld3.x, ld3.y);
            float sampleDistance = vr;

            if (clipConeAgainstObstacles)
            {
                LayerMask mask = coneClipMask.value == 0 ? obstacleMask : coneClipMask;
                Vector2 worldDir = /* sway baked into localDir in parent space → use up-from-local */
                    ((Vector3)localDir).normalized;
                worldDir = transform.TransformDirection(worldDir).normalized;
                sampleDistance = GetVisualClipDistance((Vector2)transform.position, worldDir, vr, mask, out bool clippedByHit);
                _debugRayHit[i] = clippedByHit;
            }
            else
            {
                _debugRayHit[i] = false;
            }

            verts[i + 1] = new Vector3(localDir.x * sampleDistance, localDir.y * sampleDistance, 0f);
            uvs[i + 1] = new Vector2(t, 1f);
            _debugRayEnds[i] = transform.TransformPoint(verts[i + 1]);
        }

        int vi = 0;
        for (int i = 0; i < n; i++)
        {
            tris[vi++] = 0;
            tris[vi++] = i + 1;
            tris[vi++] = i + 2;
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }

    private float GetVisualClipDistance(Vector2 origin, Vector2 worldDir, float maxDistance, LayerMask mask, out bool clippedByHit)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, worldDir, maxDistance, mask);
        float nearest = maxDistance;
        clippedByHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null) continue;
            if (IsSelfCollider(hit.collider)) continue;
            if (ignoreTriggerCollidersForConeClip && hit.collider.isTrigger) continue;

            float d = hit.distance + coneClipPadding;

            if (d < nearest)
            {
                nearest = d;
                clippedByHit = true;
            }
        }

        return Mathf.Clamp(nearest, 0f, maxDistance);
    }

    private bool IsSelfCollider(Collider2D col)
    {
        if (col == null || _selfColliders == null) return false;
        for (int i = 0; i < _selfColliders.Length; i++)
        {
            if (_selfColliders[i] == col) return true;
        }
        return false;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Gizmos
    // ────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Vector3 o = transform.position;
        float va = Application.isPlaying ? EffectiveVisionAngleDegrees : visionAngleDegrees;
        float vr = Application.isPlaying ? EffectiveVisionRange : visionRange;
        float half = va * 0.5f;
        Gizmos.DrawLine(o, o + (Quaternion.AngleAxis(half, Vector3.forward) * transform.up).normalized * vr);
        Gizmos.DrawLine(o, o + (Quaternion.AngleAxis(-half, Vector3.forward) * transform.up).normalized * vr);

        if (!debugDrawConeRays || _debugRayEnds == null || _debugRayHit == null)
            return;

        int count = Mathf.Min(_debugRayEnds.Length, _debugRayHit.Length);
        for (int i = 0; i < count; i++)
        {
            Gizmos.color = _debugRayHit[i] ? debugRayHitColor : debugRayNoHitColor;
            Gizmos.DrawLine(o, _debugRayEnds[i]);
            Gizmos.DrawWireSphere(_debugRayEnds[i], 0.03f);
        }
    }
}