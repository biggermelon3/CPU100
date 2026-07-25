using UnityEngine;

[RequireComponent(typeof(Animator), typeof(PlayerController2D))]
public class PlayerAnimationController : MonoBehaviour
{
    public GlitchBoundsController glitchBounds;

    static readonly int IdleState = Animator.StringToHash("Idle");
    static readonly int WalkState = Animator.StringToHash("Walk");
    static readonly int JumpState = Animator.StringToHash("Jump");
    static readonly int PaperPlaneState = Animator.StringToHash("PaperPlane");
    static readonly int PoisonIdleState = Animator.StringToHash("PoisonIdle");
    static readonly int AbilityState = Animator.StringToHash("Ability");

    Animator animator;
    PlayerController2D player;
    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    int currentState;
    float abilityEndTime;

    void Awake()
    {
        animator = GetComponent<Animator>();
        player = GetComponent<PlayerController2D>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (glitchBounds == null)
            glitchBounds = FindFirstObjectByType<GlitchBoundsController>();
    }

    void OnEnable()
    {
        currentState = 0;
    }

    void Update()
    {
        if (animator == null || player == null)
            return;

        bool grounded = player.IsGrounded;
        bool standing = body == null || Mathf.Abs(body.linearVelocity.x) < 0.05f;

        int nextState;
        if (glitchBounds != null && glitchBounds.IsPlayerInHazard && grounded && standing)
            nextState = PoisonIdleState;
        else if (player.AbilityMovementLocked)
            nextState = PaperPlaneState;
        else if (!grounded)
            nextState = JumpState;
        else if (Time.time < abilityEndTime)
            nextState = AbilityState;
        else if (player.State == PlayerState.Moving)
            nextState = WalkState;
        else
            nextState = IdleState;

        if (nextState == currentState)
            return;

        animator.Play(nextState, 0, 0f);
        currentState = nextState;
    }

    public bool TryPlayAbility(float duration = 0.5f)
    {
        if (player == null || animator == null || !player.IsGrounded ||
            player.State != PlayerState.Idle ||
            (body != null && Mathf.Abs(body.linearVelocity.x) >= 0.05f))
            return false;

        abilityEndTime = Time.time + Mathf.Max(0f, duration);
        animator.Play(AbilityState, 0, 0f);
        currentState = AbilityState;
        return true;
    }

    void LateUpdate()
    {
        if (spriteRenderer == null || player == null)
            return;

        // All source frames face left, so flip the complete character consistently.
        spriteRenderer.flipX = player.FacingRight;
    }
}
