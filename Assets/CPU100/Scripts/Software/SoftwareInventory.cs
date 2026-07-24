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

    public const int Capacity = 3;

    private readonly SoftwareRuntimeItem[] slots = new SoftwareRuntimeItem[Capacity];
    private int selectedIndex = -1;
    private float totalRunningLoadPerSecond;

    public SoftwareRuntimeItem[] Slots => slots;
    public int SelectedIndex => selectedIndex;

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
                OnInventoryChanged?.Invoke();
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
        OnInventoryChanged?.Invoke();
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

        if (selectedIndex == index)
        {
            selectedIndex = -1;
            OnSelectedChanged?.Invoke(selectedIndex);
        }

        RecomputeRunningLoad();
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
}
