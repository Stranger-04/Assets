#ifndef VSUV_HLSL_INCLUDED
#define VSUV_HLSL_INCLUDED

void GetVSUV_float(float2 uv, float scale, float2 offset, float depth, out float2 vsUV)
{
    float3 viewPos = ComputeViewSpacePosition(uv, depth, unity_CameraInvProjection);
    float3 viewDir = normalize(viewPos);

    vsUV = viewDir.xy / max(viewDir.z, 0.001) * 0.5 + 0.5;
    vsUV = vsUV * scale + offset;
}

#endif