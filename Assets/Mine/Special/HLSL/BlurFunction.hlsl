//BlurFunction.hlsl
#ifndef BLURFUNCTION_HLSL_INCLUDED
#define BLURFUNCTION_HLSL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

float4 BlurHorizontal(float2 uv, float2 texelSize, float BlurScale, Texture2D MainTex, SamplerState sampler_LinearClamp)
{
    float4 color = float4(0,0,0,0);

    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(-2.0, 0.0)) * 0.1216216;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(-1.0, 0.0)) * 0.2332432;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv) * 0.290918;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(1.0, 0.0)) * 0.2332432;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(2.0, 0.0)) * 0.1216216;
    return color;
}

float4 BlurVertical(float2 uv, float2 texelSize, float BlurScale, Texture2D MainTex, SamplerState sampler_LinearClamp)
{
    float4 color = float4(0,0,0,0);

    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(0.0, -2.0)) * 0.1216216;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(0.0, -1.0)) * 0.2332432;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv) * 0.290918;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(0.0, 1.0)) * 0.2332432;
    color += SAMPLE_TEXTURE2D(MainTex, sampler_LinearClamp, uv + BlurScale * texelSize * float2(0.0, 2.0)) * 0.1216216;
    return color;
}


// ════════════════════════════════════════════════════════════════
//  双边保边模糊（深度 + 可选法线引导）
//
//  关键字（在调用 Shader 中声明）：
//    BLUR_BILATERAL_LOW           强度低 — 边缘柔和，允许少量渗漏
//    BLUR_BILATERAL_MEDIUM        强度中 — 默认平衡（未定义任何强度时生效）
//    BLUR_BILATERAL_HIGH          强度高 — 边缘锐利，严格保边
//    BLUR_BILATERAL_NORMAL        启用法线保边（未定义时仅深度保边）
// ════════════════════════════════════════════════════════════════

// ── 强度预设 ──
#if defined(BLUR_BILATERAL_LOW)
    static const float _BilateralDepthSens = 2.0;
    static const float _BilateralNormalPow = 2.0;
#elif defined(BLUR_BILATERAL_HIGH)
    static const float _BilateralDepthSens = 50.0;
    static const float _BilateralNormalPow = 32.0;
#else
    static const float _BilateralDepthSens = 10.0;
    static const float _BilateralNormalPow = 8.0;
#endif

// 5-tap Gaussian
static const float _BilateralGauss[5] = { 0.1216, 0.2332, 0.2910, 0.2332, 0.1216 };
static const float _BilateralOffset[5] = { -2, -1, 0, 1, 2 };

float4 BilateralBlurHorizontal(
    float2 uv, float2 texelSize, float blurScale,
    Texture2D MainTex, SamplerState samplerState)
{
    float  centerRawDepth = SampleSceneDepth(uv);
    float  centerEyeDepth = LinearEyeDepth(centerRawDepth, _ZBufferParams);
    #ifdef BLUR_BILATERAL_NORMAL
    float3 centerNormal    = SampleSceneNormals(uv);
    #endif

    float4 sum    = 0;
    float  totalW = 0;

    [unroll]
    for (int i = 0; i < 5; i++)
    {
        float2 suv = uv + blurScale * texelSize * float2(_BilateralOffset[i], 0);
        float4 s   = SAMPLE_TEXTURE2D(MainTex, samplerState, suv);

        float sdRaw = SampleSceneDepth(suv);
        float sdEye = LinearEyeDepth(sdRaw, _ZBufferParams);
        float depthW = exp(-abs(centerEyeDepth - sdEye) * _BilateralDepthSens);
        float normalW = 1.0;

        #ifdef BLUR_BILATERAL_NORMAL
        float3 sn = SampleSceneNormals(suv);
        normalW   = pow(saturate(dot(centerNormal, sn)), _BilateralNormalPow);
        #endif

        float w = _BilateralGauss[i] * depthW * normalW;
        sum    += s * w;
        totalW += w;
    }

    return sum / max(totalW, 0.0001);
}

float4 BilateralBlurVertical(
    float2 uv, float2 texelSize, float blurScale,
    Texture2D MainTex, SamplerState samplerState)
{
    float  centerRawDepth = SampleSceneDepth(uv);
    float  centerEyeDepth = LinearEyeDepth(centerRawDepth, _ZBufferParams);
    #ifdef BLUR_BILATERAL_NORMAL
    float3 centerNormal    = SampleSceneNormals(uv);
    #endif

    float4 sum    = 0;
    float  totalW = 0;

    [unroll]
    for (int i = 0; i < 5; i++)
    {
        float2 suv = uv + blurScale * texelSize * float2(0, _BilateralOffset[i]);
        float4 s   = SAMPLE_TEXTURE2D(MainTex, samplerState, suv);

        float sdRaw = SampleSceneDepth(suv);
        float sdEye = LinearEyeDepth(sdRaw, _ZBufferParams);
        float depthW = exp(-abs(centerEyeDepth - sdEye) * _BilateralDepthSens);
        float normalW = 1.0;
        
        #ifdef BLUR_BILATERAL_NORMAL
        float3 sn = SampleSceneNormals(suv);
        normalW   = pow(saturate(dot(centerNormal, sn)), _BilateralNormalPow);
        #endif

        float w = _BilateralGauss[i] * depthW * normalW;
        sum    += s * w;
        totalW += w;
    }

    return sum / max(totalW, 0.0001);
}

#endif