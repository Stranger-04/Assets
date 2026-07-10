Shader "Mine/PCSS/CustomShadowCaster"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _ShadowDepthBias ("Depth Bias", Range(0, 2)) = 0.5
        _ShadowNormalBias ("Normal Bias", Range(0, 2)) = 0.4
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float  _Cutoff;
    CBUFFER_END

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    float3 _LightDirection;
    float  _ShadowDepthBias;
    float  _ShadowNormalBias;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS   : NORMAL;
        float2 uv         : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
    };

    // ════════════════════════════════════════════════════════════
    //  Vert — ApplyShadowBias + MVP，防止自阴影（shadow acne）
    //  参考 Unity URP ShadowCasterPass.hlsl + Common Techniques
    // ════════════════════════════════════════════════════════════
    Varyings Vert(Attributes input)
    {
        Varyings output;

        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

        // ── 深度偏移：沿法线（坡度缩放）+ 沿光源方向 ──
        // 法线与光源越接近平行（N·L → 0），自阴影越严重，偏移越大
        float  NdotL      = saturate(dot(_LightDirection, normalWS));
        float  invNdotL   = 1.0 - NdotL;
        float  slopeBias  = invNdotL * _ShadowNormalBias;
        positionWS += _LightDirection * _ShadowDepthBias;
        positionWS += normalWS * slopeBias;

        output.positionCS = TransformWorldToHClip(positionWS);
        output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        return output;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "CustomShadowCaster"
            Tags { "LightMode" = "CustomShadowCaster" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float Frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "CustomShadowCasterAlpha"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON

            float Frag(Varyings input) : SV_Target
            {
                #if _ALPHATEST_ON
                    float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                    clip(alpha - _Cutoff);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
