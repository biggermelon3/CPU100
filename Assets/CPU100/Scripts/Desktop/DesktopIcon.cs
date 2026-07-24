using System.Collections;
using UnityEngine;

/// <summary>
/// The central class of the game: every platform, file, software icon, virus and the
/// winning shortcut is a DesktopIcon. All visual children are created idempotently by
/// EnsureVisuals(), which is also called by the editor scene builder in edit mode on a
/// fresh GameObject — so that method uses only plain APIs, never destroys anything and
/// never duplicates children or components.
/// </summary>
public class DesktopIcon : MonoBehaviour
{
    /// <summary>Registry of all enabled icons (added OnEnable, removed OnDisable).</summary>
    public static readonly System.Collections.Generic.List<DesktopIcon> All =
        new System.Collections.Generic.List<DesktopIcon>();

    // ---------------- Config (set by the scene builder or CreateRuntimeIcon) ----------------
    public string iconName = "New Icon";
    public Sprite iconSprite;                 // null -> PlaceholderSpriteFactory.GetIconSprite(iconType)
    public DesktopIconType iconType;
    public bool isPlatform = true;
    public bool canDrag;
    public bool canInstall;
    public bool isShortcut;
    public SoftwareData softwareData;
    public float iconScale = 1.3f;

    // Virus tuning (used when iconType == Virus).
    public float virusCpuSpike = 10f;
    public float virusKnockback = 8f;
    public float virusDamageCooldown = 1f;

    // Cross-component refs (contract §1: public field + one-time Awake fallback).
    public CPUManager cpuManager;
    public GameStateManager gameStateManager;
    public PlayerController2D player;

    // ---------------- Runtime state ----------------
    private DesktopIconState state = DesktopIconState.Normal;
    private Vector3 homePosition;

    // Cached visuals / colliders (filled by EnsureVisuals, which Awake always calls).
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer shortcutArrowRenderer;
    private SpriteRenderer selectionOutlineRenderer;
    private SpriteRenderer runningGlowRenderer;
    private SpriteRenderer corruptionOverlayRenderer;
    private TextMesh labelMesh;
    private BoxCollider2D platformCollider;   // the ONLY collider disabled while dragging

    private float nextVirusDamageTime;
    private bool winTriggered;
    private Coroutine flashRoutine;
    private Coroutine expireRoutine;

    private static readonly Color CorruptedBodyColor = new Color(0.533f, 0.533f, 0.533f, 1f);   // #888888
    private static readonly Color RunningGlowColor = new Color(0.435f, 0.890f, 0.627f, 0.5f);   // #6FE3A0
    private static readonly Color CorruptionOverlayTint = new Color(0.55f, 0.55f, 0.55f, 0.75f);
    // WaitForSeconds only stores a duration (wake-up time is tracked per coroutine),
    // so sharing cached instances across coroutines is safe and allocation-free.
    private static readonly WaitForSeconds FlashWait = new WaitForSeconds(0.08f);
    private static readonly WaitForSeconds BlinkWait = new WaitForSeconds(0.125f); // ~8 Hz toggle

    public DesktopIconState State => state;
    public bool IsCorrupted => state == DesktopIconState.Corrupted;
    public Vector3 HomePosition => homePosition;

    // ---------------- Unity lifecycle ----------------

