using UnityEngine;

/// <summary>
/// Melee: always plays swing on Fire1. Instant-kill only if an enemy is in range and no <see cref="EnemyAI"/> detects the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAttack : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("If true, uses Fire1. If false, set alternateButton to a custom Input Manager button name.")]
    [SerializeField] bool useFire1 = true;
    [SerializeField] string alternateButton = "Fire1";

    [Header("Hit")]
    [SerializeField] float hitRadius = 0.55f;
    [Tooltip("World offset from transform.position along facing direction.")]
    [SerializeField] float hitDistance = 0.35f;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] float lethalDamage = 9999f;

    [Header("Facing")]
    [Tooltip("Top-down: forward is usually transform.up after PlayerMovement rotation.")]
    [SerializeField] bool useTransformUpAsForward = true;

    static readonly Collider2D[] Hits = new Collider2D[24];

    PlayerMovement _movement;
    PlayerControls _controls;

    void Awake()
    {
        TryGetComponent(out _movement);
        TryGetComponent(out _controls);
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        bool pressed = useFire1
            ? _controls != null && _controls.fire1Pressed
            : Input.GetButtonDown(alternateButton);

        if (!pressed)
            return;

        if (_movement != null)
            _movement.PlayAttackAnimation();

        if (AnyEnemyDetectsPlayer())
            return;

        Vector2 forward = GetForward();
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector2.up;
        forward.Normalize();

        Vector2 origin = (Vector2)transform.position + forward * hitDistance;
        var hitFilter = new ContactFilter2D();
        hitFilter.SetLayerMask(hitMask);
        hitFilter.useTriggers = Physics2D.queriesHitTriggers;
        int n = Physics2D.OverlapCircle(origin, hitRadius, hitFilter, Hits);
        EnemyPatrol best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var col = Hits[i];
            if (col == null) continue;
            var patrol = col.GetComponentInParent<EnemyPatrol>();
            if (patrol == null) continue;
            float d = Vector2.SqrMagnitude((Vector2)patrol.transform.position - origin);
            if (d < bestD)
            {
                bestD = d;
                best = patrol;
            }
        }

        if (best != null)
            best.TakeDamage(lethalDamage);
    }

    static bool AnyEnemyDetectsPlayer()
    {
        var all = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var ai = all[i];
            if (ai != null && ai.isActiveAndEnabled && ai.IsPlayerCurrentlyDetected())
                return true;
        }

        return false;
    }

    Vector2 GetForward()
    {
        if (useTransformUpAsForward)
            return transform.up;

        if (_movement != null)
        {
            float mx = _movement.moveX;
            float my = _movement.moveY;
            var v = new Vector2(mx, my);
            if (v.sqrMagnitude > 1e-4f)
                return v.normalized;
        }

        return transform.up;
    }
}
