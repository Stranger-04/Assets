//DeclareCustomTexture.hlsl
#ifndef DECLARE_CUSTOM_TEXTURE_HLSL_INCLUDED
#define DECLARE_CUSTOM_TEXTURE_HLSL_INCLUDED

//Custom Screen Texture Sampling Function
float4 SampleCustomTexture(Texture2D tex, SamplerState sam, float2 uv)
{
    uv = UnityStereoTransformScreenSpaceTex(uv);
    return SAMPLE_TEXTURE2D_X(tex, sam, uv);
}

float3 SampleCustomNormals(Texture2D tex, SamplerState sam, float2 uv)
{
    uv = UnityStereoTransformScreenSpaceTex(uv);
    float3 normal = SAMPLE_TEXTURE2D_X(tex, sam, uv).rgb;

    #if defined(_GBUFFER_NORMALS_OCT)
    float2 remap1 = Unpack888ToFloat2(normal);
    float2 remap2 = remap1 * 2.0 - 1.0;
    normal = UnpackNormalOctQuadEncode(remap2);
    #endif

    return normal;
}
#endif