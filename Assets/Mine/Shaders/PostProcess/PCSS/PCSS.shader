// ════════════════════════════════════════════════════════════
//  PCSS 屏幕空间软阴影 — 双边模糊 Pass
//  PCSS 核心计算已移至 PCSS.compute（Compute Shader）
//  此 Shader 仅保留 BlurH / BlurV 两个 Pass，
//  由 PCSSFeature.cs 通过 Blitter.BlitCameraTexture 调用
// ════════════════════════════════════════════════════════════
Shader "Hidden/PCSS/ScreenSpaceShadow"
{
    Properties { }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Assets/Mine/Special/HLSL/BlurFunction.hlsl"
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurH
            float _BlurScale;
            half4 Frag_BlurH(Varyings input) : SV_Target
            {
                float2 texelSize = 1.0 / _ScreenParams.xy;
                return BilateralBlurHorizontal(input.texcoord, texelSize, _BlurScale, _BlitTexture, sampler_LinearClamp);
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurV
            float _BlurScale;
            half4 Frag_BlurV(Varyings input) : SV_Target
            {
                float2 texelSize = 1.0 / _ScreenParams.xy;
                return BilateralBlurVertical(input.texcoord, texelSize, _BlurScale, _BlitTexture, sampler_LinearClamp);
            }
            ENDHLSL
        }
    }
}
