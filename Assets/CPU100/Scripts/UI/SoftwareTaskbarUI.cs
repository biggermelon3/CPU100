using UnityEngine;
using UnityEngine.UI;

// Taskbar controller (UI GO "DesktopTaskbar"): binds the three software slots to the
// inventory, refreshes them on inventory events, and drives the system clock text.
public class SoftwareTaskbarUI : MonoBehaviour
{
    public SoftwareInventory inventory;
    public TaskbarSlotUI[] slots;             // 3; fallback GetComponentsInChildren
    public Text clockText;                    // system clock "HH:mm"

    float clockTimer;
    int lastClockMinute = -1;

    void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<SoftwareInventory>();
        if (slots == null || slots.Length == 0)
            slots = GetComponentsInChildren<TaskbarSlotUI>(true);
        if (clockText == null)
        {
            Transform t = transform.Find("ClockText");
            if (t != null) clockText = t.GetComponent<Text>();
        }

        // Fix fonts on every child Text (clock, start button label, ...) plus the wired ref.
        Text[] childTexts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < childTexts.Length; i++) EnsureFont(childTexts[i]);
        EnsureFont(clockText);
    }

    void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += HandleInventoryChanged;
            inventory.OnSelectedChanged += HandleSelectedChanged;
        }
    }

    void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= HandleInventoryChanged;
            inventory.OnSelectedChanged -= HandleSelectedChanged;
        }
    }

    void Start()
    {
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].Bind(inventory, i);
            }
        }
        RefreshAllSlots();
        UpdateClock(true);
    }

    void Update()
    {
        if (clockText == null) return;
        clockTimer += Time.unscaledDeltaTime;
        if (clockTimer < 1f) return;      // poll once per second
        clockTimer = 0f;
        UpdateClock(false);
    }

    void UpdateClock(bool force)
    {
        if (clockText == null) return;
        System.DateTime now = System.DateTime.Now;
        // Only rebuild the string when the displayed minute actually changes.
        if (!force && now.Minute == lastClockMinute) return;
        lastClockMinute = now.Minute;
        clockText.text = now.ToString("HH:mm");
    }

    void HandleInventoryChanged()
    {
        RefreshAllSlots();
    }

    void HandleSelectedChanged(int selectedIndex)
    {
        RefreshAllSlots();
    }

    void RefreshAllSlots()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].Refresh();
        }
    }

    static void EnsureFont(Text t)
    {
        if (t != null && t.font == null)
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
