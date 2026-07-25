using UnityEngine;

/// <summary>
/// The 3-slot taskbar inventory. Install / select / launch / close-and-delete software.
/// CloseAndDelete is PERMANENT (jam design rule) — the software can never be regained.
/// Total running CPU load is cached and recomputed only on launch/close so CPUManager
/// can read it every frame for free.
/// </summary>
public class SoftwareInventory : MonoBehaviour
{
    public CPUManager cpuManager;
    public PlayerController2D player;
    public SystemInteractionAudio interactionAudio;

    public const int Capacity = 3;

    private readonly SoftwareRuntimeItem[] slots = new SoftwareRuntimeItem[Capacity];
    private int selectedIndex = -1;
    private float totalRunningLoadPerSecond;

    public SoftwareRuntimeItem[] Slots => slots;
    public int SelectedIndex => selectedIndex;

    /// <summary>Slot index filled by the most recent successful TryInstall (-1 before any).
    /// Read by CursorInteractor to aim the install fly-in effect.</summary>
    public int LastInstalledIndex { get; private set; } = -1;

    public SoftwareRuntimeItem SelectedItem
    {
        get
        {
            if (selectedIndex < 0 || selectedIndex >= Capacity) return null;
            return slots[selectedIndex];
        }
    }

    public float TotalRunningLoadPerSecond => totalRunningLoadPerSecond;

    public int RunningCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i] != null && slots[i].IsRunning) count++;
            }
            return count;
        }
    }

    public bool HasFreeSlot
    {
        get
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i] == null) return true;
            }
            return false;
        }
    }

    public event System.Action OnInventoryChanged;
    public event System.Action<int> OnSelectedChanged;

    private void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (interactionAudio == null) interactionAudio = FindFirstObjectByType<SystemInteractionAudio>();
    }

    private void Update()
    {
        // Tick cooldowns. Intentionally no events — the taskbar UI polls cooldown fill.
        float dt = Time.deltaTime;
        for (int i = 0; i < Capacity; i++)
        {
            SoftwareRuntimeItem item = slots[i];
            if (item != null && item.CooldownRemaining > 0f)
            {
                item.CooldownRemaining -= dt;
                if (item.CooldownRemaining < 0f) item.CooldownRemaining = 0f;
            }
        }
    }

    /// <summary>Puts the software into the first free slot as Installed. False when full or data is null.</summary>
    public bool TryInstall(SoftwareData data)
    {
        if (data == null) return false;

        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = new SoftwareRuntimeItem(data);
                LastInstalledIndex = i;
                OnInventoryChanged?.Invoke();
                if (interactionAudio != null) interactionAudio.PlayPickup();
                return true;
            }
        }
        return false;
    }

    /// <summary>Selects a non-empty slot; ignored for empty/invalid indices.</summary>
    public void Select(int index)
    {
        if (index < 0 || index >= Capacity) return;
        if (slots[index] == null) return;
        if (selectedIndex == index) return;

        selectedIndex = index;
        OnSelectedChanged?.Invoke(selectedIndex);
        OnInventoryChanged?.Invoke(); // selection highlight refresh
    }

    /// <summary>Installed → Running: pays the startup CPU cost and starts the running load.</summary>
    public bool TryLaunch(int index)
    {
        if (index < 0 || index >= Capacity) return false;

        SoftwareRuntimeItem item = slots[index];
        if (item == null || item.State != SoftwareState.Installed) return false;

        item.State = SoftwareState.Running;
        if (cpuManager != null && item.Data != null)
        {
            cpuManager.AddCpu(item.Data.startupCpuCost);
        }
        RecomputeRunningLoad();
        RefreshPassiveAbilities();
        OnInventoryChanged?.Invoke();
        if (interactionAudio != null) interactionAudio.PlayInstall();
        return true;
    }

    /// <summary>Permanently deletes the slot. Running software relieves some CPU on close.</summary>
    public void CloseAndDelete(int index)
    {
        if (index < 0 || index >= Capacity) return;

        SoftwareRuntimeItem item = slots[index];
        if (item == null) return;

        if (item.IsRunning && cpuManager != null && item.Data != null)
        {
            cpuManager.RelieveCpu(item.Data.closeCpuRelief);
        }

        item.State = SoftwareState.Deleted;
        slots[index] = null; // permanent — no way to reinstall
        if (interactionAudio != null) interactionAudio.PlayDelete();

        if (selectedIndex == index)
        {
            selectedIndex = -1;
            OnSelectedChanged?.Invoke(selectedIndex);
        }

        RecomputeRunningLoad();
        RefreshPassiveAbilities();
        OnInventoryChanged?.Invoke();
    }

    private void RecomputeRunningLoad()
    {
        float total = 0f;
        for (int i = 0; i < Capacity; i++)
        {
            SoftwareRuntimeItem item = slots[i];
            if (item != null && item.IsRunning && item.Data != null)
            {
                total += item.Data.runningCpuLoadPerSecond;
            }
        }
        totalRunningLoadPerSecond = total;
    }

    private void RefreshPassiveAbilities()
    {
        bool doubleJump = false;
        for (int i = 0; i < Capacity; i++)
        {
            SoftwareRuntimeItem item = slots[i];
            if (item != null && item.IsRunning && item.Data != null &&
                item.Data.abilityType == SoftwareAbilityType.DoubleJump)
            {
                doubleJump = true;
                break;
            }
        }

        if (player != null)
            player.SetDoubleJumpEnabled(doubleJump);
    }
}
