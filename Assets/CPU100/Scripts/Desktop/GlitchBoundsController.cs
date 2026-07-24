using UnityEngine;

// Four glitch/noise zones that encroach from the screen edges as CPU rises.
// Zones hurt the player on contact (hazard CPU load + knockback) and corrupt
// desktop icons they cover. This file also contains the runtime-only GlitchZone
// relay component (contract-sanctioned exception to one-class-per-file).
public class GlitchBoundsController : MonoBehaviour
{
    const float WorldHalfWidth = 9.6f;
    const float WorldHalfHeight = 5.4f;
    const float GrowthSpeed = 1.5f;       // world units per second (MoveTowards)
    const float FlickerInterval = 0.1f;   // ~10 Hz alpha flicker
    const float MinZoneScale = 0.01f;     // scale floor when a zone is hidden

    public CPUManager cpuManager;
    public PlayerController2D player;
    public GameStateManager gameState;
    public Transform glitchLeft;
    public Transform glitchRight;
    public Transform glitchTop;
    public Transform glitchBottom;
    public float encroachStartCpu = 30f;
    public float maxHorizontalEncroach = 3.2f;
    public float maxVerticalEncroach = 2.0f;
    public float hazardCpuPerSecond = 6f;
    public float pushImpulse = 5f;
    public float corruptionCheckInterval = 0.5f;

    public bool Frozen { get; set; }

    float currentH;                // smoothed horizontal encroach
    float currentV;                // smoothed vertical encroach
    bool playerTouching;           // set by GlitchZone relays, cleared each physics step
    bool hazardCleared;            // ensures SetHazardLoad(0) fires once when frozen
    float flickerTimer;
    float corruptionTimer;
    readonly SpriteRenderer[] zoneRenderers = new SpriteRenderer[4];

    void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();

        glitchLeft = ResolveZone(glitchLeft, "GlitchLeft");
        glitchRight = ResolveZone(glitchRight, "GlitchRight");
        glitchTop = ResolveZone(glitchTop, "GlitchTop");
        glitchBottom = ResolveZone(glitchBottom, "GlitchBottom");

        // Push direction = toward screen center along the zone axis.
        zoneRenderers[0] = SetupZone(glitchLeft, Vector2.right);
        zoneRenderers[1] = SetupZone(glitchRight, Vector2.left);
        zoneRenderers[2] = SetupZone(glitchTop, Vector2.down);
        zoneRenderers[3] = SetupZone(glitchBottom, Vector2.up);

