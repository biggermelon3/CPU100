using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Virtual mouse that lives on child GO "VirtualCursor" under the Player.
// The real mouse position is projected into the world and clamped to a circle of
// maxRadius around the player; ALL world interaction uses that clamped position.
public class CursorInteractor : MonoBehaviour
{
    public float maxRadius = 2.5f;
    public bool showRangeDebug = true;
    public PlayerController2D player;         // fallback GetComponentInParent
    public SoftwareInstallZone installZone;   // fallback FindFirstObjectByType
    public Camera worldCamera;                // fallback Camera.main
    public GameStateManager gameState;        // fallback find

    public Vector2 CursorWorldPos { get; private set; }
    public DesktopIcon DraggedIcon { get { return draggedIcon; } }

    DesktopIcon draggedIcon;
    DesktopIcon selectedIcon;
    Transform cursorSprite;
    Transform rangeRing;
    SpriteRenderer rangeRingRenderer;
    ContactFilter2D overlapFilter;
    readonly Collider2D[] overlapBuffer = new Collider2D[16];

    void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController2D>();
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (installZone == null) installZone = FindFirstObjectByType<SoftwareInstallZone>();
        if (worldCamera == null) worldCamera = Camera.main;
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();

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
            // Game froze mid-drag: return the icon home so it is not left floating.
            if (draggedIcon != null)
            {
                draggedIcon.EndDrag(false);
                draggedIcon = null;
            }
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        UpdateCursorPosition(mouse);

        if (draggedIcon != null)
        {
            // Releasing completes an interaction already in flight, so it is
            // processed even while the pointer is over UI (avoids a stuck drag).
            if (mouse.leftButton.wasReleasedThisFrame) FinishDrag();
            else draggedIcon.UpdateDrag(CursorWorldPos);
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
        Vector3 world = worldCamera != null
            ? worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f))
            : (Vector3)screenPos;
        world.z = 0f;

        if (player != null)
        {
            Vector2 center = player.transform.position;
            Vector2 offset = (Vector2)world - center;
            if (offset.sqrMagnitude > maxRadius * maxRadius)
                world = center + offset.normalized * maxRadius;
        }

        world.z = 0f;
        transform.position = world;
        CursorWorldPos = world;
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
            Vector2.Distance(CursorWorldPos, (Vector2)player.transform.position) <= installZone.Radius)
        {
            installed = installZone.TryInstall(icon);
        }
        icon.EndDrag(installed);
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
