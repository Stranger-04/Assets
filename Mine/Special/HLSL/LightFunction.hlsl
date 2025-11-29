//LightFunction.hlsl
#ifndef LIGHTFUNCTION_HLSL_INCLUDED
#define LIGHTFUNCTION_HLSL_INCLUDED

//half_lambert lighting function
float DiffuseLambert(float3 normal, float3 lightDir)
{
    float NdotL = dot(normal, lightDir);
    return NdotL * 0.5 + 0.5;
}

//blinn-phong specular function
float SpecularBlinnPhong(float3 normal, float3 lightDir, float3 viewDir, float smoothness)
{
    float3 halfDir = normalize(lightDir + viewDir);
    float NdotH = max(dot(normal, halfDir), 0.0);
    return pow(NdotH, smoothness);
}
#endif