Shader "Mine/PCSS/ScreenSpacePCSS"
{
    Properties
    {
        _PCSS_LightSize("Light Size", Range(0.1, 10)) = 1.0
        _PCSS_BlockerSamples("Blocker Samples", Range(4, 64)) = 16
        _PCSS_BlockerRadius("Blocker Radius", Range(1, 32)) = 8
        _PCSS_PCFSamples("PCF Samples", Range(4, 64)) = 16
        _PCSS_Softness("Softness", Range(0.1, 2)) = 1.0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    float  _PCSS_LightSize;
    int    _PCSS_BlockerSamples;
    int    _PCSS_BlockerRadius;
    int    _PCSS_PCFSamples;
    float  _PCSS_Softness;

    static const float2 VogelDisk[32] =
    {
        float2(0.0284,  0.1087),  float2( 0.1766,  0.0676),  float2(-0.1744,  0.1780),  float2( 0.0210, -0.2479),
        float2(-0.2168, -0.1762),  float2(-0.0368,  0.3060),  float2( 0.1458, -0.2791),  float2( 0.2927,  0.0888),
        float2(-0.3105,  0.0397),  float2( 0.1648,  0.3230),  float2(-0.0347, -0.4025),  float2(-0.2360, -0.3356),
        float2(-0.4092, -0.0566),  float2(-0.2305,  0.3875),  float2( 0.1669, -0.4405),  float2( 0.3942, -0.2468),
        float2(-0.3678, -0.2440),  float2( 0.3264,  0.2472),  float2(-0.3033,  0.3505),  float2( 0.0359, -0.5256),
        float2( 0.4130, -0.4186),  float2(-0.3752, -0.3988),  float2( 0.1697,  0.4868),  float2( 0.3826,  0.3261),
        float2(-0.4990, -0.1348),  float2( 0.4773, -0.1076),  float2(-0.2348, -0.5315),  float2(-0.3548,  0.4740),
        float2( 0.4941, -0.2563),  float2(-0.4674, -0.3919),  float2(-0.0510,  0.5771),  float2( 0.2900, -0.5407)
    };
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PCSS_Generate"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_PCSS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            float _PCSS_DebugMode; // 0=normal, 1=blockerCount, 2=penumbra, 3=PCF radius

            float4 Frag_PCSS(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                if (rawDepth >= 0.9999) return float4(1, 1, 1, 1);

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                float bias = 0.005;

                Light light = GetMainLight(shadowCoord);
                return float4(light.shadowAttenuation, light.shadowAttenuation,
                    light.shadowAttenuation, 1);
            }

            float4 _Frag_PCSS_Real(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                if (rawDepth >= 0.9999) return float4(1, 1, 1, 1);

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                float receiverDepth = shadowCoord.z;

                float bias = 0.005 * (1.0 + (1.0 - receiverDepth) * 4.0);

                float hardShadow = SAMPLE_TEXTURE2D_SHADOW(
                    _MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, shadowCoord.xyz);
                if (hardShadow > 0.99) return float4(1, 1, 1, 1);

                float searchRadiusUV = (float)_PCSS_BlockerRadius / 2048.0;
                searchRadiusUV *= (0.5 + receiverDepth * 2.0);

                float avgBlockerDepth = 0.0;
                int blockerCount = 0;
                for (int i = 0; i < _PCSS_BlockerSamples; i++)
                {
                    float2 offset = VogelDisk[i] * searchRadiusUV;
                    float3 sampleCoord = float3(shadowCoord.xy + offset, receiverDepth - bias);
                    float isBlocked = SAMPLE_TEXTURE2D_SHADOW(
                        _MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, sampleCoord);
                    if (isBlocked < 0.01)
                    {
                        float blockerD = SAMPLE_TEXTURE2D_LOD(
                            _MainLightShadowmapTexture, sampler_PointClamp,
                            shadowCoord.xy + offset, 0).r;
                        avgBlockerDepth += blockerD;
                        blockerCount++;
                    }
                }

                // Debug visualization
                if (_PCSS_DebugMode > 0.5)
                {
                    if (_PCSS_DebugMode < 1.5)
                        return float4(saturate(blockerCount / 32.0), 0, 0, 1); // red = blocker count
                    if (_PCSS_DebugMode < 2.5)
                        return float4(blockerCount > 0 ? float3(0, 1, 0) : float3(0, 0, 0), 1); // green = any blocker
                    return float4(searchRadiusUV * 500, 0, 0, 1); // red = search radius (scaled)
                }

                if (blockerCount < 2) return float4(0, 0, 0, 1);
                avgBlockerDepth /= (float)blockerCount;

                float penumbra = (receiverDepth - avgBlockerDepth) / max(0.0001, avgBlockerDepth);
                penumbra *= _PCSS_LightSize * _PCSS_Softness * 20.0;
                float pcfRadiusUV = max(searchRadiusUV * 0.5, penumbra / 2048.0);
                pcfRadiusUV = min(pcfRadiusUV, searchRadiusUV * 4.0);

                float shadow = 0.0;
                for (int j = 0; j < _PCSS_PCFSamples; j++)
                {
                    float2 offset = VogelDisk[j] * pcfRadiusUV;
                    float3 sampleCoord = float3(shadowCoord.xy + offset, receiverDepth - bias);
                    shadow += SAMPLE_TEXTURE2D_SHADOW(
                        _MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, sampleCoord);
                }
                shadow /= (float)_PCSS_PCFSamples;
                return float4(shadow, shadow, shadow, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PCSS_DebugDepth"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_Depth

            float4 Frag_Depth(Varyings input) : SV_Target
            {
                float d = Linear01Depth(SampleSceneDepth(input.texcoord), _ZBufferParams);
                return float4(d, d, d, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PCSS_DebugHard"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_Hard
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            float4 Frag_Hard(Varyings input) : SV_Target
            {
                float rawDepth = SampleSceneDepth(input.texcoord);
                if (rawDepth >= 0.9999) return float4(1, 1, 1, 1);
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, rawDepth, UNITY_MATRIX_I_VP);
                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                float s = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,
                    sampler_MainLightShadowmapTexture, shadowCoord.xyz);
                return float4(s, s, s, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PCSS_DebugShadowUV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_UV
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            float4 Frag_UV(Varyings input) : SV_Target
            {
                float rawDepth = SampleSceneDepth(input.texcoord);
                if (rawDepth >= 0.9999) return float4(0, 0, 1, 1);
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, rawDepth, UNITY_MATRIX_I_VP);
                float4 sc = TransformWorldToShadowCoord(worldPos);

                // 直接读取 shadow map depth（非比较模式）——验证纹理是否可访问
                float smDepth = SAMPLE_TEXTURE2D_LOD(_MainLightShadowmapTexture,
                    sampler_PointClamp, sc.xy, 0).r;

                // R=shadowU, G=shadowV, B=shadowMapDepth vs receiverDepth 差值
                float diff = sc.z - smDepth;
                return float4(sc.x, sc.y, abs(diff) * 10, 1);
            }
            ENDHLSL
        }
    }
}
