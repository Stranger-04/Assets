//NormalFunction.hlsl
#ifndef NORMALFUNCTION_HLSL_INCLUDED
#define NORMALFUNCTION_HLSL_INCLUDED

float3 NormalCompress(float3 normal, float3 direction, float strength)
{
    float factor = dot(normal, direction) * strength * 2.0;
    float3 compressedNormal = normal + direction * factor;
    compressedNormal = normalize(compressedNormal);
    return compressedNormal;
}

float3 NormalFloor(float3 normal, float3 value)
{
    float3 flooredNormal = floor(normal / value) * value;
    return flooredNormal;
}

#endif