using UnityEngine;
using UnityEngine.Serialization;

public enum EnemyKind
{
    /// <summary>Cone patrol; bulwarks on chase; sprinters off-screen after first Idle→Suspicious (2nd+).</summary>
    Watcher,
    /// <summary>Summoned: deploy → sweep (cone + sway) → chase → leave & despawn. Cone vision.</summary>
    Sprinter,
    /// <summary>Slow; omnidirectional LOS; usually dormant until watcher summons.</summary>
    Bulwark
}

/// <summary>
/// Per-enemy tuning for Watcher / Sprinter / Bulwark. Place next to <see cref="EnemyPatrol"/> / … / <see cref="EnemyAI"/>.
/// </summary>
[DefaultExecutionOrder(100)]
public class EnemyArchetype : MonoBehaviour
{
    [SerializeField] private EnemyKind kind = EnemyKind.Watcher;

    [Header("Movement multipliers (applied to EnemyMovement in Awake)")]
    [SerializeField] private float chaseSpeedMultiplier = 1f;
    [SerializeField] private float suspiciousTurnSpeedMultiplier = 1f;
    [SerializeField] private float searchSpeedMultiplier = 1f;

    [Header("Vision")]
    [Tooltip("Watcher & Sprinter: cone range/angle. Bulwark: omnidirectional max distance (no cone).")]
    [SerializeField] private float visionRangeMultiplier = 1f;
    [SerializeField] private float visionAngleMultiplier = 1f;

    [Header("Watcher — idle cone sway")]
    [SerializeField] private bool idleVisionConeSway = true;
    [SerializeField] private float idleSwayDegrees = 6f;
    [SerializeField] private float idleSwaySpeed = 1.1f;

    [Header("Watcher — sprinters (2nd+ Enter Chase: roll after first chase)")]
    [SerializeField] private GameObject sprinterPrefab;
    [Range(0f, 1f)]
    [Tooltip("Rolled each time the watcher enters Suspicious from Idle after the first such transition (lifetime).")]
    [SerializeField] private float sprinterReinforceChanceAfterFirstCycle = 0.45f;
    [SerializeField] [Min(1)] private int sprinterReinforceMinCount = 1;
    [SerializeField] [Min(1)] private int sprinterReinforceMaxCount = 3;
    [SerializeField] private float sprinterApproachRingMin = 1.1f;
    [SerializeField] private float sprinterApproachRingMax = 3.8f;
    [Tooltip("Summoned sprinters spawn around the detected player within this radius band (prefer off-screen).")]
    [SerializeField] private float sprinterSpawnNearPlayerMinRadius = 7f;
    [SerializeField] private float sprinterSpawnNearPlayerMaxRadius = 14f;
    [SerializeField] private float sprinterSweepOrbitRadius = 2.6f;
    [Tooltip("Extra world units beyond camera ortho bounds for spawn / despawn.")]
    [SerializeField] private float offscreenMarginWorld = 1.15f;

    [Header("Watcher — bulwarks (on Enter Chase)")]
    [SerializeField] private GameObject bulwarkPrefab;
    [FormerlySerializedAs("bulwarksOnReSpotAfterChase")]
    [SerializeField] [Min(0)] private int bulwarksOnEnterChase = 1;

    [Header("Sprinter — summoned sweep & chase")]
    [Tooltip("Off (default): vision cone matches body/sprite facing during sweep. On: extra sway is applied only to the cone mesh, which usually looks disconnected from the sprite.")]
    [SerializeField] private bool sprinterSweepMeshConeSway;
    [SerializeField] private float sprinterSweepConeSwayDegrees = 8f;
    [SerializeField] private float sprinterSweepConeSwaySpeed = 1.15f;
    [SerializeField] private float sprinterSweepAngularSpeed = 52f;
    [SerializeField] private float sprinterDeployArriveDistance = 0.42f;
    [Tooltip("How fast chase aim catches the player (stale target).")]
    [SerializeField] private float sprinterStaleChaseApproachPerSecond = 2.45f;
    [Tooltip("Lose LOS this long in sprinter chase before walking off-screen.")]
    [SerializeField] private float sprinterChaseLoseSightDuration = 0.4f;

