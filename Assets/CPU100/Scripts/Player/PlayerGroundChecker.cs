using UnityEngine;

public class PlayerGroundChecker : MonoBehaviour
{
    public Vector2 boxSize = new Vector2(0.5f, 0.12f);
    public LayerMask groundMask;

    public bool IsGrounded { get; private set; }

    private readonly Collider2D[] hits = new Collider2D[8];
    private Collider2D[] ownColliders;
    private ContactFilter2D filter;

    private void Awake()
    {
        if (groundMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Platform");
            groundMask = 1 << (layer >= 0 ? layer : 0);
        }

        filter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = groundMask
        };

        // Colliders on this GO and its parents (the player) never count as ground.
        ownColliders = GetComponentsInParent<Collider2D>(true);
        if (ownColliders == null) ownColliders = new Collider2D[0];
    }

    private void FixedUpdate()
    {
        int count = Physics2D.OverlapBox((Vector2)transform.position, boxSize, 0f, filter, hits);
        bool grounded = false;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = hits[i];
            if (c == null || IsOwnCollider(c)) continue;
            grounded = true;
            break;
        }
        IsGrounded = grounded;
    }

    private bool IsOwnCollider(Collider2D c)
    {
        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == c) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, boxSize);
    }
}