        corruptionTimer = corruptionCheckInterval;
    }

    // Wired ref > own child by name > scene GO by name (builder parents zones
    // under DesktopWorld/Hazards) > create as own child.
    Transform ResolveZone(Transform wired, string zoneName)
    {
        if (wired != null) return wired;

        var child = transform.Find(zoneName);
        if (child != null) return child;

        var sceneGo = GameObject.Find(zoneName);
        if (sceneGo != null) return sceneGo.transform;

        var go = new GameObject(zoneName);
        var t = go.transform;
        t.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 400;
        sr.color = new Color(1f, 1f, 1f, 0.45f);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = Vector2.one;
        return t;
    }

    SpriteRenderer SetupZone(Transform zone, Vector2 pushDir)
    {
        if (zone == null) return null;

        var sr = zone.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = zone.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 400;
            sr.color = new Color(1f, 1f, 1f, 0.45f);
        }
        // Always re-assign: procedural sprites do not survive a scene save.
        sr.sprite = PlaceholderSpriteFactory.GetNoise();

        var box = zone.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = zone.gameObject.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
        }
        box.isTrigger = true;

        var relay = zone.GetComponent<GlitchZone>();
        if (relay == null) relay = zone.gameObject.AddComponent<GlitchZone>();
        relay.controller = this;
        relay.pushDirection = pushDir;

        return sr;
    }

    void FixedUpdate()
    {
        // Cleared before this step's trigger callbacks run; OnTriggerEnter2D/Stay2D
        // re-set it. Frames without a physics step keep the last known value, so
        // the hazard load does not flicker at high frame rates.
        playerTouching = false;
    }

    void Update()
    {
        bool active = !Frozen && (gameState == null || gameState.State == GameState.Playing);
        if (!active)
        {
            if (!hazardCleared)
            {
                if (cpuManager != null) cpuManager.SetHazardLoad(0f);
                hazardCleared = true;
            }
            return; // visuals stay static
        }
        hazardCleared = false;

        // Encroach target from CPU, smoothed growth.
        float cpu = cpuManager != null ? cpuManager.CurrentCpu : 0f;
        float range = Mathf.Max(1f, 100f - encroachStartCpu);
        float t = Mathf.Clamp01((cpu - encroachStartCpu) / range);
        currentH = Mathf.MoveTowards(currentH, Mathf.Lerp(0f, maxHorizontalEncroach, t), Time.deltaTime * GrowthSpeed);
        currentV = Mathf.MoveTowards(currentV, Mathf.Lerp(0f, maxVerticalEncroach, t), Time.deltaTime * GrowthSpeed);

        ApplyZoneGeometry();
        UpdateFlicker();

        bool hazardActive = playerTouching && (player == null || !player.ShieldActive);
        if (cpuManager != null) cpuManager.SetHazardLoad(hazardActive ? hazardCpuPerSecond : 0f);

        corruptionTimer -= Time.deltaTime;
        if (corruptionTimer <= 0f)
        {
            corruptionTimer = corruptionCheckInterval;
            CorruptionSweep();
        }
    }

    // Called by GlitchZone relays on player contact.
    public void PlayerTouchedZone(Vector2 pushDirection, bool isEnter)
    {
        if (Frozen) return;
        if (gameState != null && gameState.State != GameState.Playing) return;

        playerTouching = true;

        if (isEnter && player != null)
        {
            // Shield bounces harder (and suppresses the hazard load in Update).
            float multiplier = player.ShieldActive ? 2.5f : 1f;
            Vector2 dir = pushDirection.sqrMagnitude > 0.0001f ? pushDirection.normalized : Vector2.up;
            player.ApplyKnockback(dir * (pushImpulse * multiplier));
        }
    }

    // Sprites are 1x1 world units, so localScale is the zone's world size.
    void ApplyZoneGeometry()
    {
        float sx = Mathf.Max(currentH, MinZoneScale);
        float sy = Mathf.Max(currentV, MinZoneScale);

        if (glitchLeft != null)
        {
            glitchLeft.localScale = new Vector3(sx, WorldHalfHeight * 2f, 1f);
            glitchLeft.position = new Vector3(-WorldHalfWidth + currentH * 0.5f, 0f, 0f);
        }
        if (glitchRight != null)
        {
            glitchRight.localScale = new Vector3(sx, WorldHalfHeight * 2f, 1f);
            glitchRight.position = new Vector3(WorldHalfWidth - currentH * 0.5f, 0f, 0f);
        }
        if (glitchTop != null)
        {
            glitchTop.localScale = new Vector3(WorldHalfWidth * 2f, sy, 1f);
            glitchTop.position = new Vector3(0f, WorldHalfHeight - currentV * 0.5f, 0f);
        }
        if (glitchBottom != null)
        {
            glitchBottom.localScale = new Vector3(WorldHalfWidth * 2f, sy, 1f);
            glitchBottom.position = new Vector3(0f, -WorldHalfHeight + currentV * 0.5f, 0f);
        }
    }

    void UpdateFlicker()
    {
        flickerTimer -= Time.deltaTime;
        if (flickerTimer > 0f) return;
        flickerTimer = FlickerInterval;

        for (int i = 0; i < zoneRenderers.Length; i++)
        {
            var sr = zoneRenderers[i];
            if (sr == null) continue;
            var c = sr.color;
            c.a = Random.Range(0.35f, 0.6f);
            sr.color = c;
        }
    }

    void CorruptionSweep()
    {
        var all = DesktopIcon.All;
        if (all == null) return;
        for (int i = 0; i < all.Count; i++)
        {
            var icon = all[i];
            if (icon == null) continue;
            var state = icon.State;
            if (state == DesktopIconState.Deleted ||
                state == DesktopIconState.Dragging ||
                state == DesktopIconState.Corrupted) continue;

            Vector2 p = icon.transform.position;
            if (ZoneContains(glitchLeft, p) || ZoneContains(glitchRight, p) ||
                ZoneContains(glitchTop, p) || ZoneContains(glitchBottom, p))
            {
                icon.SetState(DesktopIconState.Corrupted); // sticky inside DesktopIcon
            }
        }
    }

    static bool ZoneContains(Transform zone, Vector2 p)
    {
        if (zone == null) return false;
        Vector3 scale = zone.lossyScale;
        // A zone parked at the scale floor is hidden, never corrupting.
        if (scale.x <= MinZoneScale * 2f || scale.y <= MinZoneScale * 2f) return false;
        Vector3 c = zone.position;
        return Mathf.Abs(p.x - c.x) <= scale.x * 0.5f &&
               Mathf.Abs(p.y - c.y) <= scale.y * 0.5f;
    }
}

// Runtime-added relay living on each zone GO (zones have no Rigidbody2D, so
// trigger messages arrive on the zone GO itself). Never saved into the scene.
public class GlitchZone : MonoBehaviour
{
    public GlitchBoundsController controller;
    public Vector2 pushDirection;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (controller != null && other.CompareTag("Player"))
            controller.PlayerTouchedZone(pushDirection, true);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (controller != null && other.CompareTag("Player"))
            controller.PlayerTouchedZone(pushDirection, false);
    }
}
