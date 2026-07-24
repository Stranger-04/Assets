Shader "Mine/PCSS/DebugPlane"
{
    Properties
    {
        [Toggle] _ShowUnity ("Show Unity CSM", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D_X(_PCSS_ShadowCacheTex);
            SAMPLER(sampler_PCSS_ShadowCacheTex);

            // 直接采样 Unity 内置 Shadow RT 的原始深度（非比较采样）
            float _ShowUnity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                if (_ShowUnity > 0.5)
                {
                    // Unity 内置 Shadow RT 原始深度（非比较采样，用 LinearClamp）
                    float rawDepth = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sampler_LinearClamp, input.uv).r;
                    return float4(rawDepth, rawDepth, rawDepth, 1);
                }
                else
                {
                    float depth = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, input.uv).r;
                    return float4(depth, depth, depth, 1);
                }
            }
            ENDHLSL
        }
    }
}
