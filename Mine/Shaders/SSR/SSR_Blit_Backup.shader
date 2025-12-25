Shader "Hidden/CelToon/SSR_Blit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off 
        ZTest Always 
        Cull Off
        Blend One Zero

        Pass
        {
            Name "SSR_Raymarch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };

            float _StepSize;
            float _MaxDistance;
            int _MaxSteps;
            float _Thickness;
            float _Smoothness;
            
            float4x4 _ViewProjectionMatrix;
            float4x4 _InverseViewProjectionMatrix;
            float3 _WorldSpaceCameraPosCustom;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                
                float rawDepth = SampleSceneDepth(uv);
                if(rawDepth <= 0.0001) return half4(0,0,0,0);
                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, _InverseViewProjectionMatrix);
                float3 viewDir   = normalize(positionWS - _WorldSpaceCameraPosCustom);
                float3 normalWS  = SampleSceneNormals(uv);
                float3 reflectDir = reflect(viewDir, normalWS);

                float ndotv = saturate(dot(normalWS, -viewDir));
                if(ndotv <= 0.0)
                    return half4(0,0,0,0);

                float  t       = 0.0;
                float  maxT    = _MaxDistance;
                float  stepT   = _StepSize;

                [loop]
                for(int i = 0; i < _MaxSteps && t <= maxT; i++)
                {
                    t += stepT;
                    float3 currentPos = positionWS + reflectDir * t;

                    float4 clipPos = mul(_ViewProjectionMatrix, float4(currentPos, 1));
                    float  k       = clipPos.w;
                    if(k <= 0.0) continue; 

                    float2 screenUV = (float2(clipPos.x, clipPos.y * _ProjectionParams.x) / k) * 0.5 + 0.5;
                    
                    if(screenUV.x < 0 || screenUV.x > 1 || screenUV.y < 0 || screenUV.y > 1)
                        break;

                    float sceneRawDepth   = SampleSceneDepth(screenUV);
                    float sceneLinearDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                    float rayLinearDepth = k; 
                    float depthDiff = rayLinearDepth - sceneLinearDepth;
                    if(depthDiff > 0 && depthDiff < _Thickness)
                    {
                        float3 color = SampleSceneColor(screenUV);
                        float  alpha = 1.0;
                        float2 edge  = abs(screenUV * 2 - 1);
                        float  edgeFade = 1 - pow(max(edge.x, edge.y), 4);
                        alpha *= edgeFade;
                        float facing = smoothstep(0.0, 0.2, ndotv);
                        alpha *= facing;

                        return half4(color, alpha * _Smoothness);
                    }
                }

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
