// ═══════════════════════════════════════════════════════════════
//  Unity 6 URP 17+ 全屏后处理 Shader 模板
//
//  使用方式：
//    1. 复制此文件，改名为 YourEffect.shader
//    2. 修改 Shader "Hidden/YourEffect"
//    3. 在 HLSLINCLUDE 中替换参数和 Frag 函数
//    4. 在 SubShader 中添加所需的 Pass
//
//  ⚠️ 不要忘记两个 include 的顺序 (Core.hlsl 必须在 Blit.hlsl 之前)
// ═══════════════════════════════════════════════════════════════

Shader "Hidden/PostProcessTemplate"
{
    Properties
    {
        // ⚠️ 替换为你的参数
        _Intensity ("Intensity", Float) = 1.0
    }

    HLSLINCLUDE
    // ⚠️ Core.hlsl 必须在 Blit.hlsl 之前
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float _Intensity;
    CBUFFER_END

    // Blit.hlsl 提供:
    //   struct Varyings { float4 positionCS; float2 texcoord; };
    //   Varyings Vert(Attributes input);
    //   TEXTURE2D_X(_BlitTexture);
    //   SAMPLER(sampler_LinearClamp);

    half4 Frag(Varyings input) : SV_Target
    {
        // ⚠️ 用 SAMPLE_TEXTURE2D_X，不是 SAMPLE_TEXTURE2D
        float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
        color *= _Intensity;
        return half4(color, 1.0);
    }
    ENDHLSL

    SubShader
    {
        // ⚠️ 6 个必需标签: Opaque + UniversalPipeline
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Main"
            HLSLPROGRAM
            #pragma vertex Vert        // ← Blit.hlsl 提供
            #pragma fragment Frag
            #pragma target 2.0         // ← Metal 强烈建议
            ENDHLSL
        }
    }
}
