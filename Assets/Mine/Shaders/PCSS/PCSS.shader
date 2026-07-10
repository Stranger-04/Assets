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
        float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);
        if (linearDepth > 0.9999)
            return half4(1, 0, 0, 1); // RED = sky

        // ── 级联选择：基于 linear depth（与 PSSM split 的比较）──
        int ci = 0;
        if (linearDepth > _CascadeSplits.x) ci = 1;
        if (linearDepth > _CascadeSplits.y) ci = 2;
        if (linearDepth > _CascadeSplits.z) ci = 3;
        ci = min(ci, _CascadeCount - 1);

        float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

        float4x4 cascadeVP = _CascadeLightVP[ci];
        float4 positionCS = mul(cascadeVP, float4(positionWS, 1.0));
        float2 shadowUV = positionCS.xy / max(positionCS.w, 0.0001) * 0.5 + 0.5;

        if (shadowUV.x < 0.0 || shadowUV.x > 1.0 || shadowUV.y < 0.0 || shadowUV.y > 1.0)
            return half4(1, 1, 0, 1); // YELLOW = out of cascade frustum

        // ── Tile → Atlas UV 变换 ──
        float4 atlas = _CascadeAtlasOffset[ci];
        float2 atlasUV = shadowUV * atlas.z + atlas.xy;

        float shadowDepth = SAMPLE_TEXTURE2D_X(_PCSS_ShadowCacheTex, sampler_PCSS_ShadowCacheTex, atlasUV).r;

        float clipZ = positionCS.z / max(positionCS.w, 0.0001);

        // ── 硬阴影比较 ──
        // shadowDepth = SV_POSITION.z，已经过硬件 viewport transform。
        // clipZ = 纯矩阵乘法的 clip space z ∈ [-1,1]（Matrix4x4.Ortho）。
        // 必须把 clipZ 变换到相同范围再比较。
        #if UNITY_REVERSED_Z
            float receiverDepth = clipZ * (-0.5) + 0.5;   // [-1,1]→[1,0]
            float shadowMask = (shadowDepth > receiverDepth) ? 0.0 : 1.0;
        #else
            float receiverDepth = clipZ * 0.5 + 0.5;       // [-1,1]→[0,1]
            float shadowMask = (shadowDepth < receiverDepth) ? 0.0 : 1.0;
        #endif
        return shadowMask;
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
