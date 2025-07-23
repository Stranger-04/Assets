Shader "Unlit/VFaceClip"
{
	Properties
	{
		_ClipValue ("clipValue", float) = 0.5
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Cull off
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				float3 Pos:TEXCOORD1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed _ClipValue;
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.Pos = mul(unity_ObjectToWorld,v.vertex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i,fixed facing:VFACE) : SV_Target
			{
			    if(i.Pos.y>_ClipValue)
				{
					clip(-1);
				}
				if(facing<0)
				return fixed4(1,1,1,1);
				return fixed4(0,0,0,1);
			}
			ENDCG
		}
	}
}
