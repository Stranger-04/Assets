//HSV.hlsl
#ifndef HSV_HLSL_INCLUDED
#define HSV_HLSL_INCLUDED

//RGB to HSV conversion
float3 RGBtoHSV(float3 rgb)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy), step(rgb.b, rgb.g));
    float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    float3 hsv;
    hsv.x = abs(q.z + (q.w - q.y) / (6.0 * d + e));
    hsv.y = d / (q.x + e);
    hsv.z = q.x;
    return hsv;
}

//HSV to RGB conversion
float3 HSVtoRGB(float3 hsv)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(hsv.x + K.xyz) * 6.0 - K.www);
    float3 rgb = hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
    return rgb;
}
#endif