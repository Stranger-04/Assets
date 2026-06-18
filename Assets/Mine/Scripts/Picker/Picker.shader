Shader "Mine/Picker/Picker"
{
    // ════════════════════════════════════════════════════════════
    //  Picker — GPU 屏幕空间选物 MRT Shader
    //
    //  用途：作为 Replacement Shader 批量绘制可选物体，
    //        单 Pass 同时输出 ObjectID / Depth / Normal 到三张 RT
    //
    //  MRT 输出：
    //    SV_Target0 → ObjectID (R8_UInt, 0 = 背景)
    //    SV_Target1 → Depth    (R32_Float, linear01)
    //    SV_Target2 → Normal   (RGB8_UNorm, world space 重映射到 [0,1])
    // ════════════════════════════════════════════════════════════

    Properties
    {
        [IntRange] _ObjectID ("Object ID", Range(0, 255)) = 0
        [Toggle] _DebugScale ("Debug Scale", Float) = 1
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    // ── 参数 ──
    CBUFFER_START(UnityPerMaterial)
        uint  _ObjectID;
        float _DebugScale;
    CBUFFER_END

    // ── 顶点输入 ──
    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS   : NORMAL;
    };

    // ── 顶点→片元 ──
    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 normalWS    : TEXCOORD0;
        float  linearDepth : TEXCOORD1;
    };

    // ════════════════════════════════════════════════════════════
    //  Vert — 标准模型变换，传递世界法线 + 线性深度
    // ════════════════════════════════════════════════════════════
    Varyings Vert(Attributes input)
    {
        Varyings output;

        VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
        VertexNormalInputs   normalInputs   = GetVertexNormalInputs(input.normalOS);

        output.positionCS  = positionInputs.positionCS;
        output.normalWS    = normalInputs.normalWS;

        // linear01 depth: 世界空间距离 / farPlane
        float3 viewPos = positionInputs.positionWS - GetCameraPositionWS();
        output.linearDepth = length(viewPos) / _ProjectionParams.z;

        return output;
    }

    // ── MRT 输出结构 ──
    struct FragOutput
    {
        float4 objectID : SV_Target0;   // RGB24 ID 编码
        float  depth    : SV_Target1;
        float3 normal   : SV_Target2;
    };

    // ════════════════════════════════════════════════════════════
    //  Frag — MRT 输出 ObjectID / Depth / Normal
    //
    //  ObjectID: RGB24 编码，24-bit 范围 0–16M
    //    r = (id >> 16) & 255, g = (id >> 8) & 255, b = id & 255
    //  Readback: id = r << 16 | g << 8 | b
    // ════════════════════════════════════════════════════════════
    FragOutput Frag(Varyings input)
    {
        FragOutput output;

        uint id = _ObjectID;
        output.objectID = float4(
            (float)((id >> 16) & 255) / 255.0,
            (float)((id >> 8)  & 255) / 255.0,
            (float)( id        & 255) / 255.0,
            1.0);

        output.depth = input.linearDepth;

        float3 wn = normalize(input.normalWS);
        output.normal = wn * 0.5 + 0.5;

        return output;
    }

    // ── Forward 渲染：简单的 Lambert diffuse ──────────────────

    struct ForwardVaryings
    {
        float4 positionCS : SV_POSITION;
        float3 normalWS   : TEXCOORD0;
    };

    ForwardVaryings ForwardVert(Attributes input)
    {
        ForwardVaryings output;
        VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
        VertexNormalInputs   nmlInputs = GetVertexNormalInputs(input.normalOS);
        output.positionCS = posInputs.positionCS;
        output.normalWS   = nmlInputs.normalWS;
        return output;
    }

        // 简易 HSV→RGB（HLSL）
    float3 HueToRGB(float h)
    {
        float r = abs(h * 6.0 - 3.0) - 1.0;
        float g = 2.0 - abs(h * 6.0 - 2.0);
        float b = 2.0 - abs(h * 6.0 - 4.0);
        return saturate(float3(r, g, b));
    }
    
    half4 ForwardFrag(ForwardVaryings input) : SV_Target
    {
        float3 N = normalize(input.normalWS);
        float3 L = normalize(_MainLightPosition.xyz);
        float  diff = saturate(dot(N, L)) * 0.5 + 0.5; // half-lambert

        // 根据 ObjectID 生成不同色相，方便区分
        float hue = frac((float)_ObjectID * 0.61803398875); // golden ratio
        float3 color = saturate(HueToRGB(hue) * diff);

        return half4(color, 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ── Pass 0: MRT 选物输出 ───────────────────────────
        Pass
        {
            Name "PICKER_MRT"
            Tags { "LightMode" = "PickerMRT" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // ── Pass 1: 可见渲染（不同 ID 不同颜色） ────────────
        Pass
        {
            Name "PICKER_FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex ForwardVert
            #pragma fragment ForwardFrag
            ENDHLSL
        }
    }

    Fallback Off
}