    [Header("Sprinter / Bulwark — reinforcement")]
    [SerializeField] private bool dormantUntilSummoned = false;

    public EnemyKind Kind => kind;
    public bool IdleVisionConeSway => kind == EnemyKind.Watcher && idleVisionConeSway;
    public float IdleSwayDegrees => idleSwayDegrees;
    public float IdleSwaySpeed => idleSwaySpeed;
    public GameObject SprinterPrefab => sprinterPrefab;
    public float SprinterReinforceChanceAfterFirstCycle => sprinterReinforceChanceAfterFirstCycle;
    public int SprinterReinforceMinCount => sprinterReinforceMinCount;
    public int SprinterReinforceMaxCount => sprinterReinforceMaxCount;
    public float SprinterApproachRingMin => sprinterApproachRingMin;
    public float SprinterApproachRingMax => sprinterApproachRingMax;
    public float SprinterSweepOrbitRadius => sprinterSweepOrbitRadius;
    public float OffscreenMarginWorld => offscreenMarginWorld;
    public GameObject BulwarkPrefab => bulwarkPrefab;
    public int BulwarksOnEnterChase => bulwarksOnEnterChase;
    public bool SprinterSweepMeshConeSway => sprinterSweepMeshConeSway;
    public float SprinterSweepConeSwayDegrees => sprinterSweepConeSwayDegrees;
    public float SprinterSweepConeSwaySpeed => sprinterSweepConeSwaySpeed;
    public float SprinterSweepAngularSpeed => sprinterSweepAngularSpeed;
    public float SprinterDeployArriveDistance => sprinterDeployArriveDistance;
    public float SprinterStaleChaseApproachPerSecond => sprinterStaleChaseApproachPerSecond;
    public float SprinterChaseLoseSightDuration => sprinterChaseLoseSightDuration;

    /// <summary>Watcher: bulwarks in a ring when chase starts.</summary>
    public void SpawnWatcherBulwarksOnEnterChase(Transform caller)
    {
        if (caller == null || kind != EnemyKind.Watcher) return;
        float r = Mathf.Max(0.05f, sprinterApproachRingMax * 0.35f + 0.5f);
        SpawnRing(bulwarkPrefab, bulwarksOnEnterChase, caller.position, r * 1.2f);
    }

    /// <summary>Watcher: off-screen sprinters marching to investigation point (caller: on chase).</summary>
    public void SpawnWatcherSprinterReinforcements(Transform caller, Vector2 investigationCenter, Camera cam = null)
    {
        if (caller == null || kind != EnemyKind.Watcher || sprinterPrefab == null) return;
        int minC = Mathf.Min(sprinterReinforceMinCount, sprinterReinforceMaxCount);
        int maxC = Mathf.Max(sprinterReinforceMinCount, sprinterReinforceMaxCount);
        int n = Random.Range(minC, maxC + 1);
        for (int i = 0; i < n; i++)
        {
            Vector2 spawn = OffScreenSpawn2D.RandomNearPointOutsideView(
                investigationCenter,
                cam,
                sprinterSpawnNearPlayerMinRadius,
                sprinterSpawnNearPlayerMaxRadius,
                offscreenMarginWorld);
            var spawned = Object.Instantiate((Object)sprinterPrefab, (Vector3)spawn, Quaternion.identity);
            var go = spawned as GameObject;
            if (go == null)
            {
                Debug.LogError($"{nameof(EnemyArchetype)}: sprinter prefab did not instantiate as GameObject.", this);
                continue;
            }
            var dep = go.AddComponent<EnemySprinterDeployment>();
            dep.Setup(
                investigationCenter,
                sprinterApproachRingMin,
                sprinterApproachRingMax,
                sprinterSweepOrbitRadius,
                offscreenMarginWorld);
            SaveSummonedEnemies.Tag(go, sprinterPrefab);
            go.GetComponent<EnemyArchetype>()?.WakeFromWatcherSummon();
        }
    }

