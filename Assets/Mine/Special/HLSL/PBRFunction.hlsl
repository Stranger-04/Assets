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
real Spec_CK(real NdotH, real NdotV, real NdotL, real VdotH, real roughness)
{
    real alpha = roughness * roughness;

    //Distribution function (GGX)
    real D = Spec_D_GGX(NdotH, alpha);
    //Geometry function (Schlick-GGX)
    real G = Spec_G_Smith(NdotV, NdotL, alpha);

    return (D * G) / (4.0 * NdotV * NdotL + 1e-5);
}

real3 Spec_Unity(real NdotH, real LdotH, real VdotH, real TdotH, real BdotH, real roughness, real anisotropy)
{
    real alpha = roughness * roughness;
    real aniso = saturate(abs(anisotropy));

    real D1 = Spec_D_GGX(NdotH, alpha);
    real D2 = Spec_D_GGX_Aniso(TdotH, BdotH, NdotH, alpha, aniso, anisotropy);
    real D  = lerp(D1, D2, aniso);
    real V  = Spec_V(LdotH, alpha);

    return (D * V) / ((4.0 + 1e-5) * (1.0 + 0.5 * alpha));
}

real3 BRDF_Classic(real3 baseColor, real NdotL, real NdotV, real NdotH, real VdotH, real LdotH, real roughness, real metallic)
{
    real3 F0 = lerp(0.04, baseColor, metallic);
    real3 F  = F_P5(F0, VdotH);

    real3 specular = Spec_CK(NdotH, NdotV, NdotL, VdotH, roughness) * F;
    real3 diffuse  = Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

real3 BRDF_Unity(real3 baseColor, real NdotL, real NdotV, real NdotH, 
                real VdotH, real LdotH, real TdotH, real BdotH, 
                real roughness, real metallic, real anisotropy)
{
    real3 F0 = lerp(0.04, baseColor, metallic);
    real3 F  = F_Fast(F0, VdotH);

    real3 specular = Spec_Unity(NdotH, LdotH, VdotH, TdotH, BdotH, roughness, anisotropy) * F;
    real3 diffuse  = Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

real3 BRDF_Burley(real3 baseColor, real NdotL, real NdotV, real NdotH, real VdotH, real LdotH, real roughness, real metallic)
{
    real3 F0 = lerp(0.04, baseColor, metallic);
    real3 F  = F_P5(F0, VdotH);

    real3 specular = Spec_CK(NdotH, NdotV, NdotL, VdotH, roughness) * F;
    real3 diffuse  = Diff_Burley(NdotL, NdotV, LdotH, roughness) * Diff_Lambert(baseColor) * (1.0 - metallic) * (1.0 - F);
    return specular + diffuse;
}

// ====================================================================
// Marschner Hair Model
// 
// Three light paths through a cylindrical fiber:
//   R  (Reflection):       surface reflection — white, shifted primary highlight
//   TT (Transmission):     light passes through fiber — colored, opposite side
//   TRT (Transmission-Reflection-Transmission): internal bounce — colored, shifted secondary
//
// For real-time we simplify to two specular lobes (R + TRT).
//
// Input conventions:
//   TdotH  = dot(fiberTangent, halfVector)
//   NdotH  = dot(surfaceNormal, halfVector)
//   TdotN  = dot(fiberTangent, surfaceNormal)   — 1.0 for shell fur (T=N)
//   shift  = how far the lobe shifts along the normal  (-0.5 ~ 0.5)
//   roughness  = 0 = mirror, 1 = fully diffuse  (maps to gloss via pow(sin))
// ====================================================================

// Shifted tangent dot half-vector:  T' = normalize(T + shift * N), returns dot(T', H)
real Hair_ShiftedTdotH(real TdotH, real NdotH, real TdotN, real shift)
{
    real denom = sqrt(max(1.0 + 2.0 * shift * TdotN + shift * shift, 1e-7));
    return (TdotH + shift * NdotH) / denom;
}

// Kajiya-Kay specular from (shifted) tangent and half-vector
// roughness: 0 = sharpest, 1 = blurriest
real Hair_Specular(real shiftedTdotH, real roughness)
{
    real sinTH2 = max(1.0 - shiftedTdotH * shiftedTdotH, 1e-7);
    real specPower = lerp(256.0, 1.0, roughness * roughness);
    return pow(sinTH2, specPower * 0.5);
}

// Single Marschner lobe (R or TRT)
real3 Hair_Lobe(real TdotH, real NdotH, real TdotN, real shift,
                real roughness, real3 color, real strength)
{
    real sTdotH = Hair_ShiftedTdotH(TdotH, NdotH, TdotN, shift);
    real spec = Hair_Specular(sTdotH, roughness);
    return color * strength * spec;
}

// Full Marschner specular combining R + TRT paths
// Returns the specular contribution (add to diffuse output)
real3 BRDF_Hair(real TdotH, real NdotH, real TdotN,
                real primaryShift, real primaryRoughness, real3 primaryColor, real primaryStrength,
                real secondaryShift, real secondaryRoughness, real3 secondaryColor, real secondaryStrength)
{
    return Hair_Lobe(TdotH, NdotH, TdotN, primaryShift, primaryRoughness, primaryColor, primaryStrength)
         + Hair_Lobe(TdotH, NdotH, TdotN, secondaryShift, secondaryRoughness, secondaryColor, secondaryStrength);
}
#endif