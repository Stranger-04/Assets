Shader "Mine/Picker/OutlineMask"
{
    // ════════════════════════════════════════════════════════════
    //  OutlineMask — 选中物体 Mask 输出
    //
    //  SV_Target → R8 mask（物体=1，背景=0）
    // ════════════════════════════════════════════════════════════

    Properties
    {
        _ObjectID ("Object ID", Int) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        uint _ObjectID;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        return output;
    }

    float Frag(Varyings input) : SV_Target
    {
        // 物体区域 = 1.0（R8 格式存储为 255）
        return 1.0;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "OUTLINE_MASK"
            Tags { "LightMode" = "OutlineMask" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
