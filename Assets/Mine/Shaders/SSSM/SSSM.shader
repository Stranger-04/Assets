Shader "Hidden/SSSM"
{
    Properties
    {
        _StepSize ("Step Size", Range(0.1, 2.0)) = 0.5
        _MaxDistance ("Max Distance", Range(1, 200)) = 50.0
        _StepCount ("Step Count", Range(4, 128)) = 32
        _Thickness ("Thickness", Range(0.001, 0.5)) = 0.05
        _LightRayThickness ("Light Ray Thickness", Range(0.01, 5.0)) = 0.5
        _BlurScale ("Blur Scale", Range(0.0, 5.0)) = 1.0
    }

    HLSLINCLUDE
    // Unity 6 管线 — 全屏后处理必须使用 Blit.hlsl
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Assets/Mine/Special/HLSL/BlurFunction.hlsl"

    // ── 参数 ──
    float _StepSize;
    float _MaxDistance;
    int   _StepCount;
    float _Thickness;
    float _LightRayThickness;
    float _BlurScale;

    // DDA 使用 Unity 内置矩阵（与 ComputeWorldSpacePosition / SampleSceneDepth 一致）
    // UNITY_MATRIX_V, UNITY_MATRIX_P — 自动匹配当前相机和图形 API

    // ════════════════════════════════════════════════════════════
    //  Pass 0: DDA 2D 雷步进 — 屏幕空间阴影追踪
    //
    //  核心思路（与 SSR 对称）：
    //     SSR:   沿反射方向步进 → 找第一个表面命中点
    //     SSSM:  沿光源方向步进 → 检查是否有遮挡物
    //
    //  输出：
    //    R: shadow factor（0=完全阴影, 1=完全照亮）
    //    G: 遮挡物平均深度（Eye Space, 保留给未来 PCSS 使用）
    // ════════════════════════════════════════════════════════════
    half4 Frag_SSSM_DDA(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;

        // ── 1. 重建世界坐标 ──
        float rawDepth = SampleSceneDepth(uv);
        float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

        // ── 2. 获取主光源方向（URP 约定：表面→光源） ──
        float3 lightDirWS = GetMainLight().direction;
        float3 viewDirWS  = normalize(_WorldSpaceCameraPos - positionWS);

        // ── 3. DDA Ray 设置（投影到屏幕空间） ──
        float3 startWS = positionWS;
        float3 endWS   = positionWS + lightDirWS * _MaxDistance;

        float3 startVS = mul(UNITY_MATRIX_V, float4(startWS, 1)).xyz;
        float3 endVS   = mul(UNITY_MATRIX_V, float4(endWS, 1)).xyz;

        float4 startCS = mul(UNITY_MATRIX_P, float4(startVS, 1));
        float4 endCS   = mul(UNITY_MATRIX_P, float4(endVS, 1));

        // 透视校正插值系数: K = 1/w
        float  startK = 1.0 / startCS.w;
        float  endK   = 1.0 / endCS.w;

        // 屏幕空间起始/终点 UV
        float2 startS = (float2(startCS.x, startCS.y * _ProjectionParams.x) * startK) * 0.5 + 0.5;
        float2 endS   = (float2(endCS.x, endCS.y * _ProjectionParams.x) * endK) * 0.5 + 0.5;

        // 透视校正插值: V = viewSpacePos * K
        float3 startV = startVS * startK;
        float3 endV   = endVS * endK;

        // 每步增量
        float  dk = (endK - startK) / _StepCount * _StepSize;
        float2 ds = (endS - startS) / _StepCount * _StepSize;
        float3 dv = (endV - startV) / _StepCount * _StepSize;

        // ── 4. Jitter ──
        float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);

        float  K = startK + jitter * dk;
        float2 S = startS + jitter * ds;
        float3 V = startV + jitter * dv;

        // ── 5. 步进与遮挡测试 ──
        float shadow = 1.0;
        float occluderDepthSum = 0.0;
        int   blockerCount = 0;

        [loop]
        for (int i = 0; i < _StepCount; i++)
        {
            K += dk;
            S += ds;
            V += dv;

            // 光线穿过了近平面或摄像机后面 → 无遮挡信息，安全退出
            if (K <= 0.0)
                break;

            // 超出屏幕边界 → 无遮挡信息，假设无遮挡（optimistic）
            if (S.x < 0 || S.x > 1 || S.y < 0 || S.y > 1)
                break;

            float sceneRawDepth = SampleSceneDepth(S);
            float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
            float linearRayDepth =  - V.z / K;
            float depthDiff = linearRayDepth - sceneEyeDepth;

            // ── 沿光源方向的深度比较 ──
            // 重建光线点 + 场景点的世界坐标，沿光源方向比较深度
            // sceneWS 比 rayWS 更靠近光源 → 场景表面在光线路径上 → 遮挡
            float3 rayVS   = V / K;
            float3 rayWS   = mul(UNITY_MATRIX_I_V, float4(rayVS, 1)).xyz;
            float3 sceneWS = ComputeWorldSpacePosition(S, sceneRawDepth, UNITY_MATRIX_I_VP);
            float  depthDiffAlongLight = dot(sceneWS - rayWS, lightDirWS);
            if (depthDiff > _Thickness)
            {
                shadow = 0.0;
                occluderDepthSum += depthDiff;
                blockerCount++;
                break;
            }
        }

        half4 result;
        result.r = shadow;
        result.g = blockerCount > 0 ? occluderDepthSum / blockerCount : 0.0;
        result.ba = half2(0, 1);
        return shadow;
        return result;
    }

    // ════════════════════════════════════════════════════════════
    //  Pass 1: 水平模糊（BlurFunction 封装的 5-tap Gaussian）
    // ════════════════════════════════════════════════════════════
    half4 Frag_BlurH(Varyings input) : SV_Target
    {
        float2 texelSize = 1.0 / _ScreenParams.xy;
        return BlurHorizontal(input.texcoord, texelSize, _BlurScale, _BlitTexture, sampler_LinearClamp);
    }

    // ════════════════════════════════════════════════════════════
    //  Pass 2: 垂直模糊
    // ════════════════════════════════════════════════════════════
    half4 Frag_BlurV(Varyings input) : SV_Target
    {
        float2 texelSize = 1.0 / _ScreenParams.xy;
        return BlurVertical(input.texcoord, texelSize, _BlurScale, _BlitTexture, sampler_LinearClamp);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "SSSM_RayMarch"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_SSSM_DDA
            ENDHLSL
        }
        Pass
        {
            Name "SSSM_BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurH
            ENDHLSL
        }
        Pass
        {
            Name "SSSM_BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurV
            ENDHLSL
        }
    }
}
