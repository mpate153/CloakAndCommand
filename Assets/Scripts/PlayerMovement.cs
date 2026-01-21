using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D myBody;
    private SpriteRenderer sr;
    private PlayerControls controls;

    [Header("Basic Movement")]
    [SerializeField]
    private float moveForce = 6f;
    [SerializeField]
    private float jumpForce = 6f;

    public float moveX;
    public bool isGrounded = true;
    public bool canMove = true;

    [Header("Double Jump")]
    public bool canDoubleJump = false;
    [SerializeField]
    private int extraJumps = 1;
    private int jumpsLeft;

    [Header("Time Slow")]
    public bool canTimeSlow = false;
    public float slowDuration = 2f;
    public float slowFactor = 0.5f;
    public float slowFactorPlayer = 1f;
    public bool isSlowing = false;
    private float slowTimer = 0f;
    private float normalGravity;

    [Header("Roll")]
    public bool canRoll = true;
    public float rollForce = 8f;
    public float rollDuration = 0.5f;
    private float rollTimer = 0.5f;
    public float rollCooldown = 0.5f;
    public bool isRolling = false;
    public float rollCooldownTimer = 0.4f;
    private CapsuleCollider2D collider;
    private Vector2 normalColliderSize;
    private Vector2 normalColliderOffset;
    public Vector2 rollColliderSize = new Vector2(0.5f, 0.75f);
    public Vector2 rollColliderOffset = new Vector2(0f, 0.45f);

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        controls = GetComponent<PlayerControls>();
        collider = GetComponent<CapsuleCollider2D>();
        normalColliderSize = collider.size;
        normalColliderOffset = collider.offset;
    }

    void Start()
    {
        jumpsLeft = extraJumps;
        normalGravity = myBody.gravityScale;
    }

    void Update()
    {
        PlayerJump();
        if (canMove)
        {
            PlayerMoveKeyboard();
        }

        // TIME SLOW
        if (canTimeSlow && !isSlowing && controls.fire3Pressed)
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
        if (canRoll && !isRolling && rollCooldownTimer <= 0f && controls.rollPressed && canMove)
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
            float direction = sr.flipX ? -1f : 1f;
            myBody.linearVelocity = new Vector2(direction * rollForce, myBody.linearVelocity.y);
        }
    }

    void PlayerMoveKeyboard()
    {
        if (isRolling) return;

        moveX = controls.horizontalInput;

        float horizontalVelocity = moveForce * moveX;
        if (isSlowing)
            horizontalVelocity *= slowFactorPlayer;
        myBody.linearVelocity = new Vector2(horizontalVelocity, myBody.linearVelocity.y);
    }

    void PlayerJump()
    {
        if (controls.jumpPressed)
        {
            if (isGrounded)
            {
                Jump();
                isGrounded = false;
            }
            else if (jumpsLeft > 0)
            {
                Jump();
                jumpsLeft--;
            }
        }
    }

    void Jump()
    {
        myBody.linearVelocity = new Vector2(myBody.linearVelocity.x, 0f);

        float currentJumpForce = isSlowing ? jumpForce * slowFactorPlayer : jumpForce;
        myBody.AddForce(currentJumpForce * Vector2.up, ForceMode2D.Impulse);
    }

    void StartTimeSlow()
    {
        isSlowing = true;
        slowTimer = slowDuration;
        moveForce /= (slowFactorPlayer);
        jumpForce /= (slowFactorPlayer);
        //myBody.gravityScale /= (slowFactorPlayer);

        Time.timeScale = slowFactor;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    void EndTimeSlow()
    {
        isSlowing = false;
        moveForce *= (slowFactorPlayer);
        jumpForce *= (slowFactorPlayer);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        //myBody.gravityScale = normalGravity;
    }

    void StartRoll()
    {
        isRolling = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        collider.size = rollColliderSize;
        collider.offset = rollColliderOffset;
        canMove = false;
    }

    void EndRoll()
    {
        isRolling = false;
        canMove = true;

        collider.size = normalColliderSize;
        collider.offset = normalColliderOffset;

        myBody.linearVelocity = new Vector2(0f, myBody.linearVelocity.y);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            jumpsLeft = canDoubleJump ? extraJumps : 0;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}