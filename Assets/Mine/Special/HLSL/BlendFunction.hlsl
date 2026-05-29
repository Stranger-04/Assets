//BlendFunction.hlsl
#ifndef BLENDFUNCTION_HLSL_INCLUDED
#define BLENDFUNCTION_HLSL_INCLUDED

float3 BlendNormal_Linear(float3 normalA, float3 normalB, float blendFactor)
{
    return normalize(lerp(normalA, normalB, blendFactor));
}

float3 BlendNormal_Overlay(float3 normalA, float3 normalB, float blendFactor)
{
    float3 blendedNormal = normalA < 0 ? 2.0 * normalA * normalB : 1.0 - 2.0 * (1.0 - normalA) * (1.0 - normalB);
    blendedNormal = lerp(normalA, blendedNormal, blendFactor);
    return normalize(blendedNormal);
}

float3 BlendNormal_PartialDerivative(float3 normalA, float3 normalB, float blendFactor)
{
    float2 PD = lerp(normalA.xy/normalA.z, normalB.xy/normalB.z, 0.5);
    float3 blendedNormal =float3 (PD, 1.0);
    blendedNormal = lerp(normalA, blendedNormal, blendFactor);
    return normalize(blendedNormal);
}

float3 BlendNormal_Whiteout(float3 normalA, float3 normalB, float blendFactor)
{
    float3 blendedNormal = float3(normalA.xy + normalB.xy, normalA.z * normalB.z);
    blendedNormal = lerp(normalA, blendedNormal, blendFactor);
    return normalize(blendedNormal);
}

float3 BlendNormal_UDN(float3 normalA, float3 normalB, float blendFactor)
{
    float3 blendedNormal = float3(normalA.xy + normalB.xy, normalA.z);
    blendedNormal = lerp(normalA, blendedNormal, blendFactor);
    return normalize(blendedNormal);
}

float3 BlendNormal_RNM(float3 normalA, float3 normalB, float blendFactor)
{
    normalA = normalA + float3(0, 0, 1);
    normalB = normalB * float3(-1, -1, 1);
    float3 blendedNormal = normalA * dot(normalA, normalB)/normalA.z - normalB;
    blendedNormal = lerp(normalA, blendedNormal, blendFactor);
    return normalize(blendedNormal);
}

#endif