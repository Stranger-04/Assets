Shader "Hidden/CelToon/SSL_ScreenSpaceLight"
{
    Properties
    {
        _Intensity("SSL Intensity", Float) = 1.0
        _MaxSteps("Max Steps", Int) = 32
        _MaxDistance("Max Distance", Float) = 10.0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

    float  _Intensity;
    int    _MaxSteps;
    float  _MaxDistance;
    float  _BlurScale;
    float  _JitterScale;

    Texture2D _MainTex;
    SAMPLER(sampler_MainTex);
    Texture2D _SSLResultRT;
    SAMPLER(sampler_SSLResultRT);

    float3 ReconstructWorldPos(float2 uv, float3 CameraPos, float MaxDistance)
    {
        float rawDepth = SampleSceneDepth(uv);
        return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
    }

    float SampleShadow(float3 positionCS)
    {
        float4 shadowCoord = TransformWorldToShadowCoord(positionCS);
        return SAMPLE_TEXTURE2D_SHADOW(
            _MainLightShadowmapTexture, 
            sampler_MainLightShadowmapTexture, 
            shadowCoord);
    }

    float SSL(
        float3 CameraPos,
        float3 WorldPosFromDepth,
        int MaxSteps,
        float MaxDistance
    )
    {
        float3 RayVector = WorldPosFromDepth - CameraPos;
        float3 RayDir = normalize(RayVector);
        float RayLength = clamp(length(RayVector), 0.0, MaxDistance);

        float StepSize = RayLength / MaxSteps;
        float3 pCurrent = CameraPos;
        float Density = 0.0;

        [loop]
        for (int i = 0; i < MaxSteps; i++)
        {
            if (length(pCurrent - CameraPos) > MaxDistance)
            {    
                break;
            }

            float lighting = SampleShadow(pCurrent);

            pCurrent += RayDir * StepSize;
            float3 jitter = float3(Hash(pCurrent.xy), Hash(pCurrent.yx), Hash(pCurrent.xy));
            pCurrent += jitter * (_JitterScale / MaxSteps);
            Density += StepSize * lighting;
        }

        Density /= MaxSteps;
        return Density;
    }

    Varyings Vert (Attributes v)
    {
        Varyings o;
        o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
        o.uv = v.uv;
        return o;
    }

    half4 Frag (Varyings i) : SV_Target
    {
        float2 uv = i.uv;
        Light mainLight = GetMainLight();

        float3 cameraPosWS = GetCameraPositionWS();
        float3 worldPos = ReconstructWorldPos(uv, cameraPosWS, _MaxDistance);

        float density = SSL(
            cameraPosWS,
            worldPos,
            _MaxSteps,
            _MaxDistance
        );

        float sslLight = density * _Intensity;
        float3 sslColor = mainLight.color * sslLight;
        return float4(sslColor, 1.0);
    }

    half4 Frag_Mix(Varyings i) : SV_Target
    {
        half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
        half4 lightColor = SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, i.uv);
        return mainColor + lightColor;
    }

    float4 Frag_BlurHorizontal(Varyings i) : SV_Target
    {
        float2 uv = i.uv;
        float2 texelSize = 1.0 / _ScreenParams.xy;

        float4 color = float4(0,0,0,0);
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(-2.0, 0.0)) * 0.1216216;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(-1.0, 0.0)) * 0.2332432;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv) * 0.290918;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(1.0, 0.0)) * 0.2332432;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(2.0, 0.0)) * 0.1216216;

        return color;
    }

    float4 Frag_BlurVertical(Varyings i) : SV_Target
    {
        float2 uv = i.uv;
        float2 texelSize = 1.0 / _ScreenParams.xy;

        float4 color = float4(0,0,0,0);
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(0.0, -2.0)) * 0.1216216;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(0.0, -1.0)) * 0.2332432;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv) * 0.290918;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(0.0, 1.0)) * 0.2332432;
        color += SAMPLE_TEXTURE2D(_SSLResultRT, sampler_SSLResultRT, uv + _BlurScale * texelSize * float2(0.0, 2.0)) * 0.1216216;

        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            Name "SSL_ScreenSpaceLight"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "SSL_BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "SSL_BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_BlurVertical
            ENDHLSL
        }

        Pass
        {
            Name "SSL_Mix"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_Mix
            ENDHLSL
        }
    }

    FallBack Off
}