    static void SpawnRing(GameObject prefab, int count, Vector2 center, float radius)
    {
        if (prefab == null || count <= 0) return;
        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
            float rad = radius * Random.Range(0.75f, 1.05f);
            Vector2 offset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            var spawned = Object.Instantiate((Object)prefab, (Vector3)(center + offset), Quaternion.identity);
            var go = spawned as GameObject;
            if (go == null)
            {
                Debug.LogError($"{nameof(EnemyArchetype)}: ring prefab did not instantiate as GameObject.");
                continue;
            }
            SaveSummonedEnemies.Tag(go, prefab);
            go.GetComponent<EnemyArchetype>()?.WakeFromWatcherSummon();
        }
    }

    public void WakeFromWatcherSummon()
    {
        if (!_dormantRuntime) return;
        _dormantRuntime = false;
        SetCoreBehavioursEnabled(true);
    }

    private bool _dormantRuntime;

    private void Reset()
    {
        ApplyPreset(kind);
    }

    [ContextMenu("Preset: Watcher")]
    private void PresetWatcher() => ApplyPreset(EnemyKind.Watcher);

    [ContextMenu("Preset: Sprinter")]
    private void PresetSprinter() => ApplyPreset(EnemyKind.Sprinter);

    [ContextMenu("Preset: Bulwark")]
    private void PresetBulwark() => ApplyPreset(EnemyKind.Bulwark);

    private void ApplyPreset(EnemyKind k)
    {
        kind = k;
        switch (k)
        {
            case EnemyKind.Watcher:
                chaseSpeedMultiplier = 0.72f;
                suspiciousTurnSpeedMultiplier = 1f;
                searchSpeedMultiplier = 1f;
                visionRangeMultiplier = 1.05f;
                visionAngleMultiplier = 1f;
                idleVisionConeSway = true;
                sprinterReinforceChanceAfterFirstCycle = 0.45f;
                sprinterReinforceMinCount = 1;
                sprinterReinforceMaxCount = 3;
                bulwarksOnEnterChase = 1;
                dormantUntilSummoned = false;
                break;
            case EnemyKind.Sprinter:
                chaseSpeedMultiplier = 1.55f;
                suspiciousTurnSpeedMultiplier = 1.15f;
                searchSpeedMultiplier = 1.1f;
                visionRangeMultiplier = 0.78f;
                visionAngleMultiplier = 0.88f;
                idleVisionConeSway = false;
                bulwarksOnEnterChase = 0;
                sprinterStaleChaseApproachPerSecond = 2.45f;
                sprinterChaseLoseSightDuration = 0.4f;
                dormantUntilSummoned = true;
                break;
            case EnemyKind.Bulwark:
                chaseSpeedMultiplier = 0.38f;
                suspiciousTurnSpeedMultiplier = 0.55f;
                searchSpeedMultiplier = 0.52f;
                visionRangeMultiplier = 1f;
                visionAngleMultiplier = 1f;
                idleVisionConeSway = false;
                bulwarksOnEnterChase = 0;
                dormantUntilSummoned = true;
                break;
        }
    }

    private void Awake()
    {
        var move = GetComponent<EnemyMovement>();
        if (move != null)
        {
            move.chaseSpeed *= chaseSpeedMultiplier;
            move.suspiciousTurnSpeed *= suspiciousTurnSpeedMultiplier;
            move.searchSpeedMultiplier *= searchSpeedMultiplier;
        }

        _dormantRuntime = dormantUntilSummoned && (kind == EnemyKind.Sprinter || kind == EnemyKind.Bulwark);
        if (_dormantRuntime)
            SetCoreBehavioursEnabled(false);
    }

    private void SetCoreBehavioursEnabled(bool on)
    {
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = on;
        var patrol = GetComponent<EnemyPatrol>();
        if (patrol != null) patrol.enabled = on;
    }

    private void Start()
    {
        var vision = GetComponent<EnemyVision>();
        if (vision != null)
        {
            vision.SetDetectionMultipliers(visionRangeMultiplier, visionAngleMultiplier);
            if (kind == EnemyKind.Bulwark)
            {
                vision.SetConeVisionEnabled(false);
                vision.SetConeVisualEnabled(false);
            }
            else
            {
                vision.SetConeVisionEnabled(true);
                vision.SetConeVisualEnabled(true);
            }
        }
    }
}
