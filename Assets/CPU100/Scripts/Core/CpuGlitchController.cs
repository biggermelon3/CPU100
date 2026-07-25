using UnityEngine;

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

    void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
    }

    void OnEnable()
    {
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
        if (glitchMaterial == null || cpuManager == null) return;

        float t = Mathf.InverseLerp(startCpu, 100f, cpuManager.CurrentCpu);
        if (t <= 0f)
        {
            if (wasActive) ResetMaterial();
            return;
        }
        wasActive = true;

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
        glitchMaterial.SetVector(ScanLineJitterId, new Vector4(slDisp, slThresh, 0f, 0f));
        glitchMaterial.SetVector(VerticalJumpId, new Vector4(jump, verticalJumpTime, 0f, 0f));
        glitchMaterial.SetFloat(HorizontalShakeId, shake);
        glitchMaterial.SetVector(ColorDriftId, new Vector4(drift * 0.04f, Time.time * 606.11f, 0f, 0f));
        glitchMaterial.SetFloat(BlockStrengthId, block * blockGate * 0.9f);
        glitchMaterial.SetFloat(BlockSizeId, 32f);
        glitchMaterial.SetFloat(SeedId, Random.value * 1000f);
    }

    static float Ramp(float t, float start)
    {
        return Mathf.Clamp01((t - start) / (1f - start));
    }

    void ResetMaterial()
    {
        wasActive = false;
        if (glitchMaterial == null) return;
        glitchMaterial.SetVector(ScanLineJitterId, new Vector4(0f, 1f, 0f, 0f));
        glitchMaterial.SetVector(VerticalJumpId, Vector4.zero);
        glitchMaterial.SetFloat(HorizontalShakeId, 0f);
        glitchMaterial.SetVector(ColorDriftId, Vector4.zero);
        glitchMaterial.SetFloat(BlockStrengthId, 0f);
    }
}
