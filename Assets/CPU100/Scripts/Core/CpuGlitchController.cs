using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the fullscreen CPU glitch material (CPU100/CpuGlitch shader, applied by a
/// FullScreenPassRendererFeature on the 2D renderer). Effects layer in as the CPU
/// climbs past startCpu: scanline jitter and color drift first, then vertical jump,
/// horizontal shake, and digital block corruption near the top. Parameter mapping
/// follows keijiro/KinoGlitch's AnalogGlitch controller.
/// </summary>
public class CpuGlitchController : MonoBehaviour
{
    public CPUManager cpuManager;
    public Material glitchMaterial;
    public Shader uiGlitchShader;
    public float startCpu = 50f;

    static readonly int ScanLineJitterId = Shader.PropertyToID("_ScanLineJitter");
    static readonly int VerticalJumpId = Shader.PropertyToID("_VerticalJump");
    static readonly int HorizontalShakeId = Shader.PropertyToID("_HorizontalShake");
    static readonly int ColorDriftId = Shader.PropertyToID("_ColorDrift");
    static readonly int BlockStrengthId = Shader.PropertyToID("_BlockStrength");
    static readonly int BlockSizeId = Shader.PropertyToID("_BlockSize");
    static readonly int SeedId = Shader.PropertyToID("_Seed");

    float verticalJumpTime;
    float blockGateTimer;
    float blockGate;
    bool wasActive;
    Material uiGlitchMaterial;
    GameObject uiGlitchCanvas;
    bool stopped;

    void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        BuildUiGlitchOverlay();
    }

    void OnEnable()
    {
        stopped = false;
        // Hidden until the CPU crosses startCpu: the UI glitch shader is never fully
        // transparent with default-zero uniforms, so an always-on overlay shows
        // faint scanlines even at 0% CPU.
        if (uiGlitchCanvas != null)
            uiGlitchCanvas.SetActive(false);
        ResetMaterial();
    }

    void OnDisable()
    {
        // The feature references the material ASSET; leaving values behind would
        // keep the editor's scene view glitching after play mode ends.
        ResetMaterial();
    }

    void Update()
    {
        if (stopped || glitchMaterial == null || cpuManager == null) return;

        float t = Mathf.InverseLerp(startCpu, 100f, cpuManager.CurrentCpu);
        if (t <= 0f)
        {
            if (wasActive) ResetMaterial();
            if (uiGlitchCanvas != null && uiGlitchCanvas.activeSelf)
                uiGlitchCanvas.SetActive(false);
            return;
        }
        wasActive = true;
        if (uiGlitchCanvas != null && !uiGlitchCanvas.activeSelf)
            uiGlitchCanvas.SetActive(true);

        // Staggered stack: each Ramp stays 0 until its own threshold, so higher CPU
        // literally means MORE distinct effects, not just stronger ones.
        float scan = t * 0.75f;
        float drift = t * 0.6f;
        float jump = Ramp(t, 0.25f) * 0.09f;
        float shake = Ramp(t, 0.45f) * 0.04f;
        float block = Ramp(t, 0.6f);

        verticalJumpTime += Time.deltaTime * jump * 11.3f;

        // Blocks fire in random bursts that get denser as the CPU climbs.
        blockGateTimer -= Time.deltaTime;
        if (blockGateTimer <= 0f)
        {
            blockGateTimer = Random.Range(0.05f, 0.15f);
            blockGate = Random.value < 0.25f + 0.65f * block ? 1f : 0f;
        }

        float slDisp = 0.002f + Mathf.Pow(scan, 3f) * 0.035f;
        float slThresh = Mathf.Clamp01(1f - scan * 1.2f);
        Vector4 scanParams = new Vector4(slDisp, slThresh, 0f, 0f);
        Vector4 jumpParams = new Vector4(jump, verticalJumpTime, 0f, 0f);
        Vector4 driftParams = new Vector4(drift * 0.04f, Time.time * 606.11f, 0f, 0f);
        float blockStrength = block * blockGate * 0.9f;
        float seed = Random.value * 1000f;
        ApplyParameters(glitchMaterial, scanParams, jumpParams, shake, driftParams,
            blockStrength, seed);
        ApplyParameters(uiGlitchMaterial, scanParams, jumpParams, shake, driftParams,
            blockStrength, seed);
    }

    static float Ramp(float t, float start)
    {
        return Mathf.Clamp01((t - start) / (1f - start));
    }

    void ResetMaterial()
    {
        wasActive = false;
        ResetOneMaterial(glitchMaterial);
        ResetOneMaterial(uiGlitchMaterial);
    }

    /// <summary>Leaves the final blue screen clean after the crash transition.</summary>
    public void StopGlitch()
    {
        stopped = true;
        ResetMaterial();
        if (uiGlitchCanvas != null)
            uiGlitchCanvas.SetActive(false);
    }

    void BuildUiGlitchOverlay()
    {
        if (uiGlitchShader == null)
            uiGlitchShader = Shader.Find("CPU100/CpuGlitchUI");
        if (uiGlitchShader == null) return;

        uiGlitchMaterial = new Material(uiGlitchShader)
        {
            name = "CpuGlitchUI (Runtime)"
        };

        uiGlitchCanvas = new GameObject(
            "CpuGlitchUIOverlay", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = uiGlitchCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1500;

        var imageGo = new GameObject("Glitch", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)imageGo.transform;
        rect.SetParent(uiGlitchCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageGo.GetComponent<Image>();
        image.material = uiGlitchMaterial;
        image.raycastTarget = false;
    }

    static void ApplyParameters(Material material, Vector4 scan, Vector4 jump,
        float shake, Vector4 drift, float block, float seed)
    {
        if (material == null) return;
        material.SetVector(ScanLineJitterId, scan);
        material.SetVector(VerticalJumpId, jump);
        material.SetFloat(HorizontalShakeId, shake);
        material.SetVector(ColorDriftId, drift);
        material.SetFloat(BlockStrengthId, block);
        material.SetFloat(BlockSizeId, 32f);
        material.SetFloat(SeedId, seed);
    }

    static void ResetOneMaterial(Material material)
    {
        if (material == null) return;
        material.SetVector(ScanLineJitterId, new Vector4(0f, 1f, 0f, 0f));
        material.SetVector(VerticalJumpId, Vector4.zero);
        material.SetFloat(HorizontalShakeId, 0f);
        material.SetVector(ColorDriftId, Vector4.zero);
        material.SetFloat(BlockStrengthId, 0f);
    }

    void OnDestroy()
    {
        if (uiGlitchCanvas != null)
            Destroy(uiGlitchCanvas);
        if (uiGlitchMaterial != null)
            Destroy(uiGlitchMaterial);
    }
}
