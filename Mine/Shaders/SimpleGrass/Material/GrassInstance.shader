Shader "GrassInstance"
{
    Properties 
    {
        _Base_Color_A ("Base Color A", Color) = (1,1,1,1)
        _Base_Color_B ("Base Color B", Color) = (0,0,0,1)
        _Grass ("Grass Texture", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0,1)) = 0.5

        _Wind_Color_A ("Wind Color A", Color) = (1,1,1,1)
        _Wind_Color_B ("Wind Color B", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalRenderPipeline" 
            "RenderType"="Opaque" 
        }

        Pass
        {
            Cull Off
            Blend One Zero
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Mine/Special/HLSL/CustomLighting.hlsl"
            #include "Assets/Mine/Special/HLSL/LightFunction.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct GrassProperties
            {
                float3 offset;
                float3 normal;
                float height;
            };

            StructuredBuffer<float4x4> _MeshProperties;
            StructuredBuffer<GrassProperties> _GrassProperties;
            StructuredBuffer<uint> _ClipProperties;

            // Textures and Properties
            sampler2D _Grass;
            float4 _Base_Color_A;
            float4 _Base_Color_B;
            float _AlphaClipThreshold;
            float _Smoothness;

            float4 _Wind_Color_A;
            float4 _Wind_Color_B;

            struct appdata
            {
                float4 positionOS : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;

                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 positionHCS : SV_POSITION;
                float4 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float posdepth : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;

                uint clipIndex = _ClipProperties[v.instanceID];
                float4x4 meshProp = _MeshProperties[clipIndex];
                GrassProperties grassProp = _GrassProperties[clipIndex];

                float3 grassOffset = grassProp.offset * v.uv.y;
                float3 grassNormal = grassProp.normal;

                float3 N = grassNormal;
                float3 up = abs(N.y) > 0.99 ? float3(1,0,0) : float3(0,1,0);
                float3 T = normalize(cross(up, N));
                float3 B = cross(N, T);

                float3x3 TNB = float3x3(T, N, B);
                float4 positionOffsetNS = float4(mul(grassOffset, TNB), 0);

                o.positionWS = mul(meshProp, v.positionOS) + positionOffsetNS;
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = grassNormal;
                o.uv = v.uv;

                o.posdepth = grassOffset.y;

                return o;
            }

            half4 frag(v2f i): SV_Target
            {
                // Base Color
                half4 grassTex = tex2D(_Grass, i.uv);
                clip(grassTex.a - _AlphaClipThreshold);
                half4 grasscolor = lerp(_Base_Color_B, _Base_Color_A, i.uv.y) * grassTex;
                half4 windColor = lerp(_Wind_Color_B, _Wind_Color_A, i.posdepth);
                half4 basecolor = saturate(grasscolor * windColor);

                // Lighting Functions
                float3 LightDirection;
                float3 LightColor;
                float DistanceAtten, ShadowAtten;

                MainLight_float(i.positionWS.xyz, LightDirection, LightColor, DistanceAtten, ShadowAtten);

                float3 N = normalize(i.normalWS.xyz);
                float3 L = normalize(LightDirection);
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS.xyz);

                float3 Diffuse = DiffuseLambert(N, L);
                float3 Specular = SpecularBlinnPhong(N, L, V, _Smoothness);   
                float3 Light = saturate(Diffuse + Specular);
                float3 direct = Light * LightColor;
                float3 ambient = SampleSH(N);

                half4 finalColor = float4(lerp(ambient, direct, ShadowAtten), 1.0) * basecolor;
                return finalColor;
            }

            ENDHLSL
        }
    }
}
