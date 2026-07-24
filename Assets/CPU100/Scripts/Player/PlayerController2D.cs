using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState { Idle, Moving, Jumping, Falling, InputBlocked, Dead, Won }

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpVelocity = 13f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float fallKillY = -7f;
    public float respawnCpuPenalty = 5f;
    public float airDashSpeed = 14f;
    public float airDashDuration = 0.18f;

    public InputInterferenceController interference;
    public CPUManager cpuManager;
    public GameStateManager gameState;
    public PlayerGroundChecker groundChecker;

    public PlayerState State { get; private set; } = PlayerState.Idle;
    public bool FacingRight { get; private set; } = true;
    public bool ShieldActive { get { return shieldTimer > 0f; } }
    public Vector3 SpawnPoint { get; set; }

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    // Input cached in Update, consumed in FixedUpdate.
    private float horizontalInput;
    private float jumpBufferCounter;
    private float coyoteCounter;

    private float dashTimer;
    private float dashDirection = 1f;
    private float shieldTimer;
    // While > 0, FixedUpdate must not stomp X velocity or knockback impulses never integrate.
    private float knockbackTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Sleeping bodies stop receiving OnTriggerStay2D, which silently disables
        // virus re-hits and glitch-zone hazard drain when the player stands still.
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (interference == null) interference = FindFirstObjectByType<InputInterferenceController>();
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
        if (groundChecker == null) groundChecker = GetComponentInChildren<PlayerGroundChecker>(true);
        if (groundChecker == null) groundChecker = FindFirstObjectByType<PlayerGroundChecker>();

        SpawnPoint = transform.position;
    }

    private void Update()
    {
        if (State == PlayerState.Dead || State == PlayerState.Won) return;

        float dt = Time.deltaTime;

        if (shieldTimer > 0f) shieldTimer -= dt;

        if (transform.position.y < fallKillY)
            Respawn();

        bool blocked = interference != null && interference.InputBlocked;
        bool reversed = interference != null && interference.InputReversed;

        float h = 0f;
        bool jumpPressed = false;
        if (!blocked)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                jumpPressed = kb.spaceKey.wasPressedThisFrame;
            }
            if (reversed) h = -h;
        }
        horizontalInput = h;

        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else if (jumpBufferCounter > 0f) jumpBufferCounter -= dt;

        bool grounded = IsGroundedNow();
        if (grounded) coyoteCounter = coyoteTime;
        else if (coyoteCounter > 0f) coyoteCounter -= dt;

        if (h > 0.01f) FacingRight = true;
        else if (h < -0.01f) FacingRight = false;
        if (spriteRenderer != null) spriteRenderer.flipX = !FacingRight;

        if (blocked)
            State = PlayerState.InputBlocked;
        else if (grounded)
            State = Mathf.Abs(h) > 0.01f ? PlayerState.Moving : PlayerState.Idle;
        else
            State = rb.linearVelocity.y > 0.1f ? PlayerState.Jumping : PlayerState.Falling;
    }

    private void FixedUpdate()
    {
        if (State == PlayerState.Dead || State == PlayerState.Won)
        {
            // Hold in place without touching the builder-configured rigidbody settings.
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(dashDirection * airDashSpeed, 0f);
            return;
        }

        if (knockbackTimer > 0f)
            knockbackTimer -= Time.fixedDeltaTime;
        else
            rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }
    }

    private bool IsGroundedNow()
    {
        return groundChecker != null && groundChecker.IsGrounded && rb.linearVelocity.y <= 0.1f;
    }

    public void Respawn()
    {
        transform.position = SpawnPoint;
        rb.linearVelocity = Vector2.zero;
        dashTimer = 0f;
        knockbackTimer = 0f;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        if (cpuManager != null) cpuManager.AddCpu(respawnCpuPenalty);
    }

    public void AirDash()
    {
        if (State == PlayerState.Dead || State == PlayerState.Won) return;
        dashDirection = FacingRight ? 1f : -1f;
        dashTimer = airDashDuration;
    }

    public void ActivateShield(float duration)
    {
        if (duration > shieldTimer) shieldTimer = duration;
    }

    public void ApplyKnockback(Vector2 impulse)
    {
        if (rb == null) return;
        // A dash would override the impulse in FixedUpdate; cancel it so knockback lands.
        dashTimer = 0f;
        knockbackTimer = 0.2f;
        rb.AddForce(impulse, ForceMode2D.Impulse);
    }

    public void SetDead()
    {
        State = PlayerState.Dead;
        HaltMotion();
    }

    public void SetWon()
    {
        State = PlayerState.Won;
        HaltMotion();
    }

    private void HaltMotion()
    {
        horizontalInput = 0f;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        dashTimer = 0f;
        knockbackTimer = 0f;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
}
