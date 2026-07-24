Shader "Hidden/RimToonScreen"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _RimPower ("Rim Power", Range(0,10)) = 2.0
        _BlurScale ("Blur Scale", Range(0,1)) = 0.5
        _BlurIntensity ("Blur Intensity", Range(0,1)) = 0.5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Assets/Mine/Special/HLSL/RimLightFunction.hlsl"
    #include "Assets/Mine/Special/HLSL/BlurFunction.hlsl"

    #if SHADER_API_GLES
    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
    };
    #else
    struct Attributes
    {
        uint vertexID : SV_VertexID;
    };
    #endif

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
    };

    float _RimPower;
    float _BlurScale;
    float _BlurIntensity;

    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X(_RTTempMainTex);
    TEXTURE2D_X(_RimToonMaskRT);
    TEXTURE2D_X(_RimToonColorRT);
    TEXTURE2D_X(_RimToonBlurRT);

    Varyings Vert(Attributes input)
    {
        Varyings output;
    #if SHADER_API_GLES
        float4 pos = input.positionOS;
        float2 uv  = input.uv;
    #else
        float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
        float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
    #endif
        output.positionCS = pos;
        output.uv = uv;
        return output;
    }

    float4 Frag_Mask(Varyings input) : SV_Target
    {
        return float4(1.0, 1.0, 1.0, 1.0);
    }

    float4 Frag_ClearMask(Varyings input) : SV_Target
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }

    float4 Frag_Source(Varyings input) : SV_Target
    {
        Light  mainLight  = GetMainLight();
        float3 lightDirWS = mainLight.direction;
        float3 lightColor = mainLight.color.rgb;
        float3 normalWS   = SampleSceneNormals(input.uv);
        float  DepthRim   = RimLightDepth(lightDirWS, input.uv, _RimPower);
        float  DepthAtten = dot(normalWS, lightDirWS) * 0.5 + 0.5;

        float3 main = SAMPLE_TEXTURE2D_X(_RTTempMainTex, sampler_LinearClamp, input.uv).rgb;
        float  mask = SAMPLE_TEXTURE2D_X(_RimToonMaskRT, sampler_LinearClamp, input.uv).r;
        float  glow = mask * DepthAtten * DepthRim * 0.5;
        return float4(lightColor * glow + main * mask, mask);
    }

    float4 Frag_BlurH(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float2 texelSize = 1.0 / _ScreenParams.xy;
        float4 color = BlurHorizontal(uv, texelSize, _BlurScale, _MainTex, sampler_LinearClamp);
        return color;
    }

    float4 Frag_BlurV(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float2 texelSize = 1.0 / _ScreenParams.xy;
        float4 color = BlurVertical(uv, texelSize, _BlurScale, _MainTex, sampler_LinearClamp);
        return color;
    }

    half4 Frag_Mix(Varyings input) : SV_Target
    {
        half4 main = SAMPLE_TEXTURE2D_X(_RTTempMainTex, sampler_LinearClamp, input.uv);
        half  mask = SAMPLE_TEXTURE2D_X(_RimToonMaskRT, sampler_LinearClamp, input.uv).r;
        half4 glow = SAMPLE_TEXTURE2D_X(_RimToonColorRT, sampler_LinearClamp, input.uv);
        half4 blur = SAMPLE_TEXTURE2D_X(_RimToonBlurRT, sampler_LinearClamp, input.uv);
        return glow + main * (1.0 - mask) + blur * _BlurIntensity;
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "RimToonClearMask"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_ClearMask
            ENDHLSL
        }

        Pass
        {
            Name "RimToonMask"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            Stencil
            {
                Ref 1
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_Mask
            ENDHLSL
        }

        Pass
        {
            Name "RimToonGlowSource"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_Source
            ENDHLSL
        }

        Pass
        {
            Name "RimToonBlurH"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurH
            ENDHLSL
        }

        Pass
        {
            Name "RimToonBlurV"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurV
            ENDHLSL
        }

        Pass
        {
            Name "RimToonComposite"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_Mix
            ENDHLSL
        }
    }
}
