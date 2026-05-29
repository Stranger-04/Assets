Shader "Custom/Kuwahara"
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

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS);
        output.uv = input.uv;
        return output;
    }

    half4 Frag_Kuwahara_Basic(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float2 texelSize = 1.0 / _ScreenParams.xy;
        int radius = _Radius;

        float3 mean[4];
        float3 meanSq[4];
        int count[4];

        for (int i = 0; i < 4; i++)
        {
            mean[i] = float3(0,0,0);
            meanSq[i] = float3(0,0,0);
            count[i] = 0;
        }

        // Box region each
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > radius * radius) continue;
                int region = 0;
                float2 sampleUV = uv + float2(x, y) * texelSize;
                float3 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, sampleUV).rgb;
                
                if (x >= 0 && y >= 0) region = 0;
                else if (x < 0 && y > 0) region = 1;
                else if (x < 0 && y < 0) region = 2;
                else region = 3;
                
                mean[region] += sampleColor;
                meanSq[region] += sampleColor * sampleColor;
                count[region]++;
            }
        }

        // Color each region
        float3 finalColor = float3(0,0,0);
        float minVariance = 1e+10;

        for (int i = 0; i < 4; i++)
        {
            mean[i] /= count[i];
            meanSq[i] /= count[i];
            float3 variance = meanSq[i] - mean[i] * mean[i];
            float varianceSum = variance.x + variance.y + variance.z;

            if (varianceSum < minVariance)
            {
                minVariance = varianceSum;
                finalColor = mean[i];
            }
        }

        return half4(finalColor, 1.0);
    }

    half4 Frag_Kuwahara_Generalized(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float2 texelSize = 1.0 / _ScreenParams.xy;
        int radius = _Radius;

        float3 mean[8];
        float3 meanSq[8];
        int count[8];

        for (int i = 0; i < 8; i++)
        {
            mean[i] = float3(0,0,0);
            meanSq[i] = float3(0,0,0);
            count[i] = 0;
        }
        // Octagonal regions
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int region = 0;
                float2 sampleUV = uv + float2(x, y) * texelSize;
                float3 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, sampleUV).rgb;
                
                float angleUV = atan2((float)y, (float)x);
                float angleRegion = (3.14159265 / 4.0);

                region = (int)floor((angleUV + 3.14159265) / angleRegion) % 8;
                
                mean[region] += sampleColor;
                meanSq[region] += sampleColor * sampleColor;
                count[region]++;
            }
        }

        // Color each region
        float3 finalColor = float3(0,0,0);
        float weightSum = 0.0;
        float3 colorSum = float3(0,0,0);

        for (int i = 0; i < 8; i++)
        {
            mean[i] /= count[i];
            meanSq[i] /= count[i];
            float3 variance = meanSq[i] - mean[i] * mean[i];
            float varianceSum = variance.x + variance.y + variance.z;

            float weight = 1.0 / (varianceSum + 1.0);
            weightSum += weight;
            colorSum += mean[i] * weight;
        }
        finalColor = colorSum / weightSum;

        return half4(finalColor, 1.0);
    }

    half4 Frag(Varyings input) : SV_Target
    {
        #if defined(KUWAHARA_BASIC)
            return Frag_Kuwahara_Basic(input);
        #elif defined(KUWAHARA_GENERALIZED)
            return Frag_Kuwahara_Generalized(input);
        #endif
        return Frag_Kuwahara_Basic(input); 
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "Kuwahara"

            Blend One Zero

            HLSLPROGRAM
            #pragma multi_compile _ KUWAHARA_BASIC KUWAHARA_GENERALIZED
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
