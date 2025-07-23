Shader "Custom/PortalShader"
{
    Properties
    {
        _MaskColor ("MaskColor", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        
    }
    SubShader
    {
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MaskColor;
            float mask;

            struct appdata
            {
                float4 vertex : POSITION;
                
            };
            
            struct v2f
            {
                float4 screenPos : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                float2 screenSpaceUV = i.screenPos.xy/i.screenPos.w;
                
                fixed4 c = tex2D(_MainTex,screenSpaceUV);

                return c*(1-mask)+_MaskColor*mask;
                
            }


            ENDCG



        }


        
    }
}
