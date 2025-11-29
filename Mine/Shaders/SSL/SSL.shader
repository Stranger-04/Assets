Shader "Custom/SSL_ScreenSpaceLight"
{
    Properties
    {
        _MainTex("Color Texture", 2D) = "white" {}
        _MaxSteps("Max Steps", Int) = 32
        _MaxDistance("Max Distance", Float) = 10.0
        _Intensity("SSL Intensity", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Transparent"
        }

        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "SSL_ScreenSpaceLight"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP 通用 include
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            int   _MaxSteps;
            float _MaxDistance;
            float _Intensity;

            float SSL_float(
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

                for (int i = 0; i < MaxSteps; i++)
                {
                    Light mainLight = GetMainLight();
                    float4 shadowCoord = TransformWorldToShadowCoord(pCurrent);
                    float lighting = MainLightRealtimeShadow(shadowCoord);

                    pCurrent += RayDir * StepSize;
                    Density += StepSize * lighting;
                }

                Density /= MaxSteps;
                return Density;
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                return o;
            }

            float3 ReconstructWorldPos(float2 uv)
            {
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
                float4 positionCS = float4(uv * 2.0 - 1.0, depth, 1.0);
                float4 positionWS = TransformClipToWorld(positionCS);
                return positionWS.xyz / positionWS.w;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                float3 cameraPosWS = GetCameraPositionWS();
                float3 worldPos = ReconstructWorldPos(i.uv);

                float density = SSL_float(
                    cameraPosWS,
                    worldPos,
                    _MaxSteps,
                    _MaxDistance
                );

                float factor = 1.0 + density * _Intensity;
                col.rgb *= factor;

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}