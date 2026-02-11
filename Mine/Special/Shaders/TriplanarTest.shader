Shader "Mine/Test"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Roughness ("Roughness", Range(0.0, 1.0)) = 0.5
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Mine/Special/HLSL/PBRFunction.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float3 _BaseColor;
                float _Roughness;
                float _Metallic;

            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 camPos = GetCameraPositionWS();
                output.viewDirWS = normalize(camPos - positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light light = GetMainLight();
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 lightDirWS = normalize(light.direction);

                float  roughness = lerp(0.15, 1.0, _Roughness);
                
                float3 irradiance = light.color * light.distanceAttenuation * light.shadowAttenuation;
                float3 radiance = irradiance * saturate(dot(normalWS, lightDirWS));
                float3 brdfMain = BRDFBurley(_BaseColor, normalWS, lightDirWS, viewDirWS, roughness, _Metallic) * PI;
                float3 brdfEnv  = BRDFEnv(_BaseColor, normalWS, viewDirWS, roughness, _Metallic, unity_SpecCube0, samplerunity_SpecCube0);
                float3 brdf  = brdfMain * radiance + brdfEnv;
                float3 color = brdf;
                
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}