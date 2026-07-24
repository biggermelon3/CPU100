using UnityEngine;

public class InputInterferenceController : MonoBehaviour
{
    public CPUManager cpuManager;
    public Transform cameraTransform;          // fallback Camera.main
    public GameObject warningOverlay;          // active during reversal pre-warning + reversal
    public CanvasGroup glitchOverlayGroup;     // alpha flicker at Critical

    public bool Frozen { get; set; }
    public bool InputBlocked { get { return blockRemaining > 0f; } }
    public bool InputReversed { get { return reversalPhase == ReversalPhase.Reversing; } }

    private enum ReversalPhase { Idle, Warning, Reversing }

    private const float WarningDuration = 0.5f;
    private const float ReversalDuration = 1.5f;
    private const float MediumBlockDuration = 0.1f;
    private const float CriticalBlockDuration = 0.15f;
    private const float CameraJitterAmplitude = 0.05f;
    private const float MaxGlitchAlpha = 0.35f;
    private const float GlitchFlickerInterval = 0.05f;

    private CPUStage lastStage = CPUStage.Normal;

    private float blockRemaining;
    private float nextBlockIn;

    private ReversalPhase reversalPhase = ReversalPhase.Idle;
    private float phaseRemaining;
    private float nextReversalIn;

    private float flickerTimer;
    private Vector3 cameraBaseLocalPos;
    private bool cameraDisturbed;
    private bool clearedWhileFrozen;

    private void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null) cameraTransform = cam.transform;
        }
        if (cameraTransform != null) cameraBaseLocalPos = cameraTransform.localPosition;

        if (warningOverlay != null) warningOverlay.SetActive(false);
        if (glitchOverlayGroup != null) glitchOverlayGroup.alpha = 0f;
    }

    private void OnDisable()
    {
        ClearAllInterference();
    }

    private void Update()
    {
        if (Frozen)
        {
            if (!clearedWhileFrozen)
            {
                ClearAllInterference();
                clearedWhileFrozen = true;
            }
            return;
        }
        clearedWhileFrozen = false;

        CPUStage stage = cpuManager != null ? cpuManager.CurrentStage : CPUStage.Normal;
        if (stage != lastStage)
        {
            OnStageChanged(stage);
            lastStage = stage;
        }

        float dt = Time.deltaTime;
        bool blocksEnabled = stage == CPUStage.MediumLoad || stage == CPUStage.Critical;
        bool reversalsEnabled = stage == CPUStage.HeavyLoad || stage == CPUStage.Critical;

        if (blockRemaining > 0f) blockRemaining -= dt;

        if (blocksEnabled)
        {
            nextBlockIn -= dt;
            if (nextBlockIn <= 0f)
            {
                bool critical = stage == CPUStage.Critical;
                blockRemaining = critical ? CriticalBlockDuration : MediumBlockDuration;
                nextBlockIn = critical ? Random.Range(2f, 4f) : Random.Range(4f, 8f);
            }
        }

        if (reversalsEnabled)
        {
            switch (reversalPhase)
            {
                case ReversalPhase.Idle:
                    nextReversalIn -= dt;
                    if (nextReversalIn <= 0f)
                    {
                        reversalPhase = ReversalPhase.Warning;
                        phaseRemaining = WarningDuration;
                        SetWarningVisible(true);
                    }
                    break;

                case ReversalPhase.Warning:
                    phaseRemaining -= dt;
                    if (phaseRemaining <= 0f)
                    {
                        // Warning overlay stays on for the whole reversal.
                        reversalPhase = ReversalPhase.Reversing;
                        phaseRemaining = ReversalDuration;
                    }
                    break;

                case ReversalPhase.Reversing:
                    phaseRemaining -= dt;
                    if (phaseRemaining <= 0f)
                    {
                        reversalPhase = ReversalPhase.Idle;
                        SetWarningVisible(false);
                        nextReversalIn = stage == CPUStage.Critical ? Random.Range(5f, 8f) : Random.Range(8f, 12f);
                    }
                    break;
            }
        }

        if (stage == CPUStage.Critical)
        {
            if (cameraTransform != null)
            {
                cameraTransform.localPosition = cameraBaseLocalPos + new Vector3(
                    Random.Range(-CameraJitterAmplitude, CameraJitterAmplitude),
                    Random.Range(-CameraJitterAmplitude, CameraJitterAmplitude),
                    0f);
                cameraDisturbed = true;
            }

            if (glitchOverlayGroup != null)
            {
                flickerTimer -= dt;
                if (flickerTimer <= 0f)
                {
                    flickerTimer = GlitchFlickerInterval;
                    glitchOverlayGroup.alpha = Random.Range(0f, MaxGlitchAlpha);
                }
            }
        }
    }

    private void OnStageChanged(CPUStage stage)
    {
        // Reset active interference on every stage boundary so durations and
        // intervals always match the new stage's rules.
        blockRemaining = 0f;
        reversalPhase = ReversalPhase.Idle;
        SetWarningVisible(false);

        switch (stage)
        {
            case CPUStage.MediumLoad:
                nextBlockIn = Random.Range(4f, 8f);
                break;
            case CPUStage.HeavyLoad:
                nextReversalIn = Random.Range(8f, 12f);
                break;
            case CPUStage.Critical:
                nextBlockIn = Random.Range(2f, 4f);
                nextReversalIn = Random.Range(5f, 8f);
                break;
        }

        if (stage != CPUStage.Critical)
        {
            RestoreCamera();
            if (glitchOverlayGroup != null) glitchOverlayGroup.alpha = 0f;
        }
    }

    private void ClearAllInterference()
    {
        blockRemaining = 0f;
        reversalPhase = ReversalPhase.Idle;
        SetWarningVisible(false);
        RestoreCamera();
        if (glitchOverlayGroup != null) glitchOverlayGroup.alpha = 0f;
    }

    private void RestoreCamera()
    {
        if (cameraDisturbed && cameraTransform != null)
            cameraTransform.localPosition = cameraBaseLocalPos;
        cameraDisturbed = false;
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningOverlay != null && warningOverlay.activeSelf != visible)
            warningOverlay.SetActive(visible);
    }
}
