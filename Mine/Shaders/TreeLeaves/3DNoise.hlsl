// 输入：float3 p（一般用 worldPos * scale）
// 输出：0~1 的噪声值
float3 permute(float3 x) {
    return frac((34.0 * x + 1.0) * x) * 289.0;
}

float noise3D(float3 p) {
    float3 i = floor(p);
    float3 f = frac(p);

    // 四次方插值，保证平滑过渡
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = dot(i + float3(0,0,0), float3(1,57,113));
    float n100 = dot(i + float3(1,0,0), float3(1,57,113));
    float n010 = dot(i + float3(0,1,0), float3(1,57,113));
    float n110 = dot(i + float3(1,1,0), float3(1,57,113));
    float n001 = dot(i + float3(0,0,1), float3(1,57,113));
    float n101 = dot(i + float3(1,0,1), float3(1,57,113));
    float n011 = dot(i + float3(0,1,1), float3(1,57,113));
    float n111 = dot(i + float3(1,1,1), float3(1,57,113));

    float3 fade = u;

    // 三维插值
    float n00 = lerp(n000, n100, fade.x);
    float n01 = lerp(n001, n101, fade.x);
    float n10 = lerp(n010, n110, fade.x);
    float n11 = lerp(n011, n111, fade.x);

    float n0 = lerp(n00, n10, fade.y);
    float n1 = lerp(n01, n11, fade.y);

    float n = lerp(n0, n1, fade.z);

    // 映射到 0~1
    return frac(sin(n) * 43758.5453);
}
