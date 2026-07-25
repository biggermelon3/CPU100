using UnityEngine;

/// <summary>
/// Horizontal glitch flicker on the player's Visual sprite while the CPU runs hot
/// (>= startCpu). Short bursts displace the sprite left/right with an RGB-split-ish
/// tint; burst rate and offset both scale with the CPU value. Runs in LateUpdate so
/// it wins over the Animator, and always restores the authored pose when calm.
/// </summary>
public class PlayerGlitchEffect : MonoBehaviour
{
    public CPUManager cpuManager;
    public Transform visual;              // fallback: child "Visual"
    public float startCpu = 50f;
    public float maxOffset = 0.14f;

    static readonly Color TintA = new Color(1f, 0.45f, 0.45f, 1f);   // red split
    static readonly Color TintB = new Color(0.45f, 0.95f, 1f, 1f);   // cyan split

    SpriteRenderer visualRenderer;
    Vector3 baseLocalPos;
    float nextBurstTime;
    float burstEndTime;
    float burstOffset;
    Color burstTint = Color.white;
    bool displaced;

    void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (visual == null)
        {
            Transform v = transform.Find("Visual");
            visual = v != null ? v : transform;
        }
        visualRenderer = visual != null ? visual.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (visual != null) baseLocalPos = visual.localPosition;
    }

    void LateUpdate()
    {
        if (visual == null) return;

        float t = cpuManager != null ? Mathf.InverseLerp(startCpu, 100f, cpuManager.CurrentCpu) : 0f;
        if (t <= 0f)
        {
            Restore();
            return;
        }

        float now = Time.time;
        if (now >= nextBurstTime)
        {
            float dir = Random.value < 0.5f ? -1f : 1f;
            burstOffset = dir * (0.03f + maxOffset * t) * Random.Range(0.6f, 1f);
            burstTint = Random.value < 0.6f ? (Random.value < 0.5f ? TintA : TintB) : Color.white;
            burstEndTime = now + Random.Range(0.04f, 0.1f);
            // Quiet gap between bursts shrinks as the CPU climbs (~0.7s -> ~0.05s).
            nextBurstTime = burstEndTime + Mathf.Lerp(0.7f, 0.05f, t) * Random.Range(0.5f, 1.5f);
        }

        if (now < burstEndTime)
        {
            visual.localPosition = baseLocalPos + new Vector3(burstOffset, 0f, 0f);
            if (visualRenderer != null) visualRenderer.color = burstTint;
            displaced = true;
        }
        else
        {
            Restore();
        }
    }

    void OnDisable()
    {
        Restore();
    }

    void Restore()
    {
        if (!displaced) return;
        displaced = false;
        if (visual != null) visual.localPosition = baseLocalPos;
        if (visualRenderer != null) visualRenderer.color = Color.white;
    }
}
