Shader "Hidden/PCSS/ScreenSpaceShadow"
{
    Properties { }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D_X(_PCSS_ShadowCacheTex);
    SAMPLER(sampler_PCSS_ShadowCacheTex);

    int      _CascadeCount;
    float4   _CascadeSplits;
    float4x4 _CascadeLightVP[4];
    float4   _CascadeAtlasOffset[4];
    float4   _CascadeHalfWidth;
    float4   _CascadeZDistance;
    int      _PCSS_BlockerSamples;
    int      _PCSS_PCFSamples;
    float    _PCSS_LightSize;
    float    _PCSS_Softness;
    float3   _LightDirection; // 表面→光源方向（C# 设置）
    int      _PCSS_DebugMode;

    // Vogel disk (32 samples)
    static const float2 VogelDisk[32] = {
        float2( 0.0284,  0.1087), float2( 0.1766,  0.0676), float2(-0.1744,  0.1780), float2( 0.0210, -0.2479),
        float2(-0.2168, -0.1762), float2(-0.0368,  0.3060), float2( 0.1458, -0.2791), float2( 0.2927,  0.0888),
        float2(-0.3105,  0.0397), float2( 0.1648,  0.3230), float2(-0.0347, -0.4025), float2(-0.2360, -0.3356),
        float2(-0.4092, -0.0566), float2(-0.2305,  0.3875), float2( 0.1669, -0.4405), float2( 0.3942, -0.2468),
        float2(-0.3678, -0.2440), float2( 0.3264,  0.2472), float2(-0.3033,  0.3505), float2( 0.0359, -0.5256),
        float2( 0.4130, -0.4186), float2(-0.3752, -0.3988), float2( 0.1697,  0.4868), float2( 0.3826,  0.3261),
        float2(-0.4990, -0.1348), float2( 0.4773, -0.1076), float2(-0.2348, -0.5315), float2(-0.3548,  0.4740),
        float2( 0.4941, -0.2563), float2(-0.4674, -0.3919), float2(-0.0510,  0.5771), float2( 0.2900, -0.5407)
    };
    float Random1D(float2 seed) { return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453); }
    float2 RotateVector(float2 v, float angle) { float s=sin(angle),c=cos(angle); return float2(v.x*c-v.y*s, v.x*s+v.y*c); }

    // ── 双线性采样 + 级联 tile 边界 clamp ──
    float BilinearSampleAtlas(float2 atlasUV, float2 tileMin, float2 tileMax)
    {
        float texelSize = 1.0 / 2048.0;
        float halfTexel = 0.5 * texelSize;
        float2 uv = clamp(atlasUV, tileMin + halfTexel, tileMax - halfTexel);
        // 计算最近 4 个 texel 中心的 UV
        float2 coord = uv / texelSize - 0.5;
        float2 f = frac(coord);
        int2 base = int2(coord);
        float s00 = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, (base + int2(0,0) + 0.5) * texelSize).r;
        float s10 = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, (base + int2(1,0) + 0.5) * texelSize).r;
        float s01 = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, (base + int2(0,1) + 0.5) * texelSize).r;
        float s11 = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, (base + int2(1,1) + 0.5) * texelSize).r;

        return lerp(lerp(s00, s10, f.x), lerp(s01, s11, f.x), f.y);
    }

    half4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float rawDepth = SampleSceneDepth(uv);
        float linear01 = Linear01Depth(rawDepth, _ZBufferParams);
        if (linear01 > 0.9999) return half4(1, 0, 0, 1);

        // ── 级联选择（仍用世界空间距离）──
        float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        float distCam = distance(positionWS, _WorldSpaceCameraPos);
        int ci = 0;
        if (distCam > _CascadeSplits.x) ci = 1;
        if (distCam > _CascadeSplits.y) ci = 2;
        if (distCam > _CascadeSplits.z) ci = 3;
        ci = min(ci, _CascadeCount - 1);

        float4x4 cascadeVP = _CascadeLightVP[ci];
        float4 shadowCoord = mul(cascadeVP, float4(positionWS, 1.0));
        float2 shadowUV = shadowCoord.xy / shadowCoord.w;
        float  receiverDepth = shadowCoord.z / shadowCoord.w;

        if (shadowUV.x < 0.0 || shadowUV.x > 1.0 || shadowUV.y < 0.0 || shadowUV.y > 1.0)
            return half4(1, 1, 1, 1);

        float4 atlas = _CascadeAtlasOffset[ci];
        float2 atlasUV = shadowUV * atlas.z + atlas.xy;
        float2 tileMin = atlas.xy;
        float2 tileMax = atlas.xy + atlas.zz; // z = scale

        // ── 数值路径差异补偿 ──
        // Caster 和 receiver 走不同浮点计算路径，同一世界点会产生 ULP 级偏差。
        // epsilon ≈ 1/4096 = 半个 shadow map texel 的深度。
        float epsilon = 1.0 / 4096.0;

        // ── 硬阴影 ──
        float sd0 = BilinearSampleAtlas(atlasUV, tileMin, tileMax);
        #if UNITY_REVERSED_Z
            float hardShadow = (sd0 > receiverDepth + epsilon) ? 0.0 : 1.0;
        #else
            float hardShadow = (sd0 < receiverDepth - epsilon) ? 0.0 : 1.0;
        #endif

        // ── Blocker Search ──
        float halfW0  = _CascadeHalfWidth.x;
        float halfWci = ci==0 ? _CascadeHalfWidth.x : (ci==1 ? _CascadeHalfWidth.y : (ci==2 ? _CascadeHalfWidth.z : _CascadeHalfWidth.w));
        float searchPixels = 20.0 * halfW0 / halfWci;
        float searchRadiusUV = searchPixels / 2048.0;

        // ── 级联参数 ──
        float zDistCi = ci==0 ? _CascadeZDistance.x : (ci==1 ? _CascadeZDistance.y : (ci==2 ? _CascadeZDistance.z : _CascadeZDistance.w));

        // ── 斜面深度偏移 ──
        // 偏移量与 PCF 核大小成正比：核小（接触）→bias→0，核大→bias 增大。
        // 偏移只作用于 PCF，不影响 blocker search 和硬阴影。
        float3 normalWS = SampleSceneNormals(uv);
        float NdotL = abs(dot(normalWS, _LightDirection));
        float gradient = sqrt(1.0 - NdotL * NdotL) / max(NdotL, 0.001);
        float baseBias = (20.0 * halfW0 / 1024.0) * gradient / max(zDistCi, 0.001);

        // ── Blocker Search（用搜索半径的固定比例偏移）──
        #if UNITY_REVERSED_Z
            float biasedReceiver = receiverDepth + epsilon;
        #else
            float biasedReceiver = receiverDepth - epsilon;
        #endif

        float randomAngle = Random1D(uv + _Time.yy) * 6.28318;

        float blockerDepth = 0.0;
        float blockerCount = 0.001;
        int n = min(_PCSS_BlockerSamples, 32);
        for (int i = 0; i < n; i++)
        {
            float2 offset = RotateVector(VogelDisk[i], randomAngle) * searchRadiusUV;
            float sd = BilinearSampleAtlas(atlasUV + offset, tileMin, tileMax);
            #if UNITY_REVERSED_Z
                if (sd > biasedReceiver) { blockerDepth += sd; blockerCount += 1.0; }
            #else
                if (sd < biasedReceiver) { blockerDepth += sd; blockerCount += 1.0; }
            #endif
        }
        float avgBlocker = blockerDepth / blockerCount;
        float blockerRatio = blockerCount / (float)n;

        // ── Penumbra 估算 ──
        float depthDiff = abs(avgBlocker - receiverDepth);
        float penumbraWS = depthDiff * zDistCi / max(halfWci, 0.001) * _PCSS_LightSize * _PCSS_Softness;
        float penumbraPixels = min(penumbraWS * 10.0, 100.0 / max(halfWci, 0.001));

        // ── 变核 PCF（偏移 ∝ 实际 PCF 核大小）──
        float pcfBias = baseBias * (penumbraPixels / 100.0) * _PCSS_Softness;
        #if UNITY_REVERSED_Z
            float pcfReceiver = receiverDepth + epsilon + pcfBias;
        #else
            float pcfReceiver = receiverDepth - epsilon - pcfBias;
        #endif

        float shadow;
        // if (blockerRatio < 0.06)
        // {
        //     shadow = hardShadow; // 接触硬化
        // }
        // else
        // {
            float pcfRadiusUV = max(penumbraPixels, 1.0) / 2048.0;
            shadow = 0.0;
            int nPcf = min(_PCSS_PCFSamples, 32);
            float pcfAngle = Random1D(uv.yx + _Time.yy) * 6.28318;
            for (int i = 0; i < nPcf; i++)
            {
                float2 offset = RotateVector(VogelDisk[i], pcfAngle) * pcfRadiusUV;
                float sd = BilinearSampleAtlas(atlasUV + offset, tileMin, tileMax);
                #if UNITY_REVERSED_Z
                    shadow += (sd > pcfReceiver) ? 0.0 : 1.0;
                #else
                    shadow += (sd < pcfReceiver) ? 0.0 : 1.0;
                #endif
            }
            shadow /= (float)nPcf;
        // }

        // ── Debug ──
        if (_PCSS_DebugMode == 1) return half4(blockerRatio, blockerRatio, blockerRatio, 1);
        if (_PCSS_DebugMode == 2) return half4(hardShadow, hardShadow, hardShadow, 1);
        if (_PCSS_DebugMode == 3) return half4(avgBlocker, avgBlocker, avgBlocker, 1);
        if (_PCSS_DebugMode == 4) { float p = penumbraPixels / 30.0; return half4(p, p, p, 1); }
        if (_PCSS_DebugMode == 5) { float s = pcfBias / 0.01; return half4(s, s, s, 1); }
        return half4(shadow, shadow, shadow, 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PCSS_Hard"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma enable_d3d11_debug_symbols
            ENDHLSL
        }
    }
}
