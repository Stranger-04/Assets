#ifndef ENVFUNCTION_HLSL_INCLUDED
#define ENVFUNCTION_HLSL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

#if !defined(SHADER_API_GLES)
real4 GenEnvFGDLut(real NdotV, real roughness, uint sampleCount)
{
    return IntegrateGGXAndDisneyDiffuseFGD(NdotV, roughness, sampleCount);
}
#endif

real3 BRDF_Env_HD(real3 baseColor, real NdotV, real3 normalWS, real3 viewDirWS,
                real roughness, real metallic,
                TextureCube spec, SamplerState samSpec,
                Texture2D lutTex, SamplerState samLut)
{
    real3 F0 = lerp(0.04, baseColor, metallic);

    real3 R   = reflect(-viewDirWS, normalWS);
    real  mip = roughness * 5.0;
    real3 Pre = spec.SampleLevel(samSpec, R, mip).rgb;
    real3 Env = SampleSH(normalWS);
    real3 Lut = SAMPLE_TEXTURE2D(lutTex, samLut, float2(NdotV, roughness)).rgb;

    real3 specularFGD = F0 * Lut.g + (1.0 - F0) * Lut.r;
    real  diffuseFGD  = Lut.b + 0.5;
    real3 specular = Pre * specularFGD;
    real3 diffuse  = Env * diffuseFGD * baseColor * (1.0 - metallic) * (1.0 - specularFGD);
    return specular + diffuse;
}

real3 BRDF_Env(real3 baseColor, real NdotV, real3 normalWS, real3 viewDirWS,
              real roughness, real metallic,
              TextureCube spec, SamplerState sam)
{
    real3 F0 = lerp(0.04, baseColor, metallic);
    real  t              = 1.0 - NdotV;
    real  fresnelTerm    = t * t * t * t;
    real3 grazingTerm    = saturate(F0 + (1.0 - roughness));
    real  surfaceReduction = 1.0 / (roughness * roughness + 1.0);

    real3 R   = reflect(-viewDirWS, normalWS);
    real  mip = roughness * 5.0;
    real3 Pre = spec.SampleLevel(sam, R, mip).rgb;
    real3 Env = SampleSH(normalWS);

    real3 specularEnv = surfaceReduction * lerp(F0, grazingTerm, fresnelTerm);
    real3 specular = Pre * specularEnv;
    real3 diffuse  = Env * baseColor * (1.0 - metallic) * (1.0 - specularEnv);
    return specular + diffuse;
}

#endif
