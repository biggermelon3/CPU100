using System.Collections;
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
    private float growthSlowdownTimer;
    private float growthMultiplier = 1f;
    private bool maximumFired;
    private Coroutine drainRoutine;
    private Coroutine temporaryOverrideRoutine;
    private bool temporaryValueFrozen;

    private void Awake()
    {
        if (softwareInventory == null)
            softwareInventory = FindFirstObjectByType<SoftwareInventory>();

        currentStage = StageForValue(currentCpu);
    }

    private void Update()
    {
        if (Frozen || temporaryValueFrozen || maximumFired)
            return;

        if (growthSlowdownTimer > 0f)
        {
            growthSlowdownTimer -= Time.deltaTime;
            if (growthSlowdownTimer <= 0f)
            {
                growthSlowdownTimer = 0f;
                growthMultiplier = 1f;
            }
        }

        float perSecond = baseIncreasePerSecond + hazardLoad;
        if (softwareInventory != null)
            perSecond += softwareInventory.TotalRunningLoadPerSecond;

        if (perSecond != 0f)
            AddCpu(perSecond * growthMultiplier * Time.deltaTime);
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

    /// <summary>
    /// Terminal victory animation. Growth remains frozen, but value and stage events
    /// continue to fire while the displayed load eases down to zero.
    /// </summary>
    public void DrainToZero(float duration)
    {
        if (temporaryOverrideRoutine != null)
        {
            StopCoroutine(temporaryOverrideRoutine);
            temporaryOverrideRoutine = null;
        }
        temporaryValueFrozen = false;
        Frozen = true;
        if (drainRoutine != null)
            StopCoroutine(drainRoutine);

        if (!isActiveAndEnabled || duration <= 0f)
        {
            SetCpuDuringDrain(0f);
            return;
        }

        drainRoutine = StartCoroutine(DrainRoutine(duration));
    }

    /// <summary>
    /// Temporarily displays a fixed CPU value and blocks all CPU growth/deltas.
    /// Restores the exact value captured at activation when the duration ends.
    /// </summary>
    public void SetTemporaryValueAndFreeze(float temporaryValue, float duration)
    {
        if (Frozen || maximumFired || temporaryValueFrozen)
            return;

        float restoreValue = currentCpu;
        temporaryValueFrozen = true;
        SetCpuDuringDrain(temporaryValue);

        if (!isActiveAndEnabled || duration <= 0f)
        {
            temporaryValueFrozen = false;
            SetCpuDuringDrain(restoreValue);
            return;
        }

        temporaryOverrideRoutine =
            StartCoroutine(TemporaryOverrideRoutine(restoreValue, duration));
    }

    /// <summary>Continuous extra load in CPU/second; set by GlitchBoundsController every frame.</summary>
    public void SetHazardLoad(float perSecond)
    {
        hazardLoad = Mathf.Max(0f, perSecond);
    }

    public void ActivateGrowthSlowdown(float duration, float multiplier)
    {
        if (Frozen || temporaryValueFrozen || maximumFired) return;

        growthSlowdownTimer = Mathf.Max(growthSlowdownTimer, Mathf.Max(0f, duration));
        growthMultiplier = Mathf.Min(growthMultiplier, Mathf.Clamp01(multiplier));
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
        if (Frozen || temporaryValueFrozen || maximumFired)
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

    private IEnumerator DrainRoutine(float duration)
    {
        float start = currentCpu;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            SetCpuDuringDrain(Mathf.LerpUnclamped(start, 0f, t));
            yield return null;
        }

        SetCpuDuringDrain(0f);
        drainRoutine = null;
    }

    private IEnumerator TemporaryOverrideRoutine(float restoreValue, float duration)
    {
        yield return new WaitForSeconds(duration);
        temporaryOverrideRoutine = null;
        temporaryValueFrozen = false;

        // A terminal game state may have taken ownership while the effect was active.
        if (!Frozen && !maximumFired)
            SetCpuDuringDrain(restoreValue);
    }

    private void SetCpuDuringDrain(float value)
    {
        float next = Mathf.Clamp(value, 0f, 100f);
        if (next == currentCpu) return;

        currentCpu = next;
        OnCpuChanged?.Invoke(currentCpu);

        CPUStage newStage = StageForValue(currentCpu);
        if (newStage == currentStage) return;
        currentStage = newStage;
        OnCpuStageChanged?.Invoke(currentStage);
    }
}
