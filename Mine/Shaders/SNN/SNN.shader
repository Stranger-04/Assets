Shader "Custom/SNN"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _Radius ("Radius", Range(1,10)) = 5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
    };

    float _Radius;

    TEXTURE2D_X(_MainTex);

    float ComputePixelDistance(float3 colorA, float3 colorB)
    {
        float3 diff = colorA - colorB;
        return dot(diff, diff);
    }

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS);
        output.uv = input.uv;
        return output;
    }

    half4 Frag_SNN(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float2 texelSize = 1.0 / _ScreenParams.xy;
        float3 colorOrigin = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv).rgb;

        float  radius = _Radius;
        float3 colorSum = 0.0;
        int count = 0;

        for (int y = 0; y <= radius; y++)
        {
            for (int x = 0; x <= radius; x++)
            {
                float3 colorA = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + float2(x, y) * texelSize).rgb;
                float3 colorB = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + float2(-x, -y) * texelSize).rgb;

                float distA = ComputePixelDistance(colorOrigin, colorA);
                float distB = ComputePixelDistance(colorOrigin, colorB);

                if (distA < distB)
                {
                    colorSum += colorA;
                }
                else
                {
                    colorSum += colorB;
                }
                count++;
            }
        }
        colorSum /= count;
        return half4(colorSum, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "SNN"

            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_SNN
            ENDHLSL
        }
    }
}