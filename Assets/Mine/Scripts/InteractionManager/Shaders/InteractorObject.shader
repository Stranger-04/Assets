Shader "Mine/Interaction/InteractorObject"
{
    // ═══════════════════════════════════════════════════════════════
    //  互动物体 Shader — 两个 Pass：
    //
    //  Pass 0 (InteractionPass, UniversalForward): DrawObjectsPass 使用，
    //     输出单通道交互强度，写入 _CameraColorTexture (R8/RFloat)。
    //
    //  Pass 1 (DepthDifferencePass): 保留的深度比较逻辑，采样场景深度计算
    //     按压深度。供后续多 Pass Shader 集成参考。
    // ═══════════════════════════════════════════════════════════════

    Properties
    {
        _Intensity ("Interaction Intensity", Range(0, 1)) = 1.0
        _Power     ("Depth Power", Range(0, 1)) = 0.5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    float _Intensity;
    float _Power;

    struct Attributes
    {
        float3 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionHCS : SV_POSITION;
        float2 uv          : TEXCOORD0;
        float  viewDepth   : TEXCOORD1;
    };
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalRenderPipeline"
        }
        LOD 100

        // ════════════════════════════════════════════════════════════
        //  Pass 0 — 交互渲染 Pass (LightMode=UniversalForward)
        //  DrawObjectsPass 渲染此 Pass 到 _CameraColorTexture。
        // ════════════════════════════════════════════════════════════

        Pass
        {
            Name "InteractionPass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex InteractionVert
            #pragma fragment InteractionFrag

            struct InteractionVaryings
            {
                float4 positionHCS : SV_POSITION;
            };

            InteractionVaryings InteractionVert(Attributes i)
            {
                InteractionVaryings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS);
                return o;
            }

            half InteractionFrag(InteractionVaryings i) : SV_Target
            {
                return _Intensity;
            }
            ENDHLSL
        }

        // ════════════════════════════════════════════════════════════
        //  Pass 1 — 深度比较 Pass（保留）
        //  比较物体深度与场景深度，输出按压深度值。
        // ════════════════════════════════════════════════════════════

        Pass
        {
            Name "DepthDifferencePass"
            Tags { "LightMode" = "DepthDifference" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend One Zero

            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
                WriteMask 2
            }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthVaryings
            {
                float4 positionHCS : SV_POSITION;
                float  viewDepth   : TEXCOORD0;
                float4 positionHS  : TEXCOORD1;
            };

            DepthVaryings DepthVert(Attributes i)
            {
                DepthVaryings o;
                o.positionHCS = TransformObjectToHClip(i.positionOS);
                o.positionHS  = ComputeScreenPos(o.positionHCS);
                o.viewDepth   = o.positionHCS.z / o.positionHCS.w;
                return o;
            }

            half DepthFrag(DepthVaryings i) : SV_Target
            {
                float2 screenUV = i.positionHS.xy / i.positionHS.w;
                float sceneRawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float viewEyeDepth  = LinearEyeDepth(i.viewDepth, _ZBufferParams);
                float depthDiff = pow(saturate(viewEyeDepth - sceneEyeDepth), _Power);
                return depthDiff;
            }
            ENDHLSL
        }
    }
}
