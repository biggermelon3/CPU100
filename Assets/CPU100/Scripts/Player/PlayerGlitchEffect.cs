using UnityEngine;

/// <summary>
/// Horizontal glitch flicker on the player's Visual sprite while the CPU runs hot
/// (>= startCpu). Short bursts displace the sprite left/right with an RGB-split-ish
/// tint and coloured afterimages; burst rate and offset both scale with the CPU
/// value. Runs in LateUpdate so it wins over the Animator, and always restores the
/// authored pose when calm.
/// </summary>
public class PlayerGlitchEffect : MonoBehaviour
{
    public CPUManager cpuManager;
    public Transform visual;              // fallback: child "Visual"
    public float startCpu = 50f;
    public float maxOffset = 0.14f;

    static readonly Color TintA = new Color(1f, 0.45f, 0.45f, 1f);   // red split
    static readonly Color TintB = new Color(0.45f, 0.95f, 1f, 1f);   // cyan split
    static readonly Color[] GhostColours =
    {
        new Color(1f, 0.05f, 0.28f, 1f),
        new Color(0f, 0.95f, 1f, 1f),
        new Color(0.95f, 0.05f, 1f, 1f),
        new Color(0.2f, 1f, 0.25f, 1f),
        new Color(1f, 0.85f, 0.05f, 1f)
    };

    SpriteRenderer visualRenderer;
    readonly SpriteRenderer[] ghostRenderers = new SpriteRenderer[2];
    readonly Transform[] ghostTransforms = new Transform[2];
    Vector3 baseLocalPos;
    Color baseColour = Color.white;
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
        if (visualRenderer != null)
        {
            baseColour = visualRenderer.color;
            CreateGhosts();
        }
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
            ConfigureGhosts(t);
            // Quiet gap between bursts shrinks as the CPU climbs (~0.7s -> ~0.05s).
            nextBurstTime = burstEndTime + Mathf.Lerp(0.7f, 0.05f, t) * Random.Range(0.5f, 1.5f);
        }

        if (now < burstEndTime)
        {
            visual.localPosition = baseLocalPos + new Vector3(burstOffset, 0f, 0f);
            if (visualRenderer != null) visualRenderer.color = burstTint;
            SyncGhostFrames();
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

    void CreateGhosts()
    {
        Transform ghostParent = visual.parent != null ? visual.parent : transform;
        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            GameObject ghost = new GameObject("CPU Glitch Afterimage " + (i + 1));
            Transform ghostTransform = ghost.transform;
            ghostTransform.SetParent(ghostParent, false);
            ghostTransform.localPosition = baseLocalPos;
            ghostTransform.localRotation = visual.localRotation;
            ghostTransform.localScale = visual.localScale;

            SpriteRenderer renderer = ghost.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = visualRenderer.sharedMaterial;
            renderer.sortingLayerID = visualRenderer.sortingLayerID;
            renderer.sortingOrder = visualRenderer.sortingOrder - 1 - i;
            renderer.enabled = false;

            ghostTransforms[i] = ghostTransform;
            ghostRenderers[i] = renderer;
        }
    }

    void ConfigureGhosts(float intensity)
    {
        if (visualRenderer == null) return;

        // Usually one random side; increasingly show both sides as CPU approaches 100%.
        bool showBoth = Random.value < Mathf.Lerp(0.2f, 0.75f, intensity);
        float firstSide = Random.value < 0.5f ? -1f : 1f;
        float distance = Mathf.Lerp(0.07f, 0.25f, intensity);

        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            bool active = i == 0 || showBoth;
            SpriteRenderer ghost = ghostRenderers[i];
            if (ghost == null) continue;
            ghost.enabled = active;
            if (!active) continue;

            float side = i == 0 ? firstSide : -firstSide;
            float offset = distance * Random.Range(0.65f, 1.15f);
            ghostTransforms[i].localPosition =
                baseLocalPos + new Vector3(side * offset, Random.Range(-0.025f, 0.025f), 0f);

            Color colour = GhostColours[Random.Range(0, GhostColours.Length)];
            colour.a = Mathf.Lerp(0.35f, 0.7f, intensity) * Random.Range(0.8f, 1f);
            ghost.color = colour;
        }
    }

    void SyncGhostFrames()
    {
        if (visualRenderer == null) return;
        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            SpriteRenderer ghost = ghostRenderers[i];
            if (ghost == null || !ghost.enabled) continue;
            ghost.sprite = visualRenderer.sprite;
            ghost.flipX = visualRenderer.flipX;
            ghost.flipY = visualRenderer.flipY;
        }
    }

    void Restore()
    {
        if (displaced)
        {
            displaced = false;
            if (visual != null) visual.localPosition = baseLocalPos;
            if (visualRenderer != null) visualRenderer.color = baseColour;
        }

        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            if (ghostRenderers[i] != null) ghostRenderers[i].enabled = false;
        }
    }
}
