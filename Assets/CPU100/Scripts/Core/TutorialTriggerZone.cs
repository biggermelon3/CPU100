using UnityEngine;

/// <summary>
/// One-shot invisible trigger volume for tutorial beats. Fires OnPlayerEntered the
/// first time the Player's rigidbody enters, then stays silent. TutorialManager
/// subscribes; the zone itself knows nothing about the tutorial flow.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialTriggerZone : MonoBehaviour
{
    public event System.Action OnPlayerEntered;

    bool fired;

    void Awake()
    {
        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (fired || !other.CompareTag("Player")) return;
        fired = true;
        OnPlayerEntered?.Invoke();
    }
}
