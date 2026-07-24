Shader "Mine/Interaction/Debug"
{
    // ═══════════════════════════════════════════════════════════════
    //  测试可视化 Shader — 在平面/网格上显示 _InteractionResultTex。
    //  用 _InteractionOrthoV / _InteractionOrthoP 将世界坐标映射到 RT UV。
    // ═══════════════════════════════════════════════════════════════

    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    float4 _BaseColor;

    TEXTURE2D(_InteractionResultTex);
    SAMPLER(sampler_InteractionResultTex);

    float4x4 _InteractionOrthoV;
    float4x4 _InteractionOrthoP;
    float3   _InteractionAreaPos;

    struct Attributes
    {
        float3 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionHCS : SV_POSITION;
        float2 uv          : TEXCOORD0;
        float  interaction : TEXCOORD1;
    };

    Varyings Vert(Attributes i)
    {
        Varyings o;
        float3 worldPos = TransformObjectToWorld(i.positionOS);
        o.positionHCS   = TransformWorldToHClip(worldPos);
        o.uv            = i.uv;

        // 用正交 VP 矩阵将世界坐标映射到 RT UV
        float4 clipPos = mul(_InteractionOrthoP, mul(_InteractionOrthoV, float4(worldPos, 1.0)));
        float2 rtUV = clipPos.xy / clipPos.w * 0.5 + 0.5;
        o.interaction = SAMPLE_TEXTURE2D_LOD(_InteractionResultTex, sampler_InteractionResultTex, rtUV, 0).r;

        return o;
    }

    half4 Frag(Varyings i) : SV_Target
    {
        // 热力图: 0=蓝, 0.5=绿, 1=红
        half3 cold   = half3(0.0, 0.0, 0.5);
        half3 warm   = half3(0.5, 1.0, 0.0);
        half3 hot    = half3(1.0, 0.0, 0.0);
        half3 color  = i.interaction < 0.5
            ? lerp(cold, warm, i.interaction * 2.0)
            : lerp(warm, hot, (i.interaction - 0.5) * 2.0);

        return half4(color * _BaseColor.rgb, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalRenderPipeline"
        }
        LOD 100

        Pass
        {
            Name "Debug"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