    private void Awake()
    {
        homePosition = transform.position;

        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (gameStateManager == null) gameStateManager = FindFirstObjectByType<GameStateManager>();
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();

        // Re-runs the full visual setup. Crucially this re-assigns every sprite:
        // procedurally generated sprites do NOT survive a scene save, so icons built in
        // edit mode wake up with dead sprite references that must be replaced here.
        // (iconSprite wins if the user assigned real art — it is a persistent asset.)
        EnsureVisuals();
        ApplyStateVisuals();
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    // ---------------- Visual construction (edit mode AND play mode, idempotent) ----------------

    public void EnsureVisuals()
    {
        transform.localScale = Vector3.one * iconScale;

        int platformLayer = LayerMask.NameToLayer("Platform");
        gameObject.layer = platformLayer >= 0 ? platformLayer : 0;

        // Body.
        bodyRenderer = GetOrCreateChildRenderer("Body", new Vector3(0f, 0.1f, 0f), 10);
        bodyRenderer.sprite = ResolveBodySprite();

        // Shortcut arrow overlay.
        shortcutArrowRenderer = GetOrCreateChildRenderer("ShortcutArrow", new Vector3(-0.32f, -0.18f, 0f), 11);
        shortcutArrowRenderer.transform.localScale = Vector3.one * 0.45f;
        shortcutArrowRenderer.sprite = PlaceholderSpriteFactory.GetShortcutArrow();
        shortcutArrowRenderer.gameObject.SetActive(isShortcut);

        // File-name label (legacy TextMesh, never TMP).
        Transform labelTransform = GetOrCreateChild("Label");
        labelTransform.localPosition = new Vector3(0f, -0.62f, 0f);
        labelMesh = GetOrAdd<TextMesh>(labelTransform.gameObject);
        labelMesh.text = iconName;
        labelMesh.anchor = TextAnchor.UpperCenter;
        labelMesh.alignment = TextAlignment.Center;
        labelMesh.characterSize = 0.07f;
        labelMesh.fontSize = 64;
        labelMesh.color = Color.white;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            labelMesh.font = font;
            MeshRenderer labelRenderer = labelTransform.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                // sharedMaterial: renderer.material would instantiate a copy and leak in edit mode.
                labelRenderer.sharedMaterial = font.material;
                labelRenderer.sortingOrder = 11;
            }
        }

        // Selection outline (behind body).
        selectionOutlineRenderer = GetOrCreateChildRenderer("SelectionOutline", new Vector3(0f, 0.1f, 0f), 9);
        selectionOutlineRenderer.transform.localScale = Vector3.one * 1.25f;
        selectionOutlineRenderer.sprite = PlaceholderSpriteFactory.GetSolid(Color.white);
        selectionOutlineRenderer.color = new Color(1f, 1f, 1f, 0.35f);
        selectionOutlineRenderer.gameObject.SetActive(state == DesktopIconState.Selected);

        // Running glow (furthest back).
        runningGlowRenderer = GetOrCreateChildRenderer("RunningGlow", new Vector3(0f, 0.1f, 0f), 8);
        runningGlowRenderer.transform.localScale = Vector3.one * 1.35f;
        runningGlowRenderer.sprite = PlaceholderSpriteFactory.GetSolid(Color.white);
        runningGlowRenderer.color = RunningGlowColor;
        runningGlowRenderer.gameObject.SetActive(state == DesktopIconState.Running);

        // Corruption overlay (in front of body).
        corruptionOverlayRenderer = GetOrCreateChildRenderer("CorruptionOverlay", new Vector3(0f, 0.1f, 0f), 12);
        corruptionOverlayRenderer.transform.localScale = Vector3.one * 1.1f;
        corruptionOverlayRenderer.sprite = PlaceholderSpriteFactory.GetNoise();
        corruptionOverlayRenderer.color = CorruptionOverlayTint;
        corruptionOverlayRenderer.gameObject.SetActive(state == DesktopIconState.Corrupted);

        // One-way platform collider + effector on the root.
        if (isPlatform)
        {
            platformCollider = FindRootBoxCollider(trigger: false);
            if (platformCollider == null)
            {
                platformCollider = gameObject.AddComponent<BoxCollider2D>();
                platformCollider.isTrigger = false;
            }
            platformCollider.size = new Vector2(1.1f, 0.95f);
            platformCollider.offset = new Vector2(0f, 0.05f);
            platformCollider.usedByEffector = true;

            PlatformEffector2D effector = GetOrAdd<PlatformEffector2D>(gameObject);
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
        }
        else
        {
            platformCollider = FindRootBoxCollider(trigger: false); // keep cache valid if config changed
        }

