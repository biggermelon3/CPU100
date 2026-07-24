using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One taskbar software slot. Single click selects, double click launches,
// close button permanently deletes. Visuals fully re-render from inventory state
// in Refresh(); Update polls only the cooldown fill (alloc-free).
public class TaskbarSlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image iconImage;            // software icon (or placeholder), hidden when empty
    public Text nameText;
    public Image selectionHighlight;   // active == selected
    public Image runningIndicator;     // active == running
    public Image cooldownMask;         // Filled/Vertical, fillAmount = remaining/cooldown
    public Button closeButton;         // active == occupied; onClick -> CloseAndDelete

    SoftwareInventory inventory;
    int index = -1;

    void Awake()
    {
        // Fallback wiring by builder child names (see contract section 6).
        if (iconImage == null) iconImage = FindChild<Image>("IconImage");
        if (nameText == null) nameText = FindChild<Text>("NameText");
        if (selectionHighlight == null) selectionHighlight = FindChild<Image>("SelectionHighlight");
        if (runningIndicator == null) runningIndicator = FindChild<Image>("RunningIndicator");
        if (cooldownMask == null) cooldownMask = FindChild<Image>("CooldownMask");
        if (closeButton == null) closeButton = FindChild<Button>("CloseButton");

        Text[] childTexts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < childTexts.Length; i++) EnsureFont(childTexts[i]);
        EnsureFont(nameText);

        if (closeButton != null) closeButton.onClick.AddListener(HandleCloseClicked);
    }

    public void Bind(SoftwareInventory inv, int slotIndex)
    {
        inventory = inv;
        index = slotIndex;
        Refresh();
    }

    // Full visual re-render from current inventory state.
    public void Refresh()
    {
        SoftwareRuntimeItem item = GetItem();
        bool occupied = item != null && item.Data != null;

        if (iconImage != null)
        {
            iconImage.enabled = occupied;
            if (occupied)
            {
                iconImage.sprite = item.Data.icon != null
                    ? item.Data.icon
                    : PlaceholderSpriteFactory.GetSoftwareIconSprite(item.Data.abilityType);
            }
        }

        if (nameText != null)
        {
            nameText.text = occupied && !string.IsNullOrEmpty(item.Data.softwareName)
                ? item.Data.softwareName
                : "-";
        }

        if (selectionHighlight != null)
            selectionHighlight.gameObject.SetActive(occupied && inventory != null && inventory.SelectedIndex == index);

        if (runningIndicator != null)
            runningIndicator.gameObject.SetActive(occupied && item.IsRunning);

        if (cooldownMask != null)
            cooldownMask.fillAmount = CooldownFill(item);

        if (closeButton != null)
            closeButton.gameObject.SetActive(occupied);
    }

    void Update()
    {
        // Poll only the cooldown fill; everything else is event-driven via Refresh().
        if (cooldownMask == null) return;
        cooldownMask.fillAmount = CooldownFill(GetItem());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventory == null || index < 0) return;
        if (eventData.clickCount >= 2) inventory.TryLaunch(index);
        else inventory.Select(index);
    }

    void HandleCloseClicked()
    {
        if (inventory == null || index < 0) return;
        inventory.CloseAndDelete(index);
    }

    SoftwareRuntimeItem GetItem()
    {
        if (inventory == null || index < 0) return null;
        SoftwareRuntimeItem[] slots = inventory.Slots;
        if (slots == null || index >= slots.Length) return null;
        return slots[index];
    }

    static float CooldownFill(SoftwareRuntimeItem item)
    {
        if (item == null || item.Data == null) return 0f;
        if (item.Data.cooldown <= 0f || item.CooldownRemaining <= 0f) return 0f;
        return Mathf.Clamp01(item.CooldownRemaining / item.Data.cooldown);
    }

    T FindChild<T>(string childName) where T : Component
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    static void EnsureFont(Text t)
    {
        if (t != null && t.font == null)
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
