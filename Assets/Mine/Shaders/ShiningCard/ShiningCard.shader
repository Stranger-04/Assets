Shader "Mine/ShiningCard"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor]  _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Grid)]
        [Space]
        _GridCount        ("Grid Count", Float) = 10
        _GapWidth         ("Gap Width", Range(0.0, 0.3)) = 0.03

        [Header(Wave)]
        [Space]
        _ViewDirStrength  ("View Dir Strength", Range(0.0, 2.0)) = 0.5

        [Header(Flash Color)]
        [Space]
        _FlashThreshold   ("Flash Threshold", Range(0.0, 1.0)) = 0.3
        _FlashSoftness    ("Flash Softness", Range(0.0, 0.5)) = 0.1
        _FlashHueShift    ("Flash Hue Shift", Range(-1.0, 1.0)) = 0.3
        _FlashSaturation  ("Flash Saturation", Range(0.0, 2.0)) = 1.2
        _FlashValue       ("Flash Value", Range(0.0, 2.0)) = 1.2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Assets/Mine/Special/HLSL/HSV.hlsl"

    // ── 纹理与采样器 ──
    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    // ── 参数 ──
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float  _GridCount;
        float  _GapWidth;
        float  _ViewDirStrength;
        float  _FlashThreshold;
        float  _FlashSoftness;
        float  _FlashHueShift;
        float  _FlashSaturation;
        float  _FlashValue;
    CBUFFER_END

    // ── 顶点输入 ──
    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS   : NORMAL;
        float4 tangentOS  : TANGENT;
        float2 uv         : TEXCOORD0;
    };

    // ── 顶点→片元 ──
    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
        float3 positionWS : TEXCOORD1;
        float3 normalWS   : TEXCOORD2;
        float3 tangentWS  : TEXCOORD3;
        float  tangentSign : TEXCOORD4;
    };

    // ════════════════════════════════════════════════════════════
    //  Vert — 标准位置/法线变换，传递 TBN 数据供 Frag 计算视线
    // ════════════════════════════════════════════════════════════
    Varyings Vert(Attributes input)
    {
        Varyings output;

        VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
        VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

        output.positionCS  = positionInputs.positionCS;
        output.positionWS  = positionInputs.positionWS;
        output.normalWS    = normalInputs.normalWS;
        output.tangentWS   = normalInputs.tangentWS;
        output.tangentSign = input.tangentOS.w * GetOddNegativeScale();
        output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);

        return output;
    }

    // ════════════════════════════════════════════════════════════
    //  GetViewDirTS — 世界空间视线 → 切线空间视线
    // ════════════════════════════════════════════════════════════
    float3 GetViewDirTS(float3 positionWS, float3 normalWS, float3 tangentWS, float tangentSign)
    {
        float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;
        float3 viewDirWS   = normalize(GetWorldSpaceViewDir(positionWS));
        return float3(
            dot(viewDirWS, tangentWS),
            dot(viewDirWS, bitangentWS),
            dot(viewDirWS, normalWS)
        );
    }

    // ════════════════════════════════════════════════════════════
    //  TriangleGridMask — 静态三角格子遮罩（不随视角移动）
    // ════════════════════════════════════════════════════════════
    float TriangleGridMask(float2 uv, float gridCount, float gapWidth)
    {
        float2 p = uv * gridCount;
        const float sqrt3_2 = 0.8660254;

        float p0 =  p.x * sqrt3_2 + p.y * 0.5;
        float p1 =  p.y;
        float p2 = -p.x * sqrt3_2 + p.y * 0.5;

        float f0 = frac(p0);
        float f1 = frac(p1);
        float f2 = frac(p2);

        float d0 = min(f0, 1.0 - f0);
        float d1 = min(f1, 1.0 - f1);
        float d2 = min(f2, 1.0 - f2);

        float minEdge = min(min(d0, d1), d2);
        return smoothstep(0.0, gapWidth, minEdge);
    }

    // ════════════════════════════════════════════════════════════
    //  DiagonalFlash — 对角渐变闪光，shift 控制视角偏移
    // ════════════════════════════════════════════════════════════
    float DiagonalFlash(float2 uv, float2 shift)
    {
        float2 p = uv + shift;
        float d = dot(p, float2(1.2, 1.0));
        float flash = frac(d);
        return smoothstep(0.0, 0.2, flash) * smoothstep(1.0, 0.8, flash);
    }

    // ════════════════════════════════════════════════════════════
    //  ApplyFlashColor — HSV 颜色偏移，受 flashAmount 调制
    // ════════════════════════════════════════════════════════════
    half4 ApplyFlashColor(half4 baseColor, float flashAmount,
        float hueShift, float saturation, float value)
    {
        float3 hsv = RGBtoHSV(baseColor.rgb);
        hsv.x = frac(hsv.x + hueShift * flashAmount);
        hsv.y = lerp(hsv.y, saturation, flashAmount);
        hsv.z = lerp(hsv.z, value, flashAmount);
        return half4(HSVtoRGB(hsv), baseColor.a);
    }

    // ════════════════════════════════════════════════════════════
    //  Frag — 闪卡管线
    //
    //  viewDir → 对角闪光偏移  ×  静态三角遮罩  → 阈值 → HSV
    // ════════════════════════════════════════════════════════════
    half4 Frag(Varyings input) : SV_Target
    {
        // ── Layer 1-2: 视线 → 网格偏移 ──
        float3 normalWS  = normalize(input.normalWS);
        float3 tangentWS = normalize(input.tangentWS);
        float3 viewDirTS = GetViewDirTS(input.positionWS, normalWS, tangentWS, input.tangentSign);
        float2 gridShift = viewDirTS.xy * _ViewDirStrength;

        // ── Layer 3: 对角闪光 × 静态三角遮罩 ──
        float triMask    = TriangleGridMask(input.uv, _GridCount, _GapWidth);
        float flashVal   = DiagonalFlash(input.uv, gridShift);
        float flashMask  = flashVal * triMask;
        float flashAmount = smoothstep(_FlashThreshold, _FlashThreshold + _FlashSoftness, flashMask);

        // ── Layer 5: HSV 颜色调制 ──
        half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
        return ApplyFlashColor(baseColor, flashAmount, _FlashHueShift, _FlashSaturation, _FlashValue);
    }

    // ════════════════════════════════════════════════════════════
    //  ShadowCaster / DepthOnly / DepthNormals — 标准 URP 深度 Pass
    // ════════════════════════════════════════════════════════════

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

    float3 _LightDirection;
    float3 _LightPosition;

    float4 GetShadowPositionHClip(Attributes input)
    {
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float3 normalWS   = TransformObjectToWorldNormal(float3(0, 0, 1));
        float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
        return positionCS;
    }

    Varyings Vert_Shadow(Attributes input)
    {
        Varyings output;
        output.positionCS = GetShadowPositionHClip(input);
        output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        return output;
    }

    Varyings Vert_Depth(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        return output;
    }

    half4 Frag_Shadow(Varyings input) : SV_Target
    {
        return 0;
    }

    half4 Frag_Depth(Varyings input) : SV_Target
    {
        return 0;
    }

    half4 Frag_DepthNormals(Varyings input) : SV_Target
    {
        return half4(0, 0, 0, 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ────── Forward Pass ──────
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // ────── ShadowCaster ──────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert_Shadow
            #pragma fragment Frag_Shadow
            ENDHLSL
        }

        // ────── DepthOnly ──────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert_Depth
            #pragma fragment Frag_Depth
            ENDHLSL
        }

        // ────── DepthNormals ──────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert_Depth
            #pragma fragment Frag_DepthNormals
            ENDHLSL
        }
    }
}