        // Trigger colliders live ON THE ROOT (alongside the platform collider), NOT on
        // child GameObjects: icons have no Rigidbody2D, so a static child collider would
        // deliver its OnTrigger* messages to the child GO where no component listens.
        // On the root they arrive directly on this component.
        if (iconType == DesktopIconType.Virus)
        {
            CircleCollider2D damageTrigger = GetOrAdd<CircleCollider2D>(gameObject);
            damageTrigger.isTrigger = true;
            damageTrigger.radius = 0.75f;
        }
        else if (iconType == DesktopIconType.Accelerator)
        {
            BoxCollider2D winTrigger = FindRootBoxCollider(trigger: true);
            if (winTrigger == null)
            {
                winTrigger = gameObject.AddComponent<BoxCollider2D>();
                winTrigger.isTrigger = true;
            }
            winTrigger.size = new Vector2(1.3f, 1.2f);
        }
    }

    private Sprite ResolveBodySprite()
    {
        // Explicit null check (not ??): destroyed/not-saved sprite references must fall
        // back to the factory sprite.
        return iconSprite != null ? iconSprite : PlaceholderSpriteFactory.GetIconSprite(iconType);
    }

    private SpriteRenderer GetOrCreateChildRenderer(string childName, Vector3 localPos, int sortingOrder)
    {
        Transform child = GetOrCreateChild(childName);
        child.localPosition = localPos;
        SpriteRenderer sr = GetOrAdd<SpriteRenderer>(child.gameObject);
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    private Transform GetOrCreateChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject go = new GameObject(childName);
            child = go.transform;
            child.SetParent(transform, false);
        }
        return child;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null) component = go.AddComponent<T>();
        return component;
    }

    /// <summary>Finds a BoxCollider2D on the root matching the trigger flag (the root can
    /// legitimately carry two: the platform collider and the Accelerator win trigger).</summary>
    private BoxCollider2D FindRootBoxCollider(bool trigger)
    {
        BoxCollider2D[] boxes = GetComponents<BoxCollider2D>();
        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i].isTrigger == trigger) return boxes[i];
        }
        return null;
    }

    // ---------------- State machine ----------------

    public void SetState(DesktopIconState s)
    {
        if (state == s) return;
        // Corrupted is STICKY: the only allowed transition out is Deleted.
        if (state == DesktopIconState.Corrupted && s != DesktopIconState.Deleted) return;

        state = s;
        ApplyStateVisuals();

        if (state == DesktopIconState.Deleted) gameObject.SetActive(false);
    }

    private void ApplyStateVisuals()
    {
        if (selectionOutlineRenderer != null)
            selectionOutlineRenderer.gameObject.SetActive(state == DesktopIconState.Selected);
        if (runningGlowRenderer != null)
            runningGlowRenderer.gameObject.SetActive(state == DesktopIconState.Running);
        if (corruptionOverlayRenderer != null)
            corruptionOverlayRenderer.gameObject.SetActive(state == DesktopIconState.Corrupted);
        if (bodyRenderer != null)
            bodyRenderer.color = CurrentBodyColor();
    }

    private Color CurrentBodyColor()
    {
        Color c = state == DesktopIconState.Corrupted ? CorruptedBodyColor : Color.white;
        if (state == DesktopIconState.Dragging) c.a = 0.6f;
        return c;
    }

    // ---------------- Dragging (driven by CursorInteractor) ----------------

    public void BeginDrag()
    {
        // CursorInteractor already filters, but stay safe against direct calls.
        if (IsCorrupted || state == DesktopIconState.Deleted || state == DesktopIconState.Installed) return;

        SetState(DesktopIconState.Dragging);
        // Disable ONLY the platform collider so the icon stops being a wall/floor while
        // carried. Trigger colliders of other icons stay untouched.
        if (platformCollider != null) platformCollider.enabled = false;
    }

    public void UpdateDrag(Vector2 worldPos)
    {
        if (state != DesktopIconState.Dragging) return;
        transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
    }

    public void EndDrag(bool installed)
    {
        if (installed)
        {
            SetState(DesktopIconState.Installed);
            gameObject.SetActive(false);
            return;
        }

        transform.position = homePosition;
        SetState(DesktopIconState.Normal);
        if (platformCollider != null) platformCollider.enabled = true;
    }

    // ---------------- Temporary-icon lifetime ----------------

    public void ScheduleExpire(float lifetime)
    {
        if (!Application.isPlaying) return;

        if (expireRoutine != null) StopCoroutine(expireRoutine);
        if (!isActiveAndEnabled)
        {
            // Cannot run a coroutine on an inactive object; still honor the lifetime.
            Destroy(gameObject, Mathf.Max(0f, lifetime));
            return;
        }
        expireRoutine = StartCoroutine(ExpireRoutine(Mathf.Max(0f, lifetime)));
    }

    private IEnumerator ExpireRoutine(float lifetime)
    {
        float blinkDuration = Mathf.Min(1f, lifetime);
        float solidDuration = lifetime - blinkDuration;
        if (solidDuration > 0f) yield return new WaitForSeconds(solidDuration);

        if (bodyRenderer != null) bodyRenderer.enabled = true;
        int toggles = Mathf.Max(1, Mathf.RoundToInt(blinkDuration / 0.125f));
        for (int i = 0; i < toggles; i++)
        {
            if (bodyRenderer != null) bodyRenderer.enabled = !bodyRenderer.enabled;
            yield return BlinkWait;
        }
        Destroy(gameObject);
    }

    // ---------------- Virus damage & Accelerator win (root trigger colliders) ----------------

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Stay only matters for the virus: re-hit after the cooldown while the player
        // keeps standing on/inside it. (Cooldown gate keeps this cheap.)
        if (iconType == DesktopIconType.Virus) HandlePlayerTrigger(other);
    }

    private void HandlePlayerTrigger(Collider2D other)
    {
        if (state == DesktopIconState.Deleted) return;
        if (!other.CompareTag("Player")) return;

        if (iconType == DesktopIconType.Virus) TryVirusHit(other);
        else if (iconType == DesktopIconType.Accelerator) TryTriggerWin();
    }

    private void TryVirusHit(Collider2D other)
    {
        if (gameStateManager != null && gameStateManager.State != GameState.Playing) return;
        if (Time.time < nextVirusDamageTime) return;
        nextVirusDamageTime = Time.time + virusDamageCooldown;

        if (cpuManager != null) cpuManager.AddCpu(virusCpuSpike);

        if (player == null) player = other.GetComponentInParent<PlayerController2D>();
        if (player != null)
        {
            Vector2 dir = (Vector2)(player.transform.position - transform.position);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            player.ApplyKnockback(dir * virusKnockback);
        }

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        if (isActiveAndEnabled) flashRoutine = StartCoroutine(FlashBodyRed());
    }

    private IEnumerator FlashBodyRed()
    {
        for (int i = 0; i < 3; i++)
        {
            if (bodyRenderer != null) bodyRenderer.color = Color.red;
            yield return FlashWait;
            if (bodyRenderer != null) bodyRenderer.color = CurrentBodyColor();
            yield return FlashWait;
        }
        flashRoutine = null;
    }

    private void TryTriggerWin()
    {
        if (winTriggered || gameStateManager == null) return;
        winTriggered = true;
        gameStateManager.TriggerWin();
    }

    // ---------------- Runtime factory ----------------

    public static DesktopIcon CreateRuntimeIcon(string name, DesktopIconType type, Vector2 position,
        Transform parent, bool isPlatform = true, bool isShortcut = false)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false); // configure fields BEFORE Awake runs
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        DesktopIcon icon = go.AddComponent<DesktopIcon>();
        icon.iconName = name;
        icon.iconType = type;
        icon.isPlatform = isPlatform;
        icon.isShortcut = isShortcut;

        go.SetActive(true);  // Awake captures HomePosition and runs EnsureVisuals
        icon.EnsureVisuals(); // idempotent; also covers edit-mode callers where Awake did not run
        return icon;
    }
}
