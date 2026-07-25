using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Virtual mouse that lives on child GO "VirtualCursor" under the Player.
// The real mouse position is projected into the world and clamped to a circle of
// maxRadius around the player; ALL world interaction uses that clamped position.
// The OS cursor is hidden while playing: inside the ring only the custom cursor
// shows, and when the real mouse leaves the ring a semi-transparent ghost cursor
// (a top-most UI Image, so it stays visible over the taskbar) marks the real
// mouse position while the custom cursor stays pinned to the ring edge.
public class CursorInteractor : MonoBehaviour
{
    public float maxRadius = 2.5f;
    public bool showRangeDebug = true;
    public float ghostAlpha = 0.4f;           // real-mouse ghost shown outside the ring
    public PlayerController2D player;         // fallback GetComponentInParent
    public SoftwareInstallZone installZone;   // fallback FindFirstObjectByType
    public Camera worldCamera;                // fallback Camera.main
    public GameStateManager gameState;        // fallback find
    public SoftwareTaskbarUI taskbarUI;       // fallback find; small-cursor priority + fly-in target

    public Vector2 CursorWorldPos { get; private set; }
    public Vector2 PointerWorldPos { get; private set; }
    public bool CanInteractWithWorld { get { return mouseInsideRadius; } }
    public DesktopIcon DraggedIcon { get { return draggedIcon; } }

    DesktopIcon draggedIcon;
    DesktopIcon selectedIcon;
    Vector2 dragOffset;
    Transform cursorSprite;
    Transform rangeRing;
    SpriteRenderer rangeRingRenderer;
    RectTransform ghostCursor;
    UnityEngine.UI.Image ghostImage;
    bool osCursorHidden;
    bool mouseInsideRadius = true;
    bool overTaskbar;
    Vector2 lastScreenPos;
    ContactFilter2D overlapFilter;
    readonly Collider2D[] overlapBuffer = new Collider2D[16];

    void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController2D>();
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (installZone == null) installZone = FindFirstObjectByType<SoftwareInstallZone>();
        if (worldCamera == null) worldCamera = Camera.main;
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
        if (taskbarUI == null) taskbarUI = FindFirstObjectByType<SoftwareTaskbarUI>();

        overlapFilter = ContactFilter2D.noFilter;
        overlapFilter.useTriggers = true; // any layer, triggers included

