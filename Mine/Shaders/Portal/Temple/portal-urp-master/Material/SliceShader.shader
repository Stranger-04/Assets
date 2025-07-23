// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/SliceShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Tags{"RenderType" = "Geometry" "RenderType"="Geometry"  "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline"}
        LOD 200
        

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            float3 sliceCenter;
            float3 sliceNormal;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv:TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                float side = dot(sliceNormal,i.worldPos-sliceCenter);
                clip(-side);

                fixed4 c = tex2D(_MainTex,i.uv*_MainTex_ST.xy+_MainTex_ST.zw)*_Color;
                
                return c;
            }


            ENDCG



        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            float3 sliceCenter;
            float3 sliceNormal;

            // struct appdata
            // {
            //     float4 vertex : POSITION;
            //     float4 texcoord:TEXCOORD0;
            // };
            
            struct v2f
            {
                V2F_SHADOW_CASTER;
                float3 worldPos : TEXCOORD1;
                
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                //o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;
                
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                float side = dot(sliceNormal,i.worldPos-sliceCenter);
                clip(-side);

                SHADOW_CASTER_FRAGMENT(i)
            }


            ENDCG


        }
        
    }
    FallBack "Transparent/Cutout/VertexLit"
}
