using UnityEngine;
using UnityEngine.UI;

// Fake "Task Manager - CPU" window (UI GO "CPUWindow"). Event-driven display of
// CPU percent / stage / running process count, with window shake at HeavyLoad+
// and percent-text flicker at Critical. Strings are pre-built in Awake so the
// per-event handlers are allocation-free.
public class CPUWindowUI : MonoBehaviour
{
    public CPUManager cpuManager;
    public SoftwareInventory inventory;
    public RectTransform windowRoot;          // shaken at HeavyLoad+, base pos cached
    public Text percentText;                  // "63%"
    public Image fillBar;                     // Filled/Horizontal; green->yellow->red
    public Text stageText;                    // Normal/Light Load/.../CRITICAL/CRASHED
    public Text runningCountText;             // "Processes: 2/3"
    public Image thermometerFill;             // Filled/Vertical

    const float HeavyShakeAmplitude = 1.5f;
    const float CriticalShakeAmplitude = 4f;
    const float FlickerInterval = 0.15f;      // ~3 red/white cycles per second

    Vector2 baseAnchoredPosition;
    float shakeAmplitude;
    bool flickering;
    float flickerTimer;
    bool flickerOn;
    int lastPercent = -1;
    string[] percentStrings;                  // "0%".."100%", pre-built (no per-event GC)
    string[] processStrings;                  // "Processes: 0/3".."3/3"

    void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (inventory == null) inventory = FindFirstObjectByType<SoftwareInventory>();
        if (windowRoot == null) windowRoot = transform as RectTransform;

        // Fallback wiring by builder child names (see contract section 6).
        if (percentText == null) percentText = FindChild<Text>("PercentText");
        if (fillBar == null) fillBar = FindChild<Image>("FillBarBg/FillBar");
        if (stageText == null) stageText = FindChild<Text>("StageText");
        if (runningCountText == null) runningCountText = FindChild<Text>("RunningCountText");
        if (thermometerFill == null) thermometerFill = FindChild<Image>("ThermoBg/ThermometerFill");

        Text[] childTexts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < childTexts.Length; i++) EnsureFont(childTexts[i]);
        EnsureFont(percentText);
        EnsureFont(stageText);
        EnsureFont(runningCountText);

        if (windowRoot != null) baseAnchoredPosition = windowRoot.anchoredPosition;

        percentStrings = new string[101];
        for (int i = 0; i <= 100; i++) percentStrings[i] = i + "%";

        processStrings = new string[SoftwareInventory.Capacity + 1];
        for (int i = 0; i <= SoftwareInventory.Capacity; i++)
            processStrings[i] = "Processes: " + i + "/" + SoftwareInventory.Capacity;
    }

    void OnEnable()
    {
        if (cpuManager != null)
        {
            cpuManager.OnCpuChanged += HandleCpuChanged;
            cpuManager.OnCpuStageChanged += HandleStageChanged;
            // Sync immediately so the window is correct before the first event.
            HandleCpuChanged(cpuManager.CurrentCpu);
            HandleStageChanged(cpuManager.CurrentStage);
        }
        if (inventory != null)
            inventory.OnInventoryChanged += HandleInventoryChanged;
        HandleInventoryChanged();
    }

    void OnDisable()
    {
        if (cpuManager != null)
        {
            cpuManager.OnCpuChanged -= HandleCpuChanged;
            cpuManager.OnCpuStageChanged -= HandleStageChanged;
        }
        if (inventory != null)
            inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    void Update()
    {
        if (windowRoot != null && shakeAmplitude > 0f)
        {
            windowRoot.anchoredPosition = baseAnchoredPosition + new Vector2(
                Random.Range(-shakeAmplitude, shakeAmplitude),
                Random.Range(-shakeAmplitude, shakeAmplitude));
        }

        if (flickering && percentText != null)
        {
            flickerTimer += Time.deltaTime;
            if (flickerTimer >= FlickerInterval)
            {
                flickerTimer -= FlickerInterval;
                flickerOn = !flickerOn;
                percentText.color = flickerOn ? Color.red : Color.white;
            }
        }
    }

    void HandleCpuChanged(float cpu)
    {
        float t = Mathf.Clamp01(cpu / 100f);

        if (fillBar != null)
        {
            fillBar.fillAmount = t;
            fillBar.color = LoadColor(t);
        }
        if (thermometerFill != null) thermometerFill.fillAmount = t;

        int percent = Mathf.Clamp(Mathf.RoundToInt(cpu), 0, 100);
        if (percent != lastPercent && percentText != null && percentStrings != null)
        {
            lastPercent = percent;
            percentText.text = percentStrings[percent];
        }
    }

    void HandleStageChanged(CPUStage stage)
    {
        if (stageText != null) stageText.text = StageLabel(stage);

        switch (stage)
        {
            case CPUStage.HeavyLoad:
                shakeAmplitude = HeavyShakeAmplitude;
                break;
            case CPUStage.Critical:
            case CPUStage.Crashed:
                shakeAmplitude = CriticalShakeAmplitude;
                break;
            default:
                shakeAmplitude = 0f;
                break;
        }

        // Restore the cached base position as soon as things calm down.
        if (shakeAmplitude <= 0f && windowRoot != null)
            windowRoot.anchoredPosition = baseAnchoredPosition;

        flickering = stage == CPUStage.Critical;
        if (!flickering && percentText != null)
        {
            flickerTimer = 0f;
            flickerOn = false;
            percentText.color = Color.white;
        }
    }

    void HandleInventoryChanged()
    {
        if (runningCountText == null || processStrings == null) return;
        int count = inventory != null
            ? Mathf.Clamp(inventory.RunningCount, 0, SoftwareInventory.Capacity)
            : 0;
        runningCountText.text = processStrings[count];
    }

    // Green -> yellow over the first half, yellow -> red over the second half.
    static Color LoadColor(float t)
    {
        return t < 0.5f
            ? Color.Lerp(Color.green, Color.yellow, t * 2f)
            : Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
    }

    static string StageLabel(CPUStage stage)
    {
        switch (stage)
        {
            case CPUStage.LightLoad: return "Light Load";
            case CPUStage.MediumLoad: return "Medium Load";
            case CPUStage.HeavyLoad: return "Heavy Load";
            case CPUStage.Critical: return "CRITICAL";
            case CPUStage.Crashed: return "CRASHED";
            default: return "Normal";
        }
    }

    T FindChild<T>(string path) where T : Component
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    static void EnsureFont(Text t)
    {
        if (t != null && t.font == null)
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
