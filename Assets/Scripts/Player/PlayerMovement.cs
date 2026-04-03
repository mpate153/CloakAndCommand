using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D myBody;
    private SpriteRenderer sr;

    [Header("Basic Movement")]
    [SerializeField]
    private float moveForce = 6f;

    public float moveX;
    public float moveY;
    public bool isGrounded = true;
    public bool canMove = true;

    [Header("Relative Movement")]
    [SerializeField] private float rotationSpeed = 10f; // Degrees per second

    [Header("Animation")]
    [Tooltip("Optional. Assign Player.controller (same GameObject as Sprite Renderer). Drives Speed / Rolling / Attack.")]
    [SerializeField] private Animator animator;
    static readonly int AnimSpeed = Animator.StringToHash("Speed");
    static readonly int AnimRolling = Animator.StringToHash("Rolling");
    static readonly int AnimAttack = Animator.StringToHash("Attack");

    [Header("Time Slow")]
    public bool canTimeSlow = false;
    public float slowDuration = 2f;
    public float slowFactor = 0.5f;
    public float slowFactorPlayer = 1f;
    public bool isSlowing = false;
    private float slowTimer = 0f;

    [Header("Roll")]
    public bool canRoll = true;
    public float rollForce = 15f;
    public float rollDuration = 0.25f;
    private float rollTimer = 0.3f;
    public float rollCooldown = 0.5f;
    public bool isRolling = false;
    public float rollCooldownTimer = 0.4f;
    private CapsuleCollider2D collider;
    private Vector2 normalColliderSize;
    private Vector2 normalColliderOffset;
    public Vector2 rollColliderSize = new Vector2(0f, 0f);
    public Vector2 rollColliderOffset = new Vector2(0f, 0f);
    private Vector2 rollDirection;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        collider = GetComponent<CapsuleCollider2D>();
        normalColliderSize = collider.size;
        normalColliderOffset = collider.offset;

        myBody.gravityScale = 0f;
        if (animator == null)
            TryGetComponent(out animator);
    }

    /// <summary>Call from combat when you add an attack; fires the <c>Attack</c> trigger on the Animator.</summary>
    public void PlayAttackAnimation()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(AnimAttack);
    }

    void Start()
    {

    }

    void Update()
    {
        if (canMove && !isRolling)
            PlayerMoveKeyboard();

        if (sr != null && animator == null && moveX != 0 && !isRolling)
            sr.flipX = moveX < 0;

        ApplyRotation();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat(AnimSpeed, myBody.linearVelocity.magnitude);
            animator.SetBool(AnimRolling, isRolling);
        }

        // TIME SLOW
        if (canTimeSlow && !isSlowing && PlayerControls.Instance != null && PlayerControls.Instance.fire3Pressed)
        {
            StartTimeSlow();
        }

        if (isSlowing)
        {
            slowTimer -= Time.unscaledDeltaTime;
            if (slowTimer <= 0f)
                EndTimeSlow();
        }

        //myBody.gravityScale = isSlowing ? normalGravity * slowFactorPlayer : normalGravity;

        //ROLL
        if (canRoll && !isRolling && rollCooldownTimer <= 0f && PlayerControls.Instance != null && PlayerControls.Instance.rollPressed && canMove)
        {
            StartRoll();
        }

        // Update roll timer
        if (isRolling)
        {
            rollTimer -= Time.unscaledDeltaTime;
            if (rollTimer <= 0f)
                EndRoll();
        }

        if (rollCooldownTimer > 0f)
            rollCooldownTimer -= Time.unscaledDeltaTime;
    }

    private void FixedUpdate()
    {
        if (isRolling)
        {
            myBody.linearVelocity = rollDirection * rollForce;
        }
    }

    void PlayerMoveKeyboard()
    {
        if (isRolling) return;

        moveX = PlayerControls.Instance != null ? PlayerControls.Instance.horizontalInput : 0f;
        moveY = Input.GetAxisRaw("Vertical");
        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;
        float velocity = moveForce;

        if (isSlowing)
            velocity *= slowFactorPlayer;

        myBody.linearVelocity = moveDirection * velocity;
    }

    //void PlayerMoveKeyboard()
    //{
    //    if (isRolling) return;

    //    // 1. Get Inputs
    //    moveX = controls.horizontalInput; // A/D keys
    //    moveY = Input.GetAxisRaw("Vertical"); // W/S keys

    //    // 2. Handle Rotation (A/D)
    //    // We subtract moveX because positive D usually means clockwise rotation
    //    float rotationAmount = -moveX * rotationSpeed * Time.unscaledDeltaTime;
    //    transform.Rotate(0, 0, rotationAmount);

    //    // 3. Handle Forward/Backward Movement (W/S)
    //    // Most Unity sprites face 'Right' by default. If yours faces 'Up', use transform.up
    //    Vector2 forwardDirection = transform.right;

    //    float velocity = moveForce;
    //    if (isSlowing) velocity *= slowFactorPlayer;

    //    // Move along the sprite's current facing direction
    //    myBody.linearVelocity = forwardDirection * (moveY * velocity);
    //}

    void ApplyRotation()
    {
        if (moveX != 0 || moveY != 0)
        {
            // Atan2(y,x) is 0 for world +X; most top-down sprites face +Y at identity — offset fixes 90° mismatch.
            float targetAngle = Mathf.Atan2(moveY, moveX) * Mathf.Rad2Deg - 90f;
            float currentAngle = transform.eulerAngles.z;
            float smoothAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, smoothAngle);
        }
    }

    void StartTimeSlow()
    {
        isSlowing = true;
        slowTimer = slowDuration;
        moveForce /= (slowFactorPlayer);
        //myBody.gravityScale /= (slowFactorPlayer);

        Time.timeScale = slowFactor;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    void EndTimeSlow()
    {
        isSlowing = false;
        moveForce *= (slowFactorPlayer);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        //myBody.gravityScale = normalGravity;
    }

    void StartRoll()
    {
        isRolling = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        // Forward is local +Y (sprite “up”); matches ApplyRotation after the -90° offset from Atan2.
        rollDirection = ((Vector2)transform.up).normalized;

        collider.size = rollColliderSize;
        collider.offset = rollColliderOffset;
        canMove = false;
    }

    //void StartRoll()
    //{
    //    isRolling = true;
    //    rollTimer = rollDuration;
    //    rollCooldownTimer = rollCooldown;

    //    // Get the current vertical input to see if rolling forward or backward
    //    float v = Input.GetAxisRaw("Vertical");

    //    // Determine roll direction based on sprite's facing
    //    // Again, use transform.up if your sprite's 'front' is the top
    //    Vector2 facingDir = transform.right;

    //    // Roll backward if holding S, otherwise roll forward
    //    rollDirection = (v < 0) ? -facingDir : facingDir;

    //    collider.size = rollColliderSize;
    //    collider.offset = rollColliderOffset;
    //    canMove = false;
    //}

    void EndRoll()
    {
        isRolling = false;
        canMove = true;

        collider.size = normalColliderSize;
        collider.offset = normalColliderOffset;

        //myBody.linearVelocity = new Vector2(0f, myBody.linearVelocity.y);
        myBody.linearVelocity = Vector2.zero;
    }

    //private void OnCollisionEnter2D(Collision2D collision)
    //{
    //    if (collision.gameObject.CompareTag("Ground"))
    //    {
    //        isGrounded = true;
    //        jumpsLeft = canDoubleJump ? extraJumps : 0;
    //    }
    //}

    //private void OnCollisionExit2D(Collision2D collision)
    //{
    //    if (collision.gameObject.CompareTag("Ground"))
    //    {
    //        isGrounded = false;
    //    }
    //}
}