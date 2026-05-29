//BlurFunction.hlsl
#ifndef BLURFUNCTION_HLSL_INCLUDED
#define BLURFUNCTION_HLSL_INCLUDED

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

#endif