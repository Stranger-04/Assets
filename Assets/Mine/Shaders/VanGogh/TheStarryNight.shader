Shader "Mine/VanGogh/TheStarryNight"
{
    Properties
    {
        _BaseColorSkyA ("Base Color SkyA", Color) = (1, 1, 1, 1)
        _BaseColorSkyB ("Base Color SkyB", Color) = (0, 0, 0, 1)
        _BaseColorSkyC ("Base Color SkyC", Color) = (0, 0, 0, 1)
        _RingCount ("Ring Count", Range(1, 200)) = 10
        _RingSoftness ("Ring Softness", Range(0.001, 0.5)) = 0.1
        _SectorCount ("Sector Count", Range(0, 2)) = 1
        _SectorSoftness ("Sector Softness", Range(0.001, 0.5)) = 0.1

        _Random ("Random", Range(0, 1)) = 0.2
        _Speed ("Speed", Range(0, 1)) = 0.5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Assets/Mine/Special/HLSL/SDF.hlsl"
    // ── Parameters ──
    #define SDF_POINT_COUNT 1

    static const float2 _Points[SDF_POINT_COUNT] = {
        float2(0, 0)
    };

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColorSkyA;
        float4 _BaseColorSkyB;
        float4 _BaseColorSkyC;
        float _Smoothness;
        float _RingCount;
        float _RingSoftness;
        float _SectorCount;
        float _SectorSoftness;

        float _Random;
        float _Speed;
        float2 _Size;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 texcoord : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord : TEXCOORD0;
    };
    // ════════════════════════════════════════════════════════════
    //  ComputeSDF — 对点列表计算圆形 SDF 并取并集
    // ════════════════════════════════════════════════════════════
    float ComputeRadialSDF(float2 uv)
    {
        float distance = 1e10;
        for (int i = 0; i < SDF_POINT_COUNT; i++)
        {
            float dist = length(uv - _Points[i]);
            distance = min(distance, dist);
        }
        return distance;
    }

    // ════════════════════════════════════════════════════════════
    //  ComputeAngularSDF — 归一化角度灰度（仅灰度）
    // ════════════════════════════════════════════════════════════
    float ComputeAngularSDF(float2 uv)
    {
        float gray = 0.0;
        for (int i = 0; i < SDF_POINT_COUNT; i++)
        {
            float2 delta = uv - _Points[i];
            float angle = (atan2(delta.y, delta.x) + PI) / (2.0 * PI);
            gray = max(gray, angle);
        }
        return gray;
    }

    // ════════════════════════════════════════════════════════════
    //  ComputeCell — 灰度切分为 cell，count 按 scale 缩放
    //  t = gray * count * scale
    //  径向：scale=1 → 等距切分；周向：scale=dist → 等弧长切分
    // ════════════════════════════════════════════════════════════
    float2 ComputeCell(float gray, float count, float scale, float softness)
    {
        scale = max(scale, 1);
        float n = max(1.0, round(count * scale));   // 就近取整，确保 360° 均匀分割
        float t = gray * n;
        float cellColor = frac(t);
        float cellIndex = floor(t);
        cellColor = smoothstep(0.0, softness, cellColor) * smoothstep(0.0, softness, 1.0 - cellColor);
        return float2(cellColor, cellIndex);
    }

    float Hash(float x)
    {
        return frac(sin(x * 12.9898) * 43758.5453) - 0.5;
    }

    float Hash2(float x, float y)
    {
        return frac(sin(x * 127.1 + y * 311.7) * 43758.5453) - 0.5;
    }

    // ════════════════════════════════════════════════════════════
    //  GaussPeak — 高斯波峰：exp(-((t - pos) / width)^2) * height
    // ════════════════════════════════════════════════════════════
    float GaussPeak(float t, float3 peak)
    {
        float d = (t - peak.x) / peak.y;
        return exp(-(d * d)) * peak.z;
    }

    float3 ComputeSkyColor(float ringIndex, float sectorIndex)
    {
        float t = ringIndex / _RingCount;
        float r = Hash2(ringIndex, sectorIndex) * _Random;
        t = saturate(t + r);

        // 双波峰：tz=高度 → 对应颜色值 (1=A, 0.5=B, 0=C)
        float3 peak1 = float3(0.0, 0.25, 1.0);  // t=0 → 高度=1 → color A
        float3 peak2 = float3(0.4, 0.025, 0.5);  // t=0.4 → 高度=0.5 → color B
        // 无波峰处 → 高度≈0 → color C

        float height = max(GaussPeak(t, peak1), GaussPeak(t, peak2));

        // 高度 [0,1] 映射为 A→B→C
        float tBC = saturate(height * 2.0);           // [0,1] over height [0, 0.5]
        float3 colCtoB = lerp(_BaseColorSkyC, _BaseColorSkyB, tBC).rgb;
        float tBA = saturate((height - 0.5) * 2.0);   // [0,1] over height [0.5, 1]
        return lerp(colCtoB, _BaseColorSkyA, tBA).rgb;
    }

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.texcoord = input.texcoord;
        return output;
    }
    // ════════════════════════════════════════════════════════════
    //  Frag_Main — 主片元着色器
    //  采样 BlitTexture，通过 ComputeSDF 生成风格化效果
    // ════════════════════════════════════════════════════════════
    half4 Frag_Main(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord - 0.5;
        float dist  = ComputeRadialSDF(uv);
        float angle = ComputeAngularSDF(uv);

        float2 rings = ComputeCell(dist,  _RingCount,   1.0,  _RingSoftness);
        float ringColor = rings.x;
        float ringIndex = rings.y;
        float random = Hash(ringIndex) * _Random;
        float movement = _Time.y * _Speed * (1 + random) * 0.1;
        angle += random + movement;
        angle = frac(angle);
        float2 sectors = ComputeCell(angle, _SectorCount, ringIndex, _SectorSoftness);
        float sectorColor = sectors.x;
        float sectorIndex = sectors.y;

        float cells = ringColor * sectorColor * 0.5 + 0.5;
        float3 skyColor = ComputeSkyColor(ringIndex, sectorIndex) * cells;
        return half4(skyColor, 1);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        // ════════════════════════════════════════════════════════
        //  Pass 0: 主效果 — 风格化渲染
        // ════════════════════════════════════════════════════════
        Pass
        {
            Name "TheStarryNight_Main"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_Main
            ENDHLSL
        }
    }

    Fallback Off
}
