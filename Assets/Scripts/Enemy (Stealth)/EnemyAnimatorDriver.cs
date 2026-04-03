using UnityEngine;

/// <summary>
/// Drives enemy sprite animation using the same Animator parameters as the player:
/// <c>Speed</c> (float), <c>Rolling</c> (bool), <c>Attack</c> (trigger).
/// Kinematic enemies have no physics velocity — speed is derived from position delta.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAnimatorDriver : MonoBehaviour
{
    [Tooltip("Optional; defaults to Animator on this object.")]
    [SerializeField] private Animator animator;

    static readonly int AnimSpeed = Animator.StringToHash("Speed");
    static readonly int AnimRolling = Animator.StringToHash("Rolling");
    static readonly int AnimAttack = Animator.StringToHash("Attack");

    Rigidbody2D _body;
    Vector2 _lastPos;

    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        if (animator == null)
            TryGetComponent(out animator);
        _lastPos = _body.position;
    }

    void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        Vector2 p = _body.position;
        float speed = (p - _lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-5f);
        _lastPos = p;

        animator.SetFloat(AnimSpeed, speed);
        animator.SetBool(AnimRolling, false);
    }

    /// <summary>Call when this enemy performs a melee swing (same as <see cref="PlayerMovement.PlayAttackAnimation"/>).</summary>
    public void PlayAttackAnimation()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(AnimAttack);
    }
}
