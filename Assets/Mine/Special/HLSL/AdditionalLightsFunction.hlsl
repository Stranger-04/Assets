//AdditionalLightsFunction.hlsl
#ifndef ADDITIONALLIGHTFUNCTION_HLSL_INCLUDED
#define ADDITIONALLIGHTFUNCTION_HLSL_INCLUDED

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

float AdditionalLights(float3 positionWS, float3 normalWS, float3 viewDirWS, float smoothness)
{
    float3 addColor = float3(0,0,0);
    int count = GetAdditionalLightsCount();
    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    for (int i = 0; i < count; i++)
    {
        Light light = GetAdditionalLight(i, positionWS, unity_ProbesOcclusion);
        float3 lightDir   = light.direction;
        float3 lightColor = light.color;
        float  lightAtten = light.distanceAttenuation * light.shadowAttenuation;

        float diffuse  = saturate(DiffuseLambert(normalWS, lightDir));
        float specular = SpecularBlinnPhong(normalWS, lightDir, viewDirWS, smoothness);

        addColor += lightAtten * (diffuse + specular) * lightColor;
    }
    #endif
    return addColor;
}
#endif