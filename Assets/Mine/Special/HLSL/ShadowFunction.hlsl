// ShadowFunction.hlsl
#ifndef SHADOWFUNCTION_HLSL_INCLUDED
#define SHADOWFUNCTION_HLSL_INCLUDED

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT

#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX

void MainLight(float3 positionWS, out float3 direction, out float3 color, out float distanceAtten, out float shadowAtten)
{
    #if SHADOWS_SCREEN
        float4 positionCS  = TransformWorldToHClip(positionWS);
        float4 shadowCoord = ComputeScreenPos(positionCS);
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    #endif
    Light mainLight = GetMainLight(shadowCoord);

    direction       = mainLight.direction;
    color           = mainLight.color;
    distanceAtten   = mainLight.distanceAttenuation;
    shadowAtten     = mainLight.shadowAttenuation;
}

#endif