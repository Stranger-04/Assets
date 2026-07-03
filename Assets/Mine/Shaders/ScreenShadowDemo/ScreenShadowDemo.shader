Shader "Mine/ScreenShadowDemo"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.8, 0.8, 0.8, 1)
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.6

        [Header(Debug)]
        [Toggle(_SS_DEBUG)] _SSDebug ("Debug Screen-Space Shadow Map", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half  _ShadowIntensity;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ════════════════════════════════════════════════════════════
        //  Pass 0: Forward — 屏幕空间阴影采样
        // ════════════════════════════════════════════════════════════
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature_local _SS_DEBUG

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                Light mainLight = GetMainLight();

                // ═══════════ 阴影采样 — 统一 API，URP 内部处理 SCREEN / CASCADE 分叉 ═══════════
                // TransformWorldToShadowCoord:
                //   SCREEN 路径 → 返回 NDC 坐标 → GetMainLight → SampleScreenSpaceShadowmap
                //   CASCADE 路径 → 返回级联 ShadowMap UV → GetMainLight → SampleShadowmap
                // 采样 _ScreenSpaceShadowmapTexture 时 URP 使用 sampler_PointClamp
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLightShadow = GetMainLight(shadowCoord);
                float shadowAtten = lerp(1.0 - _ShadowIntensity, 1.0, mainLightShadow.shadowAttenuation);

                // ── 光照 ──
                float NdotL = saturate(dot(N, mainLight.direction));
                half3 diffuse = mainLight.color * _BaseColor.rgb * NdotL * shadowAtten;
                half3 ambient = SampleSH(N) * _BaseColor.rgb * 0.1;
                half3 finalColor = diffuse + ambient;

                // ── Debug：可视化屏幕空间阴影贴图 ──
                #if defined(_SS_DEBUG)
                    float4 debugCoord = ComputeScreenPos(input.positionCS);
                    // 必须与 SampleScreenSpaceShadowmap 一致：除以 w + stereo transform
                    debugCoord.xy /= debugCoord.w;
                    debugCoord.xy = UnityStereoTransformScreenSpaceTex(debugCoord.xy);
                    float ssDebug = SAMPLE_TEXTURE2D_X(_ScreenSpaceShadowmapTexture, sampler_PointClamp, debugCoord.xy).r;
                    return half4(ssDebug, ssDebug, ssDebug, 1);
                #endif

                return half4(finalColor, 1);
            }
            ENDHLSL
        }

        // ════════════════════════════════════════════════════════════
        //  Pass 1: ShadowCaster
        // ════════════════════════════════════════════════════════════
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
