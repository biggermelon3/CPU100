using UnityEngine;

/// <summary>
/// Keeps four exported music stems on one timeline and mixes the additional
/// layers in and out as CPU load rises and falls.
/// </summary>
public class CpuAdaptiveMusic : MonoBehaviour
{
    public CPUManager cpuManager;

    [Header("Synchronized stems")]
    public AudioClip baseTrack;
    public AudioClip pulseTrack;
    public AudioClip dangerTrack;
    public AudioClip glitchTrack;
    public AudioClip stageGlitchSfx;
    public AudioClip hazardGlitchLoop;
    public AudioClip blueScreenEndingSfx;
    public AudioClip repairSuccessSfx;

    [Header("Mix")]
    [Range(0f, 1f)] public float masterVolume = 0.75f;
    [Range(0f, 1f)] public float pulseVolume = 0.8f;
    [Range(0f, 1f)] public float dangerVolume = 0.8f;
    [Range(0f, 1f)] public float glitchVolume = 0.8f;
    [Range(0f, 1f)] public float stageGlitchSfxVolume = 0.8f;
    [Range(0f, 1f)] public float hazardGlitchMaxVolume = 0.7f;
    public float pulseCpu = 30f;
    public float dangerCpu = 60f;
    public float glitchCpu = 80f;
    public float cpuFadeRange = 8f;
    public float volumeFadeSeconds = 1.25f;
    public float endingFadeSeconds = 0.8f;

    readonly AudioSource[] sources = new AudioSource[4];
    readonly float[] layerTargets = new float[4];
    AudioSource stageSfxSource;
    AudioSource hazardGlitchSource;
    AudioSource endingSfxSource;
    CPUStage previousStage;
    bool endingFade;

    void Awake()
    {
        if (cpuManager == null)
            cpuManager = FindFirstObjectByType<CPUManager>();
        if (cpuManager != null)
            previousStage = cpuManager.CurrentStage;

        AudioClip[] clips = { baseTrack, pulseTrack, dangerTrack, glitchTrack };
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clips[i];
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = i == 0 ? masterVolume : 0f;
            sources[i] = source;
            if (clips[i] != null)
                clips[i].LoadAudioData();
        }

        stageSfxSource = gameObject.AddComponent<AudioSource>();
        stageSfxSource.playOnAwake = false;
        stageSfxSource.spatialBlend = 0f;
        if (stageGlitchSfx != null)
            stageGlitchSfx.LoadAudioData();

        hazardGlitchSource = gameObject.AddComponent<AudioSource>();
        hazardGlitchSource.clip = hazardGlitchLoop;
        hazardGlitchSource.playOnAwake = false;
        hazardGlitchSource.loop = true;
        hazardGlitchSource.spatialBlend = 0f;
        hazardGlitchSource.volume = 0f;
        if (hazardGlitchLoop != null)
            hazardGlitchLoop.LoadAudioData();

        endingSfxSource = gameObject.AddComponent<AudioSource>();
        endingSfxSource.playOnAwake = false;
        endingSfxSource.spatialBlend = 0f;
        if (blueScreenEndingSfx != null) blueScreenEndingSfx.LoadAudioData();
        if (repairSuccessSfx != null) repairSuccessSfx.LoadAudioData();
    }

    void OnEnable()
    {
        if (cpuManager != null)
            cpuManager.OnCpuStageChanged += HandleCpuStageChanged;
    }

    void OnDisable()
    {
        if (cpuManager != null)
            cpuManager.OnCpuStageChanged -= HandleCpuStageChanged;
    }

    void Start()
    {
        // One scheduled DSP timestamp gives every stem the exact same first sample.
        double startTime = AudioSettings.dspTime + 0.1d;
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].clip != null)
                sources[i].PlayScheduled(startTime);
        }
        if (hazardGlitchSource.clip != null)
            hazardGlitchSource.PlayScheduled(startTime);
    }

    void Update()
    {
        if (endingFade)
        {
            FadeOutGameAudio();
            return;
        }

        float cpu = cpuManager != null ? cpuManager.CurrentCpu : 0f;
        layerTargets[0] = masterVolume;
        layerTargets[1] = masterVolume * pulseVolume * LayerWeight(cpu, pulseCpu);
        layerTargets[2] = masterVolume * dangerVolume * LayerWeight(cpu, dangerCpu);
        layerTargets[3] = masterVolume * glitchVolume * LayerWeight(cpu, glitchCpu);

        float maxDelta = volumeFadeSeconds > 0f
            ? Time.unscaledDeltaTime / volumeFadeSeconds
            : 1f;

        for (int i = 0; i < sources.Length; i++)
            sources[i].volume = Mathf.MoveTowards(sources[i].volume, layerTargets[i], maxDelta);

        // Silent at 50%, then grows continuously to its configured maximum at 90%.
        float hazardTarget = hazardGlitchMaxVolume * Mathf.InverseLerp(50f, 90f, cpu);
        hazardGlitchSource.volume =
            Mathf.MoveTowards(hazardGlitchSource.volume, hazardTarget, maxDelta);
    }

    public void PlayBlueScreenEnding()
    {
        BeginEnding(blueScreenEndingSfx, true);
    }

    public void PlayRepairSuccess()
    {
        BeginEnding(repairSuccessSfx, false);
    }

    void BeginEnding(AudioClip endingClip, bool stopGameAudioImmediately)
    {
        if (endingFade) return;
        endingFade = true;
        if (stopGameAudioImmediately)
            StopGameAudio();
        if (endingSfxSource != null && endingClip != null)
            endingSfxSource.PlayOneShot(endingClip);
    }

    void StopGameAudio()
    {
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].volume = 0f;
            sources[i].Stop();
        }
        if (hazardGlitchSource != null)
        {
            hazardGlitchSource.volume = 0f;
            hazardGlitchSource.Stop();
        }
    }

    void FadeOutGameAudio()
    {
        float maxDelta = endingFadeSeconds > 0f
            ? Time.unscaledDeltaTime / endingFadeSeconds
            : 1f;
        for (int i = 0; i < sources.Length; i++)
            sources[i].volume = Mathf.MoveTowards(sources[i].volume, 0f, maxDelta);
        if (hazardGlitchSource != null)
            hazardGlitchSource.volume =
                Mathf.MoveTowards(hazardGlitchSource.volume, 0f, maxDelta);
    }

    float LayerWeight(float cpu, float threshold)
    {
        float halfRange = Mathf.Max(0.01f, cpuFadeRange) * 0.5f;
        float t = Mathf.InverseLerp(threshold - halfRange, threshold + halfRange, cpu);
        return t * t * (3f - 2f * t);
    }

    void HandleCpuStageChanged(CPUStage newStage)
    {
        if ((int)newStage > (int)previousStage && stageSfxSource != null && stageGlitchSfx != null)
            stageSfxSource.PlayOneShot(stageGlitchSfx, stageGlitchSfxVolume);
        previousStage = newStage;
    }
}
