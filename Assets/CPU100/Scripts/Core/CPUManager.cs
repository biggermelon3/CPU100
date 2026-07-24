using UnityEngine;

/// <summary>
/// Global CPU load model (0..100). Grows smoothly every frame from the base rate,
/// running-software load and current hazard load; instant spikes/relief go through
/// AddCpu/RelieveCpu. Fires change/stage/maximum events (contract §5.2).
/// </summary>
public class CPUManager : MonoBehaviour
{
    public float baseIncreasePerSecond = 0.35f;
    public SoftwareInventory softwareInventory;

    public float CurrentCpu { get { return currentCpu; } }
    public CPUStage CurrentStage { get { return currentStage; } }

    /// <summary>True → no growth and no value changes (set by GameStateManager on win/fail).</summary>
    public bool Frozen { get; set; }

    public event System.Action<float> OnCpuChanged;
    public event System.Action<CPUStage> OnCpuStageChanged;
    public event System.Action OnCpuReachedMaximum;

    private float currentCpu;
    private CPUStage currentStage = CPUStage.Normal;
    private float hazardLoad;
    private bool maximumFired;

    private void Awake()
    {
        if (softwareInventory == null)
            softwareInventory = FindFirstObjectByType<SoftwareInventory>();

        currentStage = StageForValue(currentCpu);
    }

    private void Update()
    {
        if (Frozen || maximumFired)
            return;

        float perSecond = baseIncreasePerSecond + hazardLoad;
        if (softwareInventory != null)
            perSecond += softwareInventory.TotalRunningLoadPerSecond;

        if (perSecond != 0f)
            AddCpu(perSecond * Time.deltaTime);
    }

    /// <summary>Instant add, clamps to 0..100, fires events. No-op after Crashed or while Frozen.</summary>
    public void AddCpu(float amount)
    {
        ApplyDelta(amount);
    }

    /// <summary>Instant subtract, clamps to 0..100, fires events. No-op after Crashed or while Frozen.</summary>
    public void RelieveCpu(float amount)
    {
        ApplyDelta(-amount);
    }

    /// <summary>Continuous extra load in CPU/second; set by GlitchBoundsController every frame.</summary>
    public void SetHazardLoad(float perSecond)
    {
        hazardLoad = Mathf.Max(0f, perSecond);
    }

    public static CPUStage StageForValue(float cpu)
    {
        if (cpu <= 30f) return CPUStage.Normal;
        if (cpu <= 50f) return CPUStage.LightLoad;
        if (cpu <= 70f) return CPUStage.MediumLoad;
        if (cpu <= 85f) return CPUStage.HeavyLoad;
        if (cpu < 100f) return CPUStage.Critical;
        return CPUStage.Crashed;
    }

    private void ApplyDelta(float delta)
    {
        // Terminal after the crash event: further calls change nothing.
        if (Frozen || maximumFired)
            return;

        float next = Mathf.Clamp(currentCpu + delta, 0f, 100f);
        if (next == currentCpu)
            return; // OnCpuChanged only fires when the value actually changed.

        currentCpu = next;

        // Latch BEFORE invoking handlers so reentrant AddCpu calls from a handler
        // cannot fire OnCpuReachedMaximum twice.
        bool reachedMax = currentCpu >= 100f;
        if (reachedMax)
            maximumFired = true;

        OnCpuChanged?.Invoke(currentCpu);

        CPUStage newStage = StageForValue(currentCpu);
        if (newStage != currentStage)
        {
            currentStage = newStage;
            OnCpuStageChanged?.Invoke(currentStage);
        }

        if (reachedMax)
            OnCpuReachedMaximum?.Invoke();
    }
}
