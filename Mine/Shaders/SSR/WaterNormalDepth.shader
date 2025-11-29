Shader "Hidden/WaterNormalDepth"
{
    Properties 
    {
        _Normal_Speed ("Normal Speed", Float) = 1.0
        _Normal_Scale ("Normal Scale", Float) = 1.0
        _Normal_Strength ("Normal Strength", Float) = 1.0
        _Normal_Direction1 ("Normal Direction 1", Vector) = (1,0,0,0)
        _Normal_Direction2 ("Normal Direction 2", Vector) = (0,1,0,0)
        _NormalMap ("Normal Map", 2D) = "bump" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "WaterNormalDepth"
            Tags { "LightMode" = "WaterNormalDepth" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 tangentWS  : TEXCOORD1;
                float3 bitangentWS: TEXCOORD2;
                float3 viewPos    : TEXCOORD3;
                float2 uv         : TEXCOORD4;
            };

            sampler2D _NormalMap;
            float _Normal_Speed;
            float _Normal_Scale;
            float _Normal_Strength;
            float4 _Normal_Direction1;
            float4 _Normal_Direction2;

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(v.normalOS);
                float3 worldTangent = TransformObjectToWorldDir(v.tangentOS.xyz);
                float3 worldBitangent = cross(worldNormal, worldTangent) * v.tangentOS.w;
                
                o.positionCS = TransformWorldToHClip(worldPos);
                o.normalWS = normalize(worldNormal);
                o.tangentWS = normalize(worldTangent);
                o.bitangentWS = normalize(worldBitangent);
                o.viewPos = TransformWorldToView(worldPos);
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float t = _Time.x;
                float2 offset1 = t * _Normal_Speed * _Normal_Direction1.xy;
                float2 offset2 = t * _Normal_Speed * _Normal_Direction2.xy;
                float2 uv1 = offset1 + i.uv * _Normal_Scale;
                float2 uv2 = offset2 + i.uv * _Normal_Scale;
                
                float3 n1 = UnpackNormal(tex2D(_NormalMap, uv1));
                float3 n2 = UnpackNormal(tex2D(_NormalMap, uv2));
                float3 normalTS = lerp(n1, n2, 0.5);
                
                normalTS.xy *= _Normal_Strength;
                normalTS = normalize(normalTS);
                
                float3x3 TBN = float3x3(i.tangentWS, i.bitangentWS, i.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));
                
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS));
                
                float4 clip = TransformWViewToHClip(float4(i.viewPos, 1));
                float deviceDepth01 = clip.z / max(clip.w, 1e-6);
                
                return float4(normalVS, deviceDepth01);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
