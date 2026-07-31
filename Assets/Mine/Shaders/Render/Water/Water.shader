Shader "Custom/Water"
{
    Properties
    {
        _baseColorA("Base Color A", Color) = (1, 1, 1, 1)
        _baseColorB("Base Color B", Color) = (1, 1, 1, 1)

        [Header(FFT Wave)]
        _DisplacementScale("Displacement Scale", Float) = 1.0
        _NormalIntensity("Normal Intensity", Range(0, 2)) = 1.0
        _TessellationFactor("Tessellation Factor", Range(1, 32)) = 8

        [Header(Foam)]
        _FoamTex("Foam Texture", 2D) = "white" {}
        [Range(0, 1)]_FoamScale("Foam Scale", Float) = 1.0
        [Range(0, 1)]_FoamSpeed("Foam Speed", Float) = 1.0
        [Range(0, 1)]_FoamIntensity("Foam Intensity", Float) = 0.5

        [Header(Debug)]
        [KeywordEnum(Off, Displacement, Normal, Height, Cascade0, Cascade1, Cascade2)] _DebugMode("Debug Mode", Float) = 0

        [Range(0, 1)]_Distortion("Distortion", Range(0, 1)) = 0.1
        [Range(0, 1)]_Caustics("Caustics", Range(0, 1)) = 1.0
        [Range(0, 1)]_Alpha("Alpha", Range(0, 1)) = 1.0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Assets/Mine/Special/HLSL/DepthDiffFunction.hlsl"
    #include "Assets/Mine/Special/HLSL/ShadowFunction.hlsl"
    #include "Assets/Mine/Special/HLSL/LightFunction.hlsl"
    #include "Assets/Mine/Special/HLSL/BlendFunction.hlsl"
    #include "Assets/Mine/Special/HLSL/PBRFunction.hlsl"
    #include "Assets/Mine/Special/HLSL/ENVFunction.hlsl"

    // ── FFT Wave 全局纹理（由 FFTWaveOrchestrator 注入）──
    TEXTURE2D(_WaveDisplacement0); SAMPLER(sampler_WaveDisplacement0);
    TEXTURE2D(_WaveDisplacement1); SAMPLER(sampler_WaveDisplacement1);
    TEXTURE2D(_WaveDisplacement2); SAMPLER(sampler_WaveDisplacement2);
    TEXTURE2D(_WaveNormal0);       SAMPLER(sampler_WaveNormal0);
    TEXTURE2D(_WaveNormal1);       SAMPLER(sampler_WaveNormal1);
    TEXTURE2D(_WaveNormal2);       SAMPLER(sampler_WaveNormal2);
    float _WavePatchSize0, _WavePatchSize1, _WavePatchSize2;



    TEXTURE2D_X(_FoamTex);
    SAMPLER(sampler_FoamTex);

    CBUFFER_START(UnityPerMaterial)
        float4 _baseColorA;
        float4 _baseColorB;

        float _DisplacementScale;
        float _NormalIntensity;
        float _TessellationFactor;

        float _FoamScale;
        float _FoamSpeed;
        float _FoamIntensity;

        float _Distortion;
        float _Caustics;
        float _Alpha;
    CBUFFER_END

    // ── 结构体 ──────────────────────────────────────────────

    struct WaterAttributes
    {
        float3 positionOS   : POSITION;
        float3 normalOS     : NORMAL;
        float4 tangentOS    : TANGENT;
        float2 uv           : TEXCOORD0;
    };

    // 控制点输出: Vert → Hull
    struct HullControlPoint
    {
        float3 positionOS : INTERNALTESSPOS;
        float3 normalOS   : NORMAL;
        float4 tangentOS  : TANGENT;
        float2 uv         : TEXCOORD0;
    };

    // Domain → Frag
    struct WaterVaryings
    {
        float4 positionCS   : SV_POSITION;
        float3 positionWS   : TEXCOORD0;
        float4 positionSS   : TEXCOORD1;
        float3 normalWS     : TEXCOORD2;
        float3 tangentWS    : TEXCOORD3;
        float3 bitanentWS   : TEXCOORD4;
        float2 uv           : TEXCOORD5;
    };

    // ════════════════════════════════════════════════════════════
    //  ComputeFFTDisplacement — 3 级 cascade 位移混合
    // ════════════════════════════════════════════════════════════
    float3 ComputeFFTDisplacement(float3 positionWS)
    {
        float3 disp = 0;

        float2 uv0 = positionWS.xz / _WavePatchSize0;
        disp += SAMPLE_TEXTURE2D_LOD(_WaveDisplacement0, sampler_WaveDisplacement0, uv0, 0).rgb;

        float2 uv1 = positionWS.xz / _WavePatchSize1;
        disp += SAMPLE_TEXTURE2D_LOD(_WaveDisplacement1, sampler_WaveDisplacement1, uv1, 0).rgb;

        float2 uv2 = positionWS.xz / _WavePatchSize2;
        disp += SAMPLE_TEXTURE2D_LOD(_WaveDisplacement2, sampler_WaveDisplacement2, uv2, 0).rgb;

        return disp * _DisplacementScale;
    }

    // ════════════════════════════════════════════════════════════
    //  ComputeFFTNormal — 3 级 cascade 法线混合
    // ════════════════════════════════════════════════════════════
    float3 ComputeFFTNormal(float3 positionWS, float3 defaultNormalWS)
    {
        float3 n = 0;
        float  w = 0;

        float2 uv0 = positionWS.xz / _WavePatchSize0;
        float3 n0 = SAMPLE_TEXTURE2D(_WaveNormal0, sampler_WaveNormal0, uv0).rgb * 2.0 - 1.0;
        float  w0 = 0.6;
        n += n0 * w0; w += w0;

        float2 uv1 = positionWS.xz / _WavePatchSize1;
        float3 n1 = SAMPLE_TEXTURE2D(_WaveNormal1, sampler_WaveNormal1, uv1).rgb * 2.0 - 1.0;
        float  w1 = 0.3;
        n += n1 * w1; w += w1;

        float2 uv2 = positionWS.xz / _WavePatchSize2;
        float3 n2 = SAMPLE_TEXTURE2D(_WaveNormal2, sampler_WaveNormal2, uv2).rgb * 2.0 - 1.0;
        float  w2 = 0.1;
        n += n2 * w2; w += w2;

        n /= w;

        float3 blended = lerp(defaultNormalWS, n, _NormalIntensity);
        return normalize(blended);
    }

    // ════════════════════════════════════════════════════════════
    //  ComputeOpaque — PBR 光照
    // ════════════════════════════════════════════════════════════
    float3 ComputeOpaque(float3 baseColor, float3 positionWS, float3 normalWS, float3 viewDirWS, float3 mainLitDir, float3 mainLitColor, float mainLitDistanceAtten, float mainLitShadowAtten)
    {
        float  roughness = 0.2;
        float  metallic = 0.0;
        float3 lightDirWS = normalize(mainLitDir);

        float3 halfVec = normalize(lightDirWS + viewDirWS);
        float  NdotL = max(0.0, dot(normalWS, lightDirWS));
        float  NdotV = max(0.0, dot(normalWS, viewDirWS));
        float  NdotH = max(0.0, dot(normalWS, halfVec));
        float  VdotH = max(0.0, dot(viewDirWS, halfVec));
        float  LdotH = max(0.0, dot(lightDirWS, halfVec));
        float  ndotl = dot(normalWS, lightDirWS) * 0.25 + 0.75;
        float  ndoth = dot(normalWS, halfVec) * 0.5 + 0.5;
        float  ndotv = dot(normalWS, viewDirWS) * 0.5 + 0.5;
        ndoth = smoothstep(0.8, 1.0, ndoth);

        float  shadowArea = mainLitShadowAtten * 0.5 + 0.5;
        float3 radiance = mainLitColor * mainLitDistanceAtten * shadowArea;
        float3 F0 = lerp(0.04, baseColor, metallic);
        float3 F  = F_Fast(F0, VdotH);

        float3 diffuse  = Diff_Lambert(baseColor) * PI * radiance * (1.0 - metallic) * (1.0 - F);
        float3 specular = Spec_Unity(ndoth, LdotH, VdotH, 0.0, 0.0, roughness, 0.0) * PI * radiance * ndotl * F;
        float3 ambient  = BRDF_Env(baseColor, ndotv, normalWS, viewDirWS, roughness, metallic, unity_SpecCube0, samplerunity_SpecCube0) * mainLitColor;
        return diffuse + specular + ambient;
    }

    // ════════════════════════════════════════════════════════════
    //  ComputeEdgeFoam — 深度边缘泡沫
    // ════════════════════════════════════════════════════════════
    float ComputeEdgeFoam(float edge, float3 positionWS, float normalDiff)
    {
        edge = saturate(exp(- edge * _FoamIntensity * 10));
        float offset = normalDiff * 0.8;
        float2 coord = float2(positionWS.y, edge + offset) * _FoamScale * 10 + _Time.y * _FoamSpeed * 0.1;
        float  foam  = _FoamTex.Sample(sampler_FoamTex, coord).r;
        float  edgeFoam = lerp(edge, foam, 0.8) * edge;
        edgeFoam = smoothstep(0.15, 0.16, edgeFoam);
        return edgeFoam;
    }

    // ════════════════════════════════════════════════════════════
    //  ComputeWaveFoam — Jacobian 波峰泡沫
    // ════════════════════════════════════════════════════════════
    float ComputeWaveFoam(float3 positionWS)
    {
        float foam = 0;

        float2 uv0 = positionWS.xz / _WavePatchSize0;
        foam += SAMPLE_TEXTURE2D(_WaveDisplacement0, sampler_WaveDisplacement0, uv0).a * 0.5;

        float2 uv1 = positionWS.xz / _WavePatchSize1;
        foam += SAMPLE_TEXTURE2D(_WaveDisplacement1, sampler_WaveDisplacement1, uv1).a * 0.3;

        float2 uv2 = positionWS.xz / _WavePatchSize2;
        foam += SAMPLE_TEXTURE2D(_WaveDisplacement2, sampler_WaveDisplacement2, uv2).a * 0.2;

        foam = smoothstep(_FoamIntensity, 1.0, foam);
        return foam;
    }

    // ════════════════════════════════════════════════════════════
    //  ComputeCaustics — Snell 折射 Jacobian (世界空间差分)
    //
    //  ddx/ddy 替换为显式世界空间近邻采样。
    //  corrDet = |∂(hit)/∂x × ∂(hit)/∂z| → 小 = 光线会聚 → 亮
    // ════════════════════════════════════════════════════════════
    float3 ComputeCaustics(float3 positionWS, float3 mainLitDir, float3 scenePosWS, float sceneDepDf, float3 sceneNorWS)
    {
        float eta = 1.0 / 1.33;
        float eps = 0.1;

        float3 V = -normalize(mainLitDir);
        float3 N = ComputeFFTNormal(positionWS, float3(0, 1, 0));
        float3 correctDir = refract(V, N, eta);
        if (all(correctDir == 0)) return 0;

        float3 correctHit = positionWS + correctDir * (sceneDepDf / max(-correctDir.y, 0.01));

        // 邻居: 世界空间 ±Δx, ±Δz → FFTNormal → refract → hit
        #define CAUSTIC_NEIGHBOR(OUT, OFFSET) { \
            float3 p = positionWS + OFFSET; \
            float3 n = ComputeFFTNormal(p, float3(0, 1, 0)); \
            float3 r = refract(V, n, eta); \
            if (all(r == 0)) r = correctDir; \
            float d = max(p.y - scenePosWS.y, 0.01); \
            OUT = p + r * (d / max(-r.y, 0.01)); \
        }

        float3 hitR, hitL, hitU, hitD;
        CAUSTIC_NEIGHBOR(hitR, float3( eps, 0, 0));
        CAUSTIC_NEIGHBOR(hitL, float3(-eps, 0, 0));
        CAUSTIC_NEIGHBOR(hitU, float3( 0, 0, eps));
        CAUSTIC_NEIGHBOR(hitD, float3( 0, 0,-eps));
        #undef CAUSTIC_NEIGHBOR

        float3 dhdx = (hitR - hitL) / (2.0 * eps);
        float3 dhdz = (hitU - hitD) / (2.0 * eps);
        float corrDet = max(length(cross(dhdx, dhdz)), 1e-4);
        float sceneDet = 1.0 / max(abs(dot(sceneNorWS, float3(0, 1, 0))), 0.1);
        float intensity = sceneDet / corrDet;

        float confidence = dot(float3(0, 1, 0), N) * 0.5 + 0.5;
        intensity *= confidence;

        // RGB 色散
        float dR = length(refract(V, N, 1.0/1.330) - correctDir) * sceneDepDf;
        float dB = length(refract(V, N, 1.0/1.340) - correctDir) * sceneDepDf;

        float3 caustics = float3(
            saturate(intensity * (1.0 + dR * 5.0)),
            saturate(intensity),
            saturate(intensity * (1.0 + dB * 5.0))
        );
        return caustics * _Caustics;
    }

    float4 ComputePositionSS(float3 positionWS, float3 normalWS, float distortion)
    {
        positionWS -= normalWS * distortion;
        float4 positionCS = TransformWorldToHClip(positionWS);
        float4 positionSS = ComputeScreenPos(positionCS);
        return positionSS;
    }

    float4 ComparePositionSS(float3 positionWS, float4 positionSSDetail, float4 positionSSBasic)
    {
        float  depth = ComputeRelDepthDiff(positionWS, positionSSDetail);
        float  branch = step(depth, 0.0);
        return lerp(positionSSDetail, positionSSBasic, branch);
    }

    // ════════════════════════════════════════════════════════════
    //  Vert — 控制点 pass-through（不做位移，位移在 Domain）
    // ════════════════════════════════════════════════════════════
    HullControlPoint Vert(WaterAttributes input)
    {
        HullControlPoint output;
        output.positionOS = input.positionOS;
        output.normalOS   = input.normalOS;
        output.tangentOS  = input.tangentOS;
        output.uv         = input.uv;
        return output;
    }

    // ════════════════════════════════════════════════════════════
    //  Tessellation — Hull + Domain
    // ════════════════════════════════════════════════════════════
    struct TessellationFactors
    {
        float edge[3]  : SV_TessFactor;
        float inside   : SV_InsideTessFactor;
    };

    TessellationFactors HullConst(InputPatch<HullControlPoint, 3> patch)
    {
        TessellationFactors f;
        f.edge[0] = _TessellationFactor;
        f.edge[1] = _TessellationFactor;
        f.edge[2] = _TessellationFactor;
        f.inside  = _TessellationFactor;
        return f;
    }

    [domain("tri")]
    [partitioning("integer")]
    [outputtopology("triangle_cw")]
    [outputcontrolpoints(3)]
    [patchconstantfunc("HullConst")]
    HullControlPoint Hull(InputPatch<HullControlPoint, 3> patch, uint id : SV_OutputControlPointID)
    {
        return patch[id];
    }

    // ════════════════════════════════════════════════════════════
    //  Domain — 细分顶点 + FFT 位移
    // ════════════════════════════════════════════════════════════
    [domain("tri")]
    WaterVaryings Domain(
        TessellationFactors factors,
        OutputPatch<HullControlPoint, 3> patch,
        float3 bary : SV_DomainLocation)
    {
        WaterVaryings output;

        // 重心坐标插值
        float3 positionOS = patch[0].positionOS * bary.x
                          + patch[1].positionOS * bary.y
                          + patch[2].positionOS * bary.z;
        float3 normalOS   = patch[0].normalOS   * bary.x
                          + patch[1].normalOS   * bary.y
                          + patch[2].normalOS   * bary.z;
        float4 tangentOS  = patch[0].tangentOS  * bary.x
                          + patch[1].tangentOS  * bary.y
                          + patch[2].tangentOS  * bary.z;
        float2 uv         = patch[0].uv         * bary.x
                          + patch[1].uv         * bary.y
                          + patch[2].uv         * bary.z;

        float3 positionWS = TransformObjectToWorld(positionOS);
        positionWS += ComputeFFTDisplacement(positionWS);

        output.positionCS = TransformWorldToHClip(positionWS);
        output.positionWS = positionWS;
        output.positionSS = 0;
        output.normalWS   = TransformObjectToWorldNormal(normalOS);
        output.tangentWS  = TransformObjectToWorldDir(tangentOS.xyz);
        output.bitanentWS = cross(output.normalWS, output.tangentWS) * tangentOS.w * unity_WorldTransformParams.w;
        output.uv = uv;
        return output;
    }

    // ════════════════════════════════════════════════════════════
    //  Frag — 片元着色器
    // ════════════════════════════════════════════════════════════
    half4 Frag(WaterVaryings input) : SV_Target
    {
        float3 camPos      = GetCameraPositionWS();
        float3 positionWS  = input.positionWS;
        float3 defaultNormalWS = normalize(input.normalWS);
        float3 normalWS    = ComputeFFTNormal(positionWS, defaultNormalWS);
        float3 viewDirWS   = normalize(camPos - positionWS);

        float3 mainLitDir;
        float3 mainLitColor;
        float  mainLitDistanceAtten;
        float  mainLitShadowAtten;
        MainLight(positionWS, mainLitDir, mainLitColor, mainLitDistanceAtten, mainLitShadowAtten);

        float4 positionSS1 = ComputePositionSS(positionWS, normalWS, _Distortion * 0.1);
        float4 positionSS2 = ComputePositionSS(positionWS, normalWS, 0);
        float4 positionSS  = ComparePositionSS(positionWS, positionSS1, positionSS2);

        float2 screenUV   = positionSS.xy / positionSS.w;
        float  sceneDepth = SampleSceneDepth(screenUV);
        float3 sceneNorWS = SampleSceneNormals(screenUV);
        float3 scenePosWS = ComputeWorldSpacePosition(screenUV, sceneDepth, UNITY_MATRIX_I_VP);

        float normalDiff = dot(normalWS, defaultNormalWS);
        float sceneDepDf = positionWS.y - scenePosWS.y;
        float3 baseColor = lerp(_baseColorA.rgb, _baseColorB.rgb, saturate(sceneDepDf));

        float3 opaque      = ComputeOpaque(baseColor, positionWS, normalWS, viewDirWS, mainLitDir, mainLitColor, mainLitDistanceAtten, mainLitShadowAtten);
        float3 transparent = SampleSceneColor(screenUV);
        float3 caustics    = ComputeCaustics(positionWS, mainLitDir, scenePosWS, sceneDepDf, sceneNorWS);
        float  edge        = ComputeEdgeFoam(sceneDepDf, positionWS, normalDiff);
        float  wave        = ComputeWaveFoam(positionWS);
        float  foam        = saturate(edge + wave);

        // ── Debug ────────────────────────────────────────────
        #if defined(_DEBUGMODE_DISPLACEMENT)
            float3 disp = ComputeFFTDisplacement(positionWS);
            return half4(disp * 0.5 + 0.5, 1);
        #elif defined(_DEBUGMODE_NORMAL)
            float3 fftNormal = ComputeFFTNormal(positionWS, defaultNormalWS);
            return half4(fftNormal * 0.5 + 0.5, 1);
        #elif defined(_DEBUGMODE_HEIGHT)
            float3 dispH = ComputeFFTDisplacement(positionWS);
            float h = dispH.y * 0.5 + 0.5;
            return half4(h, h, h, 1);
        #elif defined(_DEBUGMODE_CASCADE0)
            float2 uv0 = positionWS.xz / _WavePatchSize0;
            float3 c0 = SAMPLE_TEXTURE2D(_WaveDisplacement0, sampler_WaveDisplacement0, uv0).rgb;
            return half4(c0 * 0.5 + 0.5, 1);
        #elif defined(_DEBUGMODE_CASCADE1)
            float2 uv1 = positionWS.xz / _WavePatchSize1;
            float3 c1 = SAMPLE_TEXTURE2D(_WaveDisplacement1, sampler_WaveDisplacement1, uv1).rgb;
            return half4(c1 * 0.5 + 0.5, 1);
        #elif defined(_DEBUGMODE_CASCADE2)
            float2 uv2 = positionWS.xz / _WavePatchSize2;
            float3 c2 = SAMPLE_TEXTURE2D(_WaveDisplacement2, sampler_WaveDisplacement2, uv2).rgb;
            return half4(c2 * 0.5 + 0.5, 1);
        #endif

        float3 color = lerp(transparent, opaque, _Alpha);
        color += (foam) * mainLitColor;
        return half4(caustics, 1);

        return half4(color, 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex Vert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Frag
            #pragma shader_feature_local _DEBUGMODE_OFF _DEBUGMODE_DISPLACEMENT _DEBUGMODE_NORMAL _DEBUGMODE_HEIGHT _DEBUGMODE_CASCADE0 _DEBUGMODE_CASCADE1 _DEBUGMODE_CASCADE2
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX
            ENDHLSL
        }
    }
}
