Shader "Hidden/PCSS/ScreenSpaceShadow"
{
    Properties { }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
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
    float3   _LightDirection;
    int      _PCSS_DebugMode; // 0=final, 1=blockerRatio, 2=hardShadow, 3=penumbra, 4=blockerDepth, 5=receiverDepth

    // Vogel disk (32 samples) — 参考文章
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

    float Random1D(float2 seed) {
        return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
    }
    float2 RotateVector(float2 v, float angle) {
        float s = sin(angle), c = cos(angle);
        return float2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ── Blocker Search（含深度偏移补偿）──
    // 返回：(平均 blocker 深度, blocker 比例)
    float2 SampleBlockerAvgDepth(float biasedReceiver, float2 atlasUV, float searchRadiusUV, float randomAngle)
    {
        float blockerDepth = 0.0;
        float blockerCount = 0.001;
        int n = min(_PCSS_BlockerSamples, 32);
        for (int i = 0; i < n; i++)
        {
            float2 offset = RotateVector(VogelDisk[i], randomAngle) * searchRadiusUV;
            float sd = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, atlasUV + offset).r;
            #if UNITY_REVERSED_Z
                if (sd > biasedReceiver) { blockerDepth += sd; blockerCount += 1.0; }
            #else
                if (sd < biasedReceiver) { blockerDepth += sd; blockerCount += 1.0; }
            #endif
        }
        return float2(blockerDepth / blockerCount, blockerCount / (float)n);
    }

    half4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float rawDepth = SampleSceneDepth(uv);
        float linear01 = Linear01Depth(rawDepth, _ZBufferParams);
        if (linear01 > 0.9999) return half4(1, 0, 0, 1); // sky

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
            return half4(1, 1, 0, 1);

        float4 atlas = _CascadeAtlasOffset[ci];
        float2 atlasUV = shadowUV * atlas.z + atlas.xy;

        // ── 级联参数 ──
        float halfW0  = _CascadeHalfWidth.x;
        float halfWci = ci == 0 ? _CascadeHalfWidth.x : (ci == 1 ? _CascadeHalfWidth.y : (ci == 2 ? _CascadeHalfWidth.z : _CascadeHalfWidth.w));
        float zDist0  = _CascadeZDistance.x;
        float zDistCi = ci == 0 ? _CascadeZDistance.x : (ci == 1 ? _CascadeZDistance.y : (ci == 2 ? _CascadeZDistance.z : _CascadeZDistance.w));

        // ── 深度偏移补偿（大核 PCF 防止斜面自阴影，参考文章）──
        float lightDotY = abs(_LightDirection.y);
        float tanAngle = lightDotY / sqrt(1.0 - lightDotY * lightDotY + 0.0001);
        float texelSizeWS = halfWci / 1024.0; // tileRes = 1024
        float searchPixels = 20.0 * halfW0 / max(halfWci, 0.001);
        float deltaZ_WS = searchPixels * texelSizeWS / max(tanAngle, 0.001);
        float deltaZ_LS = deltaZ_WS / max(zDistCi, 0.001);

        // ── Penumbra Mask（参考文章：3×3 邻域快速判定半影区）──
        // 全屏 PCSS 太贵。先用固定世界空间偏移扫描 3×3 邻域：
        //   9 个邻居全部 shadowed → 直接返回 0（全影）
        //   9 个邻居全部 lit     → 直接返回 1（全亮）
        //   否则                 → 在半影区 → 继续 PCSS
        float neighborLit = 0.0;
        float maskOffsetWS = 0.2;
        float maskOffsetPixels = maskOffsetWS / max(texelSizeWS, 0.0001);
        float maskRadiusUV = maskOffsetPixels / 2048.0;
        float deltaZ_WS_mask = maskOffsetWS / max(tanAngle, 0.001);
        float deltaZ_LS_mask = deltaZ_WS_mask / max(zDistCi, 0.001);
        for (int ni = -1; ni <= 1; ni++)
        {
            for (int nj = -1; nj <= 1; nj++)
            {
                float2 nUV = atlasUV + float2(ni, nj) * maskRadiusUV;
                float nsd = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, nUV).r;
                #if UNITY_REVERSED_Z
                    neighborLit += (nsd < receiverDepth + deltaZ_LS_mask) ? 1.0 : 0.0;
                #else
                    neighborLit += (nsd > receiverDepth + deltaZ_LS_mask) ? 1.0 : 0.0;
                #endif
            }
        }
        if (neighborLit < 0.5) return half4(0, 0, 0, 1);  // 全影
        if (neighborLit > 8.5) return half4(1, 1, 1, 1);  // 全亮

        // ── Blocker Search ──
        float searchRadiusUV = searchPixels / 2048.0;
        float randomAngle = Random1D(uv + _Time.yy) * 6.28318;
        float biasedReceiver = receiverDepth + deltaZ_LS;
        float2 blocker = SampleBlockerAvgDepth(biasedReceiver, atlasUV, searchRadiusUV, randomAngle);
        float avgBlockerDepth = blocker.x;
        float blockerRatio = blocker.y;

        // ── Penumbra 估算 ──
        float depthDiff = abs(avgBlockerDepth - receiverDepth);
        float penumbra = depthDiff * zDistCi / max(halfWci, 0.001);
        penumbra *= _PCSS_LightSize * _PCSS_Softness;
        float penumbraPixels = penumbra * 10.0;
        penumbraPixels = min(penumbraPixels, 100.0 / max(halfWci, 0.001));

        // ── 硬阴影参考值（单采样，无 bias）──
        // 斜率偏移是为大核搜索/PCF 设计的，硬阴影单点比较不需要。
        float sd0 = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, atlasUV).r;
        float hardShadow;
        #if UNITY_REVERSED_Z
            hardShadow = (sd0 > receiverDepth) ? 0.0 : 1.0;
        #else
            hardShadow = (sd0 < receiverDepth) ? 0.0 : 1.0;
        #endif

        // ── 接触硬化 ──
        float shadow;
        if (blockerRatio < 0.06)
        {
            shadow = hardShadow;
        }
        else
        {
            float pcfRadiusUV = max(penumbraPixels, 1.0) / 2048.0;
            shadow = 0.0;
            int nPcf = min(_PCSS_PCFSamples, 32);
            float pcfRandomAngle = Random1D(uv.yx + _Time.yy) * 6.28318;
            for (int i = 0; i < nPcf; i++)
            {
                float2 offset = RotateVector(VogelDisk[i], pcfRandomAngle) * pcfRadiusUV;
                float sd = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, atlasUV + offset).r;
                #if UNITY_REVERSED_Z
                    shadow += (sd > biasedReceiver) ? 0.0 : 1.0;
                #else
                    shadow += (sd < biasedReceiver) ? 0.0 : 1.0;
                #endif
            }
            shadow /= (float)nPcf;
        }

        // ── Debug 输出 ──
        if (_PCSS_DebugMode == 1) return half4(blockerRatio, blockerRatio, blockerRatio, 1);   // blocker比例
        if (_PCSS_DebugMode == 2) return half4(hardShadow, hardShadow, hardShadow, 1);          // 硬阴影
        if (_PCSS_DebugMode == 3) { float p = penumbraPixels / 30.0; return half4(p, p, p, 1); } // penumbra大小
        if (_PCSS_DebugMode == 4) return half4(avgBlockerDepth, avgBlockerDepth, avgBlockerDepth, 1); // blocker深度
        if (_PCSS_DebugMode == 5) return half4(receiverDepth, receiverDepth, receiverDepth, 1);  // receiver深度
        return half4(shadow, shadow, shadow, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PCSS"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma enable_d3d11_debug_symbols
            ENDHLSL
        }
    }
}
