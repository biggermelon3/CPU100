// Fullscreen CPU glitch for the URP 2D renderer (FullScreenPassRendererFeature).
// Analog part (scanline jitter / vertical jump / horizontal shake / color drift)
// is a port of keijiro/KinoGlitch AnalogGlitch; the block corruption approximates
// DigitalGlitch with in-shader hash noise instead of a CPU-updated noise texture.
// All time/seed values are fed from CpuGlitchController so the shader needs no _Time.
Shader "CPU100/CpuGlitch"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "CpuGlitch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float2 _ScanLineJitter;   // (displacement, threshold)
            float2 _VerticalJump;     // (amount, accumulated jump time)
            float  _HorizontalShake;
            float2 _ColorDrift;       // (amount, time)
            float  _BlockStrength;    // 0..1 digital block corruption
            float  _BlockSize;        // blocks across the screen width
            float  _Seed;             // per-frame random from C#

            float nrand(float x, float y)
            {
                return frac(sin(dot(float2(x, y), float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Digital block corruption: displace a random subset of screen blocks.
                if (_BlockStrength > 0.0001)
                {
                    float2 block = floor(uv * float2(_BlockSize, _BlockSize * 0.5625));
                    float r = nrand(block.x * 0.037 + _Seed, block.y * 0.113 + _Seed * 1.7);
                    if (r < _BlockStrength * 0.35)
                    {
                        float2 shift = float2(nrand(r, _Seed) - 0.5, nrand(_Seed, r) - 0.5);
                        uv += shift * 0.08 * _BlockStrength;
                    }
                }

                float u = uv.x;
                float v = uv.y;

                // Scan line jitter.
                float jitter = nrand(v, _Seed) * 2.0 - 1.0;
                jitter *= step(_ScanLineJitter.y, abs(jitter)) * _ScanLineJitter.x;

                // Vertical jump.
                float jump = lerp(v, frac(v + _VerticalJump.y), _VerticalJump.x);

                // Horizontal shake.
                float shake = (nrand(_Seed, 2.0) - 0.5) * _HorizontalShake;

                // Color drift.
                float drift = sin(jump + _ColorDrift.y) * _ColorDrift.x;

                float2 uv1 = frac(float2(u + jitter + shake, jump));
                float2 uv2 = frac(float2(u + jitter + shake + drift, jump));
                half4 src1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv1);
                half4 src2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv2);
                return half4(src1.r, src2.g, src1.b, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