        BuildVisuals();
        CursorWorldPos = transform.position;
    }

    // Builds CursorSprite / RangeRing children if missing. Sprites are always
    // re-assigned because procedural sprites do not survive a scene save.
    void BuildVisuals()
    {
        cursorSprite = transform.Find("CursorSprite");
        if (cursorSprite == null)
        {
            var go = new GameObject("CursorSprite");
            cursorSprite = go.transform;
            cursorSprite.SetParent(transform, false);
            cursorSprite.localPosition = Vector3.zero;
        }
        var cursorRenderer = cursorSprite.GetComponent<SpriteRenderer>();
        if (cursorRenderer == null) cursorRenderer = cursorSprite.gameObject.AddComponent<SpriteRenderer>();
        cursorRenderer.sortingOrder = 200;
        cursorRenderer.sprite = PlaceholderSpriteFactory.GetCursor();

        rangeRing = transform.Find("RangeRing");
        if (rangeRing == null)
        {
            var go = new GameObject("RangeRing");
            rangeRing = go.transform;
            rangeRing.SetParent(transform, false);
            rangeRing.localPosition = Vector3.zero;
        }
        rangeRingRenderer = rangeRing.GetComponent<SpriteRenderer>();
        if (rangeRingRenderer == null) rangeRingRenderer = rangeRing.gameObject.AddComponent<SpriteRenderer>();
        rangeRingRenderer.sortingOrder = 199;
        rangeRingRenderer.sprite = PlaceholderSpriteFactory.GetRing();
        rangeRingRenderer.color = new Color(1f, 1f, 1f, 0.15f);
        rangeRing.localScale = new Vector3(maxRadius * 2f, maxRadius * 2f, 1f);
        rangeRing.gameObject.SetActive(showRangeDebug);
    }

    void Update()
    {
        if (gameState != null && gameState.State != GameState.Playing)
        {
            // Result screens need the OS cursor back for the restart buttons.
            SetOsCursorHidden(false);
            if (ghostCursor != null && ghostCursor.gameObject.activeSelf)
                ghostCursor.gameObject.SetActive(false);
            if (cursorSprite != null && cursorSprite.gameObject.activeSelf)
                cursorSprite.gameObject.SetActive(false);
            // Game froze mid-drag: return the icon home so it is not left floating.
            if (draggedIcon != null)
            {
                draggedIcon.CancelDrag();
                draggedIcon = null;
            }
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        SetOsCursorHidden(true);
        UpdateCursorPosition(mouse);
        UpdateOverTaskbar();
        UpdateCursorVisuals();
        UpdateGhost();

        // Outside the local interaction radius the small cursor is UI-only.
        // Do not let the hidden, clamped world cursor click or keep dragging
        // objects at the edge of the radius.
        if (!mouseInsideRadius)
        {
            if (draggedIcon != null)
            {
                draggedIcon.CancelDrag();
                draggedIcon = null;
            }
            return;
        }

        if (draggedIcon != null)
        {
            // Releasing completes an interaction already in flight, so it is
            // processed even while the pointer is over UI (avoids a stuck drag).
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                draggedIcon.UpdateDrag(CursorWorldPos + dragOffset);
                FinishDrag();
            }
            else draggedIcon.UpdateDrag(CursorWorldPos + dragOffset);
            return;
        }

        // Never START a world interaction through UI.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (mouse.leftButton.wasPressedThisFrame) HandlePress();
    }

    void LateUpdate()
    {
        // The VirtualCursor GO itself moves; the ring must stay centered on the player.
        if (rangeRing == null) return;
        if (rangeRing.gameObject.activeSelf != showRangeDebug)
            rangeRing.gameObject.SetActive(showRangeDebug);
        if (!showRangeDebug) return;

        if (player != null) rangeRing.position = player.transform.position;
        float diameter = maxRadius * 2f;
        if (!Mathf.Approximately(rangeRing.localScale.x, diameter))
            rangeRing.localScale = new Vector3(diameter, diameter, 1f);
    }

    void UpdateCursorPosition(Mouse mouse)
    {
        Vector2 screenPos = mouse.position.ReadValue();
        lastScreenPos = screenPos;
        Vector3 world = worldCamera != null
            ? worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f))
            : (Vector3)screenPos;
        world.z = 0f;
        PointerWorldPos = world;

        mouseInsideRadius = true;
        if (player != null)
        {
            Vector2 center = player.transform.position;
            Vector2 offset = (Vector2)world - center;
            if (offset.sqrMagnitude > maxRadius * maxRadius)
            {
                mouseInsideRadius = false;
                world = center + offset.normalized * maxRadius;
            }
        }

        world.z = 0f;
        transform.position = world;
        CursorWorldPos = world;
    }

    // The taskbar is UI: even inside the interaction ring the SMALL cursor takes
    // priority there, because clicks go to the slots, not the world. Dragging an
    // icon keeps the big cursor so the carried item stays readable.
    void UpdateOverTaskbar()
    {
        RectTransform tbRect = taskbarUI != null ? taskbarUI.transform as RectTransform : null;
        overTaskbar = tbRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(tbRect, lastScreenPos);
    }

    bool SmallCursorPriority { get { return overTaskbar && draggedIcon == null; } }

    void SetOsCursorHidden(bool hidden)
    {
        if (osCursorHidden == hidden) return;
        osCursorHidden = hidden;
        Cursor.visible = !hidden;
    }

    // Ghost is parented to the UI canvas as the LAST sibling so it renders above
    // the taskbar/CPU window; raycastTarget stays off so it never eats clicks.
    void EnsureGhost()
    {
        if (ghostCursor != null) return;
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var existing = canvas.transform.Find("RealMouseGhost");
        GameObject go = existing != null ? existing.gameObject : new GameObject("RealMouseGhost");
        ghostCursor = go.GetComponent<RectTransform>();
        if (ghostCursor == null) ghostCursor = go.AddComponent<RectTransform>();
        ghostCursor.SetParent(canvas.transform, false);
        ghostCursor.SetAsLastSibling();
        ghostCursor.pivot = new Vector2(0.15f, 0.9f); // matches the cursor sprite hotspot
        ghostCursor.sizeDelta = new Vector2(26f, 26f);

        ghostImage = go.GetComponent<UnityEngine.UI.Image>();
        if (ghostImage == null) ghostImage = go.AddComponent<UnityEngine.UI.Image>();
        ghostImage.sprite = PlaceholderSpriteFactory.GetCursor();
        ghostImage.color = new Color(1f, 1f, 1f, ghostAlpha);
        ghostImage.raycastTarget = false;

        go.SetActive(false);
    }

    void UpdateGhost()
    {
        EnsureGhost();
        if (ghostCursor == null) return;

        bool show = !mouseInsideRadius || SmallCursorPriority;
        if (ghostCursor.gameObject.activeSelf != show)
            ghostCursor.gameObject.SetActive(show);
        // Overlay-canvas RectTransform.position is in screen pixels.
        if (show) ghostCursor.position = new Vector3(lastScreenPos.x, lastScreenPos.y, 0f);
    }

    void UpdateCursorVisuals()
    {
        // Exactly one custom cursor is visible: the large world cursor inside
        // the interaction radius, or the small UI cursor outside it / over the taskbar.
        bool showWorldCursor = mouseInsideRadius && !SmallCursorPriority;
        if (cursorSprite != null &&
            cursorSprite.gameObject.activeSelf != showWorldCursor)
        {
            cursorSprite.gameObject.SetActive(showWorldCursor);
        }
    }

    void OnDisable()
    {
        SetOsCursorHidden(false);
        if (ghostCursor != null) ghostCursor.gameObject.SetActive(false);
        if (cursorSprite != null) cursorSprite.gameObject.SetActive(false);
    }

    void HandlePress()
    {
        int count = Physics2D.OverlapPoint(CursorWorldPos, overlapFilter, overlapBuffer);
        DesktopIcon icon = null;
        int bestOrder = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;
            var candidate = col.GetComponentInParent<DesktopIcon>();
            if (candidate == null || candidate.State == DesktopIconState.Deleted) continue;
            int order = BodySortingOrder(candidate);
            if (icon == null || order > bestOrder)
            {
                icon = candidate;
                bestOrder = order;
            }
        }

        if (icon == null)
        {
            ClearSelection(); // click on empty space
            return;
        }

        if (icon.canDrag && !icon.IsCorrupted && icon.State != DesktopIconState.Installed)
        {
            if (selectedIcon == icon) selectedIcon = null; // BeginDrag replaces its state
            dragOffset = (Vector2)icon.transform.position - CursorWorldPos;
            icon.BeginDrag();
            draggedIcon = icon;
            return;
        }

        if (selectedIcon == icon) return;
        ClearSelection();
        // Selection bookkeeping only ever flips Normal <-> Selected.
        if (icon.State == DesktopIconState.Normal)
        {
            icon.SetState(DesktopIconState.Selected);
            selectedIcon = icon;
        }
    }

    void FinishDrag()
    {
        var icon = draggedIcon;
        draggedIcon = null;
        if (icon == null) return;

        bool installed = false;
        if (icon.canInstall && player != null && installZone != null &&
            Vector2.Distance((Vector2)icon.transform.position, (Vector2)player.transform.position) <= installZone.Radius)
        {
            installed = installZone.TryInstall(icon);
        }
        if (installed) TriggerInstallFlyEffect(icon); // before EndDrag hides the icon
        icon.EndDrag(installed);

        // A repositioned folder behaves like a desktop item after mouse-up:
        // it remains selected and keeps the same outline as a clicked static icon.
        if (!installed && icon.canDrag && !icon.canInstall &&
            icon.iconType == DesktopIconType.Folder)
        {
            ClearSelection();
            icon.SetState(DesktopIconState.Selected);
            selectedIcon = icon;
        }
    }

    // Shrinking icon + trail flying from the drop position into the taskbar slot
    // that just received the software.
    void TriggerInstallFlyEffect(DesktopIcon icon)
    {
        if (installZone == null || installZone.inventory == null || taskbarUI == null) return;
        int index = installZone.inventory.LastInstalledIndex;
        TaskbarSlotUI[] slots = taskbarUI.slots;
        if (index < 0 || slots == null || index >= slots.Length || slots[index] == null) return;
        InstallFlyEffect.Play(icon.BodySprite, icon.BodyWorldScale, icon.transform.position,
            slots[index].transform as RectTransform, worldCamera);
    }

    void ClearSelection()
    {
        if (selectedIcon != null && selectedIcon.State == DesktopIconState.Selected)
            selectedIcon.SetState(DesktopIconState.Normal);
        selectedIcon = null;
    }

    // Used to pick the topmost icon when several colliders overlap the click point.
    static int BodySortingOrder(DesktopIcon icon)
    {
        var body = icon.transform.Find("Body");
        if (body == null) return 0;
        var sr = body.GetComponent<SpriteRenderer>();
        return sr != null ? sr.sortingOrder : 0;
    }
}
