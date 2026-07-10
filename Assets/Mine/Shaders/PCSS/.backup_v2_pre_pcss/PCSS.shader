Shader "Hidden/PCSS/ScreenSpaceShadow"
{
    Properties { }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D_X(_PCSS_ShadowCacheTex);
    SAMPLER(sampler_PCSS_ShadowCacheTex);

    int      _CascadeCount;
    float4   _CascadeSplits;
    float4x4 _CascadeLightVP[4];
    float4   _CascadeAtlasOffset[4];

    half4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;

        float rawDepth = SampleSceneDepth(uv);
        float linear01 = Linear01Depth(rawDepth, _ZBufferParams);
        if (linear01 > 0.9999)
            return half4(1, 0, 0, 1); // RED = sky

        float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

        // ── 级联选择：世界空间距离（参考 Unity ComputeCascadeIndex）──
        // LinearEyeDepth 是视空间深度，旋转时变化 → 级联选择漂移 → 抖动。
        // 世界空间距离 rotation-invariant → 级联选择稳定。
        float distCam = distance(positionWS, _WorldSpaceCameraPos);
        int ci = 0;
        if (distCam > _CascadeSplits.x) ci = 1;
        if (distCam > _CascadeSplits.y) ci = 2;
        if (distCam > _CascadeSplits.z) ci = 3;
        ci = min(ci, _CascadeCount - 1);

        // ── 矩阵已含 reversed-Z + scale-bias（参考 Unity GetShadowTransform）──
        // C# 侧将 reversed-Z 修正 + [-1,1]→[0,1] 的 scale-bias 烘焙进矩阵。
        // shader 只需做一次矩阵乘法 + 除 w → 直接得到 [0,1] 的 UV 和深度。
        float4x4 cascadeVP = _CascadeLightVP[ci];
        float4 shadowCoord = mul(cascadeVP, float4(positionWS, 1.0));
        float2 shadowUV = shadowCoord.xy / shadowCoord.w;
        float  receiverDepth = shadowCoord.z / shadowCoord.w;

        if (shadowUV.x < 0.0 || shadowUV.x > 1.0 || shadowUV.y < 0.0 || shadowUV.y > 1.0)
            return half4(1, 1, 0, 1); // YELLOW = out of cascade frustum

        // ── Tile → Atlas UV ──
        float4 atlas = _CascadeAtlasOffset[ci];
        float2 atlasUV = shadowUV * atlas.z + atlas.xy;

        // ── 2×2 PCF（对齐 Unity 硬件 PCF 的基础滤波）──
        // 硬阴影无过渡 → 锯齿随屏幕同步移动。2×2 PCF 提供 4 级灰度过渡。
        float texelSize = 1.0 / 2048.0;
        float2 pcfOff[4] = {
            float2(-0.5, -0.5) * texelSize, float2(0.5, -0.5) * texelSize,
            float2(-0.5,  0.5) * texelSize, float2(0.5,  0.5) * texelSize
        };
        float pcf = 0.0;
        for (int i = 0; i < 4; i++)
        {
            float sd = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, atlasUV + pcfOff[i]).r;
            #if UNITY_REVERSED_Z
                pcf += (sd > receiverDepth) ? 0.0 : 1.0;
            #else
                pcf += (sd < receiverDepth) ? 0.0 : 1.0;
            #endif
        }
        return pcf * 0.25;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PCSS_Cascade"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
