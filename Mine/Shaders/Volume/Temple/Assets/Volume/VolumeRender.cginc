//#include "UnityCG.cginc"

float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 rayDir)
{
    float3 t0 = (boundsMin - rayOrigin) / rayDir;
    float3 t1 = (boundsMax - rayOrigin) / rayDir;

    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);

    float dstA = max(max(tmin.x, tmin.y), tmin.z);
    float dstB = min(min(tmax.x, tmax.y), tmax.z);

    float dstToBox = max(0, dstA);
    float dstInsideBox = max(0, dstB - dstToBox);
    return float2(dstToBox, dstInsideBox);
}

float _DensityMul;
//云的吸收
float _LightAbsorptionThroughCloud;
//朝向太阳的吸收
float _LightAbsorptionTowardSun;

float SampleDensity(sampler3D volumeTex, float3 pos)
{
    return  tex3D(volumeTex, pos).x*_DensityMul;
}

float LightMarch(float3 boundsMin, float3 boundsMax, float3 rayOrigin, sampler3D volumeTex)
{
    float stepNum = 10;
    float3 rayDir = _WorldSpaceLightPos0;
    float inside = rayBoxDst(boundsMin, boundsMax, rayOrigin, rayDir).y;
    float stepSize = inside / stepNum;
    float totalDensity = 0;
    float3 samplePoint = rayOrigin;
    float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));
    float3 scale = 1.0f / (boundsMax - boundsMin);
    for (int step = 0; step < stepNum; step++)
    {
        float3 pos = (samplePoint - center) * scale + float3(0.5, 0.5, 0.5);
        totalDensity += SampleDensity(volumeTex, pos)* stepSize;
    }
    float transmittance = exp(-totalDensity* _LightAbsorptionTowardSun);
    return transmittance;
}
//步进最大数
#define StepMaxNum 50
//boundsMin:包围盒的最小值 boundsMax:包围盒的最大值  rayOrigin:射线的起点 rayDir:射线的方向 volumeTex：体积纹理
float4 rayMarching(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 rayDir,sampler3D volumeTex,float depthFade)
{
    float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));

    float2 hitInfo = rayBoxDst(boundsMin, boundsMax, rayOrigin, rayDir);

    hitInfo.y = min(depthFade, hitInfo.y);

    float stepSize = hitInfo.y / StepMaxNum;

    float3 samplePoint = rayOrigin + rayDir*hitInfo.x;

    float3 scale = 1.0f/(boundsMax - boundsMin);

    //透光率
    float transmittance = 1;
    float3 Energy = 0;
    for (int step = 0; step < StepMaxNum; step++)
    {
        float3 pos = (samplePoint - center) * scale + float3(0.5, 0.5, 0.5);
        //采样密度
        float density = SampleDensity(volumeTex,pos)* stepSize;

        Energy += LightMarch(boundsMin, boundsMax, samplePoint, volumeTex)* density* transmittance;
        
        transmittance *= exp(-density* _LightAbsorptionThroughCloud);

        samplePoint += rayDir * stepSize;
    }
    return float4(Energy,1-transmittance);
}