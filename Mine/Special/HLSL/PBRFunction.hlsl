#ifndef PBRFUNCTION_HLSL_INCLUDED
#define PBRFUNCTION_HLSL_INCLUDED

float Spec_D_GGX(float NdotH, float alpha)
{
    float alpha2   = alpha * alpha;
    float NdotH2   = NdotH * NdotH;
    float denomD   = (NdotH2 * (alpha2 - 1.0) + 1.0);
    return alpha2 / (PI * denomD * denomD + 1e-5);
}

float Spec_G_Smith(float NdotV, float NdotL, float alpha)
{
    float k = (alpha + 1.0) * (alpha + 1.0) / 8.0;
    float GV = NdotV / (NdotV * (1.0 - k) + k + 1e-5);
    float GL = NdotL / (NdotL * (1.0 - k) + k + 1e-5);
    return GV * GL;
}

float Spec_G_SKSmith(float NdotV, float NdotL, float LdotH, float alpha)
{
    float k = alpha;
    return (NdotL * NdotV) / ((1 - k) * pow(LdotH, 2.0) + k + 1e-5);
}

float Spec_V(float LdotH, float alpha)
{
    return 1 / (max(pow(LdotH, 2.0), 0.1) * (alpha + 0.5));
}

float3 F_P5(float3 F0, float VdotH)
{
    return F0 + (1.0 - F0) * pow(1.0 - VdotH, 5.0);
}

float3 F_Fast(float3 F0, float VdotH)
{
    return F0 + (1.0 - F0) * pow(1.0 - VdotH, 2.0) * (1.0 - 2.0 * VdotH);
}

float3 Diff_Lambert(float3 albedo)
{
    return albedo / PI;
}

float Diff_Burley(float NdotL, float NdotV, float LdotH, float roughness)
{
    float FL = pow(1.0 - NdotL, 5.0);
    float FV = pow(1.0 - NdotV, 5.0);
    float Fd90 = 0.5 + 2.0 * LdotH * LdotH * roughness;
    return (1.0 + (Fd90 - 1.0) * FL) * (1.0 + (Fd90 - 1.0) * FV);
}

float3 Diff_Env(float3 albedo, float3 normalWS)
{
    return SampleSH(normalWS) * albedo;
}

//CookTorrance BRDF function
float BRDF_Spec_CK(float NdotH, float NdotV, float NdotL, float VdotH, float roughness)
{
    float alpha = roughness * roughness;

    //Distribution function (GGX)
    float D = Spec_D_GGX(NdotH, alpha);
    //Geometry function (Schlick-GGX)
    float G = Spec_G_Smith(NdotV, NdotL, alpha);

    return (D * G) / (4.0 * NdotV * NdotL + 1e-5);
}

float3 BRDF_Spec_Unity(float NdotH, float LdotH, float VdotH, float roughness)
{
    float alpha = roughness * roughness;

    float D = Spec_D_GGX(NdotH, alpha);
    float V = Spec_V(LdotH, alpha);

    return (D * V) / ((4.0 + 1e-5) * (1.0 + 0.5 * alpha));
}

float3 Spec_Env(float3 normalWS, float3 viewDirWS, float NdotV, float roughness, TextureCube env, SamplerState sam)
{
    float3 R = reflect(-viewDirWS, normalWS);
    float  mip = roughness * 5.0;
    return env.SampleLevel(sam, R, mip).rgb;
}

float3 BRDFClassic(float3 baseColor, float3 normalWS, float3 lightDirWS, float3 viewDirWS, float roughness, float metallic)
{
    float3 halfVec = normalize(lightDirWS + viewDirWS);
    float NdotH = max(dot(normalWS, halfVec), 0.0);
    float NdotV = max(dot(normalWS, viewDirWS), 0.0);
    float NdotL = max(dot(normalWS, lightDirWS), 0.0);
    float VdotH = max(dot(viewDirWS, halfVec), 0.0);
    float LdotH = max(dot(lightDirWS, halfVec), 0.0);

    float3 F0 = lerp(0.04, baseColor, metallic);
    float3 F  = F_P5(F0, VdotH);
    float3 Fenv = F_P5(F0, NdotV);

    float3 specular = BRDF_Spec_CK(NdotH, NdotV, NdotL, VdotH, roughness) * F;
    float3 diffuse  = Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

float3 BRDFSimple(float3 baseColor, float3 normalWS, float3 lightDirWS, float3 viewDirWS, float roughness, float metallic)
{
    float3 halfVec = normalize(lightDirWS + viewDirWS);
    float NdotH = max(dot(normalWS, halfVec), 0.0);
    float NdotV = max(dot(normalWS, viewDirWS), 0.0);
    float NdotL = max(dot(normalWS, lightDirWS), 0.0);
    float VdotH = max(dot(viewDirWS, halfVec), 0.0);
    float LdotH = max(dot(lightDirWS, halfVec), 0.0);

    float3 F0 = lerp(0.04, baseColor, metallic);
    float3 F  = F_Fast(F0, VdotH);

    float3 specular = BRDF_Spec_Unity(NdotH, LdotH, VdotH, roughness) * F;
    float3 diffuse  = Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

float3 BRDFBurley(float3 baseColor, float3 normalWS, float3 lightDirWS, float3 viewDirWS, float roughness, float metallic)
{
    float3 halfVec = normalize(lightDirWS + viewDirWS);
    float NdotH = max(dot(normalWS, halfVec), 0.0);
    float NdotV = max(dot(normalWS, viewDirWS), 0.0);
    float NdotL = max(dot(normalWS, lightDirWS), 0.0);
    float VdotH = max(dot(viewDirWS, halfVec), 0.0);
    float LdotH = max(dot(lightDirWS, halfVec), 0.0);

    float3 F0 = lerp(0.04, baseColor, metallic);
    float3 F  = F_P5(F0, VdotH);

    float3 specular = BRDF_Spec_CK(NdotH, NdotV, NdotL, VdotH, roughness) * F;
    float3 diffuse  = Diff_Burley(NdotL, NdotV, LdotH, roughness) * Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

float3 BRDFEnv(float3 baseColor, float3 normalWS, float3 viewDirWS, float roughness, float metallic, TextureCube env, SamplerState sam)
{
    float NdotV = max(dot(normalWS, viewDirWS), 0.0);
    float3 F0   = lerp(0.04, baseColor, metallic);
    float3 Fenv = F_P5(F0, NdotV);

    float3 specular = Spec_Env(normalWS, viewDirWS, NdotV, roughness, env, sam) * Fenv;
    float3 diffuse  = Diff_Env(baseColor, normalWS) * (1.0 - metallic) * (1.0 - Fenv);
    return specular + diffuse;
}
#endif