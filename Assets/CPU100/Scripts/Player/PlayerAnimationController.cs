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

    Animator animator;
    PlayerController2D player;
    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    int currentState;

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
        else if (player.State == PlayerState.Moving)
            nextState = WalkState;
        else
            nextState = IdleState;

        if (nextState == currentState)
            return;

        animator.Play(nextState, 0, 0f);
        currentState = nextState;
    }

    void LateUpdate()
    {
        if (spriteRenderer == null || player == null)
            return;

        // All source frames face left, so flip the complete character consistently.
        spriteRenderer.flipX = player.FacingRight;
    }
}
