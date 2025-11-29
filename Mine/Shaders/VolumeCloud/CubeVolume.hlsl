float ShapeSDF(float3 p, float3 boxCenter, float3 boxHalfSize)
{
    float radius = boxHalfSize.x;
    return length(p - boxCenter) - radius;
}

float Noise3D(
    UnityTexture3D noiseTexture, 
    UnitySamplerState noiseTexture_sampler, 
    float3 p, 
    float scale, 
    float strength, 
    float3 dir, 
    float speed,
    float time
    )
{
    float3 uvw = p * scale + dir * speed * time;
    return SAMPLE_TEXTURE3D_LOD(noiseTexture, noiseTexture_sampler, uvw, 0).r * strength;
}

float DensityCalculation(float sdf, float noise, float softness)
{
    if (sdf > 0.0)
    {
        return 0.0;
    }
    return saturate(((- sdf) * noise) / softness);
}

void CubeVolume_float
(
    float3 CameraPos, 
    float3 WorldPos, 
    float3 BoxCenter, 
    float3 BoxHalfSize, 
    int MaxSteps, 
    float Softness, 
    float3 WorldPosFromDepth, 
    UnityTexture3D NoiseTex, 
    UnitySamplerState NoiseTex_sampler,
    float NoiseScale,
    float NoiseStrength,
    float NoiseSpeed,
    float3 NoiseDirection,
    float Time,
    float3 LightDir,
    out float Diffuse,
    out float Density
    )
{
    float3 RayDir = normalize(WorldPos - CameraPos);
    float3 InvDir = 1.0 / RayDir;

    float3 tMin = (BoxCenter - BoxHalfSize - CameraPos) * InvDir;
    float3 tMax = (BoxCenter + BoxHalfSize - CameraPos) * InvDir;

    float3 t1 = min(tMin, tMax);
    float3 t2 = max(tMin, tMax);

    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);

    Diffuse = 0.0;
    Density = 0.0;
    // AABB Box
    if (tNear > tFar || tFar < 0.0)
    {
        return;
    }
    
    float tScene = dot(WorldPosFromDepth - CameraPos, RayDir);

    // Depth Clipping
    if (tNear > tScene)
    {
        return;
    }
    if (tFar > tScene)
    {
        tFar = tScene;
    }
    tNear = max(tNear, 0.0);

    float StepSize = (tFar - tNear) / MaxSteps;
    float MinStep = StepSize * 0.1;
    float tCurrent = tNear;

    for (int i = 0; i < MaxSteps; i++)
    {
        // Bounding Area
        float3 SamplePos = CameraPos + RayDir * tCurrent;
        float3 jitter = float3(Hash(SamplePos), Hash(SamplePos.yx), Hash(SamplePos.xy));
        SamplePos += jitter * (1.0 / MaxSteps);
        float SDF = ShapeSDF(SamplePos, BoxCenter, BoxHalfSize);

        if (SDF > 0.0) 
        { 
            tCurrent += max(SDF, MinStep); 
            if (tCurrent >= tFar) 
            { 
                break; 
            } 
            continue; 
        }

        // Density Accumulation
        if (SDF < 0.0) 
        { 
            float Noise = Noise3D(NoiseTex, NoiseTex_sampler, SamplePos, NoiseScale, NoiseStrength, NoiseDirection, NoiseSpeed, Time);

            float3 LightPos = SamplePos + normalize(LightDir);
            float LightSDF = ShapeSDF(LightPos, BoxCenter, BoxHalfSize);
            float LightNoise = Noise3D(NoiseTex, NoiseTex_sampler, LightPos, NoiseScale, NoiseStrength, NoiseDirection, NoiseSpeed, Time);

            float LocalDensity = DensityCalculation(SDF, Noise, Softness);
            float LightDensity = DensityCalculation(LightSDF, LightNoise, Softness);
            float DeltaDensity = saturate(LocalDensity - LightDensity);

            float Step = StepSize;
            tCurrent += Step;
            Diffuse += Step * DeltaDensity;
            Density += Step * LocalDensity;
            continue;
        } 

        if (SDF >= 0.0) 
        { 
            break; 
        }
    }
}

