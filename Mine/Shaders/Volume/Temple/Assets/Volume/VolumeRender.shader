// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "VolumeRender"
{
	Properties
	{
		_Texture0("Texture 0", 3D) = "white" {}
		m_DensityMul("DensityMul", Float) = 0
		m_DensityMul1("AbsorptionThroughCloud", Float) = 0
		m_DensityMul2("AbsorptionTowardSun", Float) = 0

	}
	
	SubShader
	{
		
		Tags { "RenderType"="Opaque" "Queue"="Transparent" }
	LOD 100

		CGINCLUDE
		#pragma target 3.0
		ENDCG
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Back
		ColorMask RGBA
		ZWrite Off
		ZTest LEqual
		Offset 0 , 0
		
		
		
		Pass
		{
			Name "Unlit"
			Tags { "LightMode"="ForwardBase" }
			CGPROGRAM

			

			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			//only defining to not throw compilation error over Unity 5.5
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#include "Lighting.cginc"
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#include "VolumeRender.cginc"


			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 worldPos : TEXCOORD0;
#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				float4 ase_texcoord1 : TEXCOORD1;
			};

			uniform sampler3D _Texture0;
			uniform float m_DensityMul;
			uniform float m_DensityMul1;
			uniform float m_DensityMul2;
			UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
			uniform float4 _CameraDepthTexture_TexelSize;
			float4 MyCustomExpression( float3 boundsMin , float3 boundsMax , float3 rayOrigin , float3 rayDir , sampler3D volumeTex , float DensityMul , float LightAbsorptionThroughCloud , float LightAbsorptionTowardSun , float depthFade )
			{
				_DensityMul = DensityMul;
				_LightAbsorptionThroughCloud=LightAbsorptionThroughCloud;
				_LightAbsorptionTowardSun = LightAbsorptionTowardSun;
				return  rayMarching( boundsMin,  boundsMax,  rayOrigin,  rayDir, volumeTex,depthFade);
			}
			

			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				float4 ase_clipPos = UnityObjectToClipPos(v.vertex);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord1 = screenPos;
				
				float3 vertexValue = float3(0, 0, 0);
				#if ASE_ABSOLUTE_VERTEX_POS
				vertexValue = v.vertex.xyz;
				#endif
				vertexValue = vertexValue;
				#if ASE_ABSOLUTE_VERTEX_POS
				v.vertex.xyz = vertexValue;
				#else
				v.vertex.xyz += vertexValue;
				#endif
				o.vertex = UnityObjectToClipPos(v.vertex);

#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
#endif
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				fixed4 finalColor;
#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 WorldPosition = i.worldPos;
#endif
				float3 ase_objectScale = float3( length( unity_ObjectToWorld[ 0 ].xyz ), length( unity_ObjectToWorld[ 1 ].xyz ), length( unity_ObjectToWorld[ 2 ].xyz ) );
				float3 objToWorld19 = mul( unity_ObjectToWorld, float4( float3( 0,0,0 ), 1 ) ).xyz;
				float3 boundsMin7 = ( ( ase_objectScale * float3( -0.5,-0.5,-0.5 ) ) + objToWorld19 );
				float3 boundsMax7 = ( objToWorld19 + ( ase_objectScale * float3( 0.5,0.5,0.5 ) ) );
				float3 rayOrigin7 = _WorldSpaceCameraPos;
				float3 normalizeResult12 = normalize( ( WorldPosition - _WorldSpaceCameraPos ) );
				float3 rayDir7 = normalizeResult12;
				sampler3D volumeTex7 = _Texture0;
				float DensityMul7 = m_DensityMul;
				float LightAbsorptionThroughCloud7 = m_DensityMul1;
				float LightAbsorptionTowardSun7 = m_DensityMul2;
				float4 screenPos = i.ase_texcoord1;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float screenDepth25 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
				float distanceDepth25 = abs( ( screenDepth25 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( 1.0 ) );
				float depthFade7 = distanceDepth25;
				float4 localMyCustomExpression7 = MyCustomExpression( boundsMin7 , boundsMax7 , rayOrigin7 , rayDir7 , volumeTex7 , DensityMul7 , LightAbsorptionThroughCloud7 , LightAbsorptionTowardSun7 , depthFade7 );
				#if defined(LIGHTMAP_ON) && ( UNITY_VERSION < 560 || ( defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN) ) )//aselc
				float4 ase_lightColor = 0;
				#else //aselc
				float4 ase_lightColor = _LightColor0;
				#endif //aselc
				
				
				finalColor = ( localMyCustomExpression7 * ase_lightColor );
				return finalColor;
			}
			ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18100
0;7;1920;971;1256.188;58.36031;1;True;True
Node;AmplifyShaderEditor.CommentaryNode;22;-1536.232,28.06196;Inherit;False;794.4016;584.6578;射线方向和起点;5;11;9;10;12;8;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;21;-1023.836,-512.0016;Inherit;False;754.0009;493.0001;平移缩放;6;15;17;19;16;18;20;;1,1,1,1;0;0
Node;AmplifyShaderEditor.ObjectScaleNode;15;-969.8361,-462.0016;Inherit;False;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldSpaceCameraPos;11;-1486.232,429.7198;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode;9;-1480.887,222.4902;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;16;-611.8362,-404.0016;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;-0.5,-0.5,-0.5;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;10;-1126.13,285.4212;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-687.8362,-154.0015;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0.5,0.5,0.5;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TransformPositionNode;19;-973.8361,-276.0016;Inherit;False;Object;World;False;Fast;True;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;33;-568.188,615.6397;Inherit;False;Property;m_DensityMul1;AbsorptionThroughCloud;2;0;Create;False;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;32;-578.188,526.6397;Inherit;False;Property;m_DensityMul;DensityMul;1;0;Create;False;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;14;-627.2801,309.5707;Inherit;True;Property;_Texture0;Texture 0;0;0;Create;True;0;0;False;0;False;None;0bb826cf8eb6e3c41af5f12749e4e519;False;white;LockedToTexture3D;Texture3D;-1;0;1;SAMPLER3D;0
Node;AmplifyShaderEditor.NormalizeNode;12;-916.8312,255.52;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WorldSpaceCameraPos;8;-1179.393,78.06197;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;34;-549.188,689.6397;Inherit;False;Property;m_DensityMul2;AbsorptionTowardSun;3;0;Create;False;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;18;-421.8352,-377.0016;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;20;-428.8352,-199.0015;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DepthFade;25;-559.8873,783.7843;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LightColorNode;24;164.942,353.2339;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.CustomExpressionNode;7;-44.84483,60.18339;Inherit;False;_DensityMul = DensityMul@$_LightAbsorptionThroughCloud=LightAbsorptionThroughCloud@$_LightAbsorptionTowardSun = LightAbsorptionTowardSun@$return  rayMarching( boundsMin,  boundsMax,  rayOrigin,  rayDir, volumeTex,depthFade)@;4;False;9;False;boundsMin;FLOAT3;-0.5,-0.5,-0.5;In;;Inherit;False;False;boundsMax;FLOAT3;0.5,0.5,0.5;In;;Inherit;False;False;rayOrigin;FLOAT3;0,0,0;In;;Inherit;False;False;rayDir;FLOAT3;0,0,0;In;;Inherit;False;False;volumeTex;SAMPLER3D;;In;;Inherit;False;True;DensityMul;FLOAT;10;In;;Inherit;False;True;LightAbsorptionThroughCloud;FLOAT;2;In;;Inherit;False;True;LightAbsorptionTowardSun;FLOAT;1;In;;Inherit;False;True;depthFade;FLOAT;0;In;;Inherit;False;My Custom Expression;False;False;0;9;0;FLOAT3;-0.5,-0.5,-0.5;False;1;FLOAT3;0.5,0.5,0.5;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;SAMPLER3D;;False;5;FLOAT;10;False;6;FLOAT;2;False;7;FLOAT;1;False;8;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;31;459.0389,188.3094;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;791.3637,177.5856;Float;False;True;-1;2;ASEMaterialInspector;100;1;VolumeRender;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;True;2;5;False;-1;10;False;-1;0;1;False;-1;0;False;-1;True;0;False;-1;0;False;-1;True;False;True;0;False;-1;True;True;True;True;True;0;False;-1;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;2;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;2;RenderType=Opaque=RenderType;Queue=Transparent=Queue=0;True;2;0;False;False;False;False;False;False;False;False;False;True;1;LightMode=ForwardBase;False;2;Include;;False;;Native;Include;VolumeRender.cginc;False;;Custom;;0;0;Standard;1;Vertex Position,InvertActionOnDeselection;1;0;1;True;False;;0
WireConnection;16;0;15;0
WireConnection;10;0;9;0
WireConnection;10;1;11;0
WireConnection;17;0;15;0
WireConnection;12;0;10;0
WireConnection;18;0;16;0
WireConnection;18;1;19;0
WireConnection;20;0;19;0
WireConnection;20;1;17;0
WireConnection;7;0;18;0
WireConnection;7;1;20;0
WireConnection;7;2;8;0
WireConnection;7;3;12;0
WireConnection;7;4;14;0
WireConnection;7;5;32;0
WireConnection;7;6;33;0
WireConnection;7;7;34;0
WireConnection;7;8;25;0
WireConnection;31;0;7;0
WireConnection;31;1;24;0
WireConnection;0;0;31;0
ASEEND*/
//CHKSM=BAC595EEFE0A7FB4D0D3E58167CDCC74516FB42B