#ifndef PBRFUNCTION_HLSL_INCLUDED
#define PBRFUNCTION_HLSL_INCLUDED

real Spec_D_GGX(real NdotH, real alpha)
{
    real alpha2   = alpha * alpha;
    real NdotH2   = NdotH * NdotH;
    real denomD   = (NdotH2 * (alpha2 - 1.0) + 1.0);
    return alpha2 / (PI * denomD * denomD + 1e-5);
}

real Spec_D_GGX_Aniso(real TdotH, real BdotH, real NdotH, real alpha, real aniso, real anisotropy)
{
    real alpha2   = alpha * alpha;
    real aspect   = sqrt(1.0 - aniso * 0.9);
    real axisX    = aspect / alpha;
    real axisY    = aspect * alpha;

    if (anisotropy < 0.0)
    {
        axisX = aspect * alpha;
        axisY = aspect / alpha;
    }
    
    real TdotH2   = TdotH * TdotH;
    real BdotH2   = BdotH * BdotH;
    real NdotH2   = NdotH * NdotH;
    real denomD   = (TdotH2 / axisX / axisX + BdotH2 / axisY / axisY + NdotH2);
    return 1 / (PI * axisX * axisY * denomD * denomD + 1e-5);
}

real Spec_G_Smith(real NdotV, real NdotL, real alpha)
{
    real k = (alpha + 1.0) * (alpha + 1.0) / 8.0;
    real GV = NdotV / (NdotV * (1.0 - k) + k + 1e-5);
    real GL = NdotL / (NdotL * (1.0 - k) + k + 1e-5);
    return GV * GL;
}

real Spec_G_SKSmith(real NdotV, real NdotL, real LdotH, real alpha)
{
    real k = alpha;
    return (NdotL * NdotV) / ((1 - k) * pow(LdotH, 2.0) + k + 1e-5);
}

real Spec_V(real LdotH, real alpha)
{
    return 1 / (max(pow(LdotH, 2.0), 0.1) * (alpha + 0.5));
}

real3 F_P5(real3 F0, real VdotH)
{
    return F0 + (1.0 - F0) * pow(1.0 - VdotH, 5.0);
}

real3 F_Fast(real3 F0, real VdotH)
{
    return F0 + (1.0 - F0) * pow(1.0 - VdotH, 2.0) * (1.0 - 2.0 * VdotH);
}

real3 Diff_Lambert(real3 albedo)
{
    return albedo / PI;
}

real Diff_Burley(real NdotL, real NdotV, real LdotH, real roughness)
{
    real FL = pow(1.0 - NdotL, 5.0);
    real FV = pow(1.0 - NdotV, 5.0);
    real Fd90 = 0.5 + 2.0 * LdotH * LdotH * roughness;
    return (1.0 + (Fd90 - 1.0) * FL) * (1.0 + (Fd90 - 1.0) * FV);
}

//CookTorrance BRDF function
real BRDF_Spec_CK(real NdotH, real NdotV, real NdotL, real VdotH, real roughness)
{
    real alpha = roughness * roughness;

    //Distribution function (GGX)
    real D = Spec_D_GGX(NdotH, alpha);
    //Geometry function (Schlick-GGX)
    real G = Spec_G_Smith(NdotV, NdotL, alpha);

    return (D * G) / (4.0 * NdotV * NdotL + 1e-5);
}

real3 BRDF_Spec_Unity(real NdotH, real LdotH, real VdotH, real TdotH, real BdotH, real roughness, real anisotropy)
{
    real alpha = roughness * roughness;
    real aniso = saturate(abs(anisotropy));

    real D1 = Spec_D_GGX(NdotH, alpha);
    real D2 = Spec_D_GGX_Aniso(TdotH, BdotH, NdotH, alpha, aniso, anisotropy);
    real D  = lerp(D1, D2, aniso);
    real V  = Spec_V(LdotH, alpha);

    return (D * V) / ((4.0 + 1e-5) * (1.0 + 0.5 * alpha));
}

real3 BRDFClassic(real3 baseColor, real NdotL, real NdotV, real NdotH, real VdotH, real LdotH, real roughness, real metallic)
{
    real3 F0 = lerp(0.04, baseColor, metallic);
    real3 F  = F_P5(F0, VdotH);

    real3 specular = BRDF_Spec_CK(NdotH, NdotV, NdotL, VdotH, roughness) * F;
    real3 diffuse  = Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

real3 BRDFUnity(real3 baseColor, real NdotL, real NdotV, real NdotH, 
                real VdotH, real LdotH, real TdotH, real BdotH, 
                real roughness, real metallic, real anisotropy)
{
    real3 F0 = lerp(0.04, baseColor, metallic);
    real3 F  = F_Fast(F0, VdotH);

    real3 specular = BRDF_Spec_Unity(NdotH, LdotH, VdotH, TdotH, BdotH, roughness, anisotropy) * F;
    real3 diffuse  = Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

real3 BRDFBurley(real3 baseColor, real NdotL, real NdotV, real NdotH, real VdotH, real LdotH, real roughness, real metallic)
{
    real3 F0 = lerp(0.04, baseColor, metallic);
    real3 F  = F_P5(F0, VdotH);

    real3 specular = BRDF_Spec_CK(NdotH, NdotV, NdotL, VdotH, roughness) * F;
    real3 diffuse  = Diff_Burley(NdotL, NdotV, LdotH, roughness) * Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}
#endif