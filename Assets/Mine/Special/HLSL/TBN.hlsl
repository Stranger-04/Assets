//TBN.hlsl
#ifndef TBN_HLSL_INCLUDED
#define TBN_HLSL_INCLUDED

void TBN(float3 normalOS, out float3 tangentOS, out float3 bitangentOS)
{
    float3 upOS = float3(0,1,0);
    if (normalOS.y > 0.999f || normalOS.y < -0.999f)
    {
        upOS = float3(0,0,1);
    }
    tangentOS = normalize(cross(upOS, normalOS));
    bitangentOS = cross(normalOS, tangentOS);
}

void BillboardTBN(float3 centerOS, out float3 tangentOS, out float3 bitangentOS, out float3 normalOS)
{
    float3 upOS = float3(0,1,0);
    float3 cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
    float3 toCameraOS = normalize(cameraOS - centerOS);
    if (toCameraOS.y > 0.999f || toCameraOS.y < -0.999f)
    {
        upOS = float3(0,0,1);
    }
    tangentOS = normalize(cross(upOS, toCameraOS));
    bitangentOS = cross(toCameraOS, tangentOS);
    normalOS = toCameraOS;
}
#endif