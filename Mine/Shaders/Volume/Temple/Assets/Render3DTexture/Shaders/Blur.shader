Shader "Unlit/SimpleBlur"
{
	Properties
	{
		_VolumeTex("VolumeTex",3D) = "black"{}
		_offset("Offset", float) = 0
		_stepSize("StepSize",range(0,2)) = 1
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque"  "Queue" = "Geometry"}

		Pass
		{
			 CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};
			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 posW :  TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			Texture3D<float4> _VolumeTex;
			SamplerState  sampler_VolumeTex;

			float _offset;
			float _stepSize;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(float4(v.vertex.xyz, 1.0));
				o.uv = v.uv;
				o.posW = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float3 tex3DUvw = float3(i.uv.x, _offset,i.uv.y);
				float colorSample = 0;
				float colAcc = 0;
				float dx = 0.5;

				for (int z = -1; z < 2; z++)
				for (int y = -1; y < 2; y++)
				for (int x = -1; x < 2; x++)
				{
					float3 offset = float3(x, y, z)  *_stepSize;
					colorSample += _VolumeTex.SampleLevel(sampler_VolumeTex, tex3DUvw + offset, 0).r;
				}

				colorSample = colorSample / 27.0f;

				return colorSample;
			}
		ENDCG
		}
	}
}