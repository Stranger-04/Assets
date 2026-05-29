//ProjectionFunction.hlsl
#ifndef PROJECTIONFUNCTION_HLSL_INCLUDED
#define PROJECTIONFUNCTION_HLSL_INCLUDED

#ifndef SAMPLE_CUSTOM
#define SAMPLE_CUSTOM(tex, sam, uv) \
    tex.SampleLevel(sam, uv, 0).rgb
#endif

//direction weight function
float3 DirectionWeight(float3 normalWS, float sharpness)
{
    //sharpness is recommonded to be about 100
    float3 a = pow(abs(normalWS), sharpness);
    float  b = dot(a, float3(1.0, 1.0, 1.0));
    float3 weight = a / b;
    return weight;
}

float2 LowCostTriplanarProjectionUV(float3 normalWS, float3 positionWS, float sharpness, float scale)
{
    float3 weight = round(DirectionWeight(normalWS, sharpness));
    float3 remap  = sign(normalWS) * float3(1, 1, -1);
    float3 positionUV = positionWS * scale;
    float2 uvX = positionUV.zy * weight.x * float2(remap.x, 1);
    float2 uvY = positionUV.xz * weight.y * float2(remap.y, 1);
    float2 uvZ = positionUV.xy * weight.z * float2(remap.z, 1);
    float2 uv = uvX + uvY + uvZ;
    return uv;
}

float3 LowCostTriplanarProjectionTex(Texture2D tex, SamplerState sam, float3 normalWS, float3 positionWS, float sharpness, float scale)
{
    float2 uv = LowCostTriplanarProjectionUV(normalWS, positionWS, sharpness, scale);
    float3 color = SAMPLE_CUSTOM(tex, sam, uv);
    return color;
}

float3 HighCostTriplanarProjectionTex(Texture2D tex, SamplerState sam, float3 normalWS, float3 positionWS, float sharpness, float scale)
{
    float3 weight = DirectionWeight(normalWS, sharpness);
    float3 remap  = sign(normalWS) * float3(1, 1, -1);
    float3 positionUV = positionWS * scale;
    float2 uvX = positionUV.zy * float2(remap.x, 1);
    float2 uvY = positionUV.xz * float2(remap.y, 1);
    float2 uvZ = positionUV.xy * float2(remap.z, 1);
    float3 colorX = SAMPLE_CUSTOM(tex, sam, uvX) * weight.x;
    float3 colorY = SAMPLE_CUSTOM(tex, sam, uvY) * weight.y;
    float3 colorZ = SAMPLE_CUSTOM(tex, sam, uvZ) * weight.z;
    float3 color = colorX + colorY + colorZ;
    return color;
}

#endif