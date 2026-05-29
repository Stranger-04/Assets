Shader "Custom/Shells"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _TileCount("Tile Count", Float) = 8
        _Seed("Seed", Float) = 1
        _MinScale("Min Scale", Float) = 0.65
        _MaxScale("Max Scale", Float) = 1
        _RotateAmount("Rotate Amount", Float) = 1
        _OffsetAmount("Offset Amount", Float) = 0.2
        _Cutoff("Alpha Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _TileCount;
                float _Seed;
                float _MinScale;
                float _MaxScale;
                float _RotateAmount;
                float _OffsetAmount;
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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

            float2 Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(float2(p.x * p.y, p.x + p.y));
            }

            float2 RotateUV(float2 uv, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(
                    uv.x * c - uv.y * s,
                    uv.x * s + uv.y * c
                );
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float tileCount = max(_TileCount, 1.0);
                float2 tiledUV = input.uv * tileCount;
                float2 baseCell = floor(tiledUV);

                float4 bestSample = float4(0.0, 0.0, 0.0, 0.0);
                float bestPriority = -1.0;
                bool found = false;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = baseCell + float2(x, y);
                        float2 cellSeedUV = cell + _Seed;
                        float2 rnd = Hash21(cellSeedUV);

                        float scale = lerp(_MinScale, _MaxScale, rnd.x);
                        float angle = (rnd.y - 0.5) * 6.2831853 * _RotateAmount;
                        float2 offset = (Hash21(cellSeedUV + 17.0) - 0.5) * _OffsetAmount;
                        float priority = Hash21(cellSeedUV + 31.0).x;

                        float2 center = cell + 0.5 + offset;
                        float2 localUV = tiledUV - center;
                        localUV = RotateUV(localUV, -angle);
                        localUV /= max(scale, 1e-4);
                        localUV += 0.5;

                        bool inside = all(localUV >= 0.0) && all(localUV <= 1.0);
                        if (inside)
                        {
                            if (!found || priority > bestPriority)
                            {
                                bestPriority = priority;
                                bestSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, localUV);
                                found = true;
                            }
                        }
                    }
                }

                clip(bestSample.a * _BaseColor.a - _Cutoff);
                return bestSample * _BaseColor;
            }
            ENDHLSL
        }
    }

    FallBack Off

}
