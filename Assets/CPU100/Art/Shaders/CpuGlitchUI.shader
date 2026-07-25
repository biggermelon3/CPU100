Shader "CPU100/CpuGlitchUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "CpuGlitchUI"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float2 _ScanLineJitter;
            float2 _VerticalJump;
            float _HorizontalShake;
            float2 _ColorDrift;
            float _BlockStrength;
            float _BlockSize;
            float _Seed;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233)) + _Seed) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float scanStrength = saturate(
                    _ScanLineJitter.x * 24.0 + (1.0 - _ScanLineJitter.y) * 0.35);
                float analogStrength = saturate(
                    scanStrength + _VerticalJump.x * 2.0 +
                    _HorizontalShake * 3.0 + _ColorDrift.x * 8.0);

                float lineId = floor(uv.y * 260.0);
                float lineNoise = Hash(float2(lineId, floor(_Seed * 0.1)));
                float fineLine = step(0.82, lineNoise) * scanStrength;
                float brightBand = step(0.965, lineNoise) * analogStrength;

                float2 blockId = floor(
                    uv * float2(_BlockSize, _BlockSize * 0.5625));
                float blockNoise = Hash(blockId);
                float brokenBlock = step(
                    1.0 - _BlockStrength * 0.38, blockNoise);

                float rgbChoice = Hash(float2(lineId, _Seed * 0.37));
                half3 bandColor = rgbChoice < 0.5
                    ? half3(0.1, 0.85, 1.0)
                    : half3(1.0, 0.08, 0.65);
                half3 blockColor = lerp(
                    half3(0.05, 0.2, 0.8),
                    half3(1.0, 0.15, 0.65),
                    Hash(blockId + 17.0));

                float alpha = fineLine * 0.11 +
                    brightBand * 0.3 + brokenBlock * 0.48;
                half3 color = bandColor * (fineLine + brightBand) +
                    blockColor * brokenBlock;
                color /= max(1.0, fineLine + brightBand + brokenBlock);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
