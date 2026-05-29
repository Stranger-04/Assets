//DepthDiffFunction.hlsl
#ifndef DEPTHDIFFFUNCTION_HLSL_INCLUDED
#define DEPTHDIFFFUNCTION_HLSL_INCLUDED

float ComputeRelDepthDiff(float3 positionWS, float4 positionSS)
{
    float2 screenUV = positionSS.xy / positionSS.w;
    float  sceneRawDepth = SampleSceneDepth(screenUV);
    float  sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
    float  depthDiff = sceneEyeDepth - positionSS.w;
    
    return depthDiff;
}

float ComputeAbsDepthDiff(float3 positionWS, float4 positionSS)
{
    float2 screenUV = positionSS.xy / positionSS.w;
    float  sceneDepth = SampleSceneDepth(screenUV);
    float3 scenePosWS = ComputeWorldSpacePosition(screenUV, sceneDepth, UNITY_MATRIX_I_VP);
    float  depthDiff = positionWS.y - scenePosWS.y;
    
    return depthDiff;
}
#endif