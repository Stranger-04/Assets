Shader "Custom/Water"
{
    Properties
    {
        _baseColorA("Base Color A", Color) = (1, 1, 1, 1)
        _baseColorB("Base Color B", Color) = (1, 1, 1, 1)

        _NormalTex("Normal Texture", 2D) = "bump" {}
        [Range(0, 1)]_NormalScale("Normal Scale", Float) = 1.0
        [Range(0, 1)]_NormalSpeed("Normal Speed", Float) = 1.0
        [Range(0, 1)]_NormalIntensity("Normal Intensity", Float) = 1.0

        _FoamTex("Foam Texture", 2D) = "white" {}
        [Range(0, 1)]_FoamScale("Foam Scale", Float) = 1.0
        [Range(0, 1)]_FoamSpeed("Foam Speed", Float) = 1.0
        [Range(0, 1)]_FoamIntensity("Foam Intensity", Float) = 0.5

        [Range(0, 1)]_Distortion("Distortion", Float) = 0.1
        [Range(0, 1)]_Caustics("Caustics", Float) = 1.0
        [Range(0, 1)]_Alpha("Alpha", Float) = 1.0
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

    TEXTURE2D_X(_NormalTex);
    SAMPLER(sampler_NormalTex);
    TEXTURE2D_X(_FoamTex);
    SAMPLER(sampler_FoamTex);
    TEXTURE2D(_WaveFoamTex);
    SAMPLER(sampler_WaveFoamTex);
    float _WaveFoamWorldTexSize;

    CBUFFER_START(UnityPerMaterial)
        float4 _baseColorA;
        float4 _baseColorB;

        float _NormalScale;
        float _NormalSpeed;
        float _NormalIntensity;

        float _FoamScale;
        float _FoamSpeed;
        float _FoamIntensity;

        float _Distortion;
        float _Caustics;
        float _Alpha;

    CBUFFER_END

    struct WaterAttributes
    {
        float3 positionOS   : POSITION;
        float3 normalOS     : NORMAL;
        float4 tangentOS    : TANGENT;
        float2 uv           : TEXCOORD0;
    };

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

    float3 ComputeNormalWS(float3 normalWS, float3 tangentWS, float3 bitanentWS, float2 uv)
    {
        float3 normalTS1 = UnpackNormal(_NormalTex.Sample(sampler_NormalTex, uv * _NormalScale * 0.1 + _Time.y * _NormalSpeed * 0.01));
        float3 normalTS2 = UnpackNormal(_NormalTex.Sample(sampler_NormalTex, uv * _NormalScale * 0.05 - _Time.y * _NormalSpeed * 0.005));
        float3 normalTS  = BlendNormal_RNM(normalTS1, normalTS2, 1.0);
        // float3 normalTS = BlendNormal_Linear(normalTS1, normalTS2, 0.5);
        return normalize(
            tangentWS  * normalTS.x * _NormalIntensity +
            bitanentWS * normalTS.y * _NormalIntensity +
            normalWS   * normalTS.z
        );
    }

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
        // ndotv = smoothstep(0.4, 0.6, ndotv);

        float  shadowArea = mainLitShadowAtten * 0.5 + 0.5;
        float3 radiance = mainLitColor * mainLitDistanceAtten * shadowArea;
        float3 F0 = lerp(0.04, baseColor, metallic);
        float3 F  = F_Fast(F0, VdotH);

        float3 diffuse  = Diff_Lambert(baseColor) * PI * radiance * (1.0 - metallic) * (1.0 - F);
        float3 specular = Spec_Unity(ndoth, LdotH, VdotH, 0.0, 0.0, roughness, 0.0) * PI * radiance * ndotl * F;
        float3 ambient  = BRDF_Env(baseColor, ndotv, normalWS, viewDirWS, roughness, metallic, unity_SpecCube0, samplerunity_SpecCube0) * mainLitColor;
        float3 opaque   = diffuse + specular + ambient;
        // return ndotl;
        return opaque;
    }

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

    float3 ComputeCaustics(float3 positionWS, float3 mainLitDir, float3 scenePosWS, float sceneDepDf, float3 sceneNorWS)
    {
        float3 AABB = float3(5, 2, 5);
        float  sceneNorDf = dot(mainLitDir, sceneNorWS) * 0.5 + 0.5;
        float3 scenePosOS = TransformWorldToObject(scenePosWS);
        float  maskInside = all(step(abs(scenePosOS), AABB));
        float  mask = sceneDepDf * sceneNorDf;

        float eta = 1.0 / 1.33;

        float3 refractDir = refract(-mainLitDir, float3(0, 1, 0), eta);
        float3 surfaceRay = refractDir * (positionWS.y - scenePosWS.y) / refractDir.y;
        float3 surfaceHit = scenePosWS + surfaceRay; 
        float3 surfaceNor = ComputeNormalWS(float3(0, 1, 0), float3(1, 0, 0), float3(0, 0, 1), surfaceHit.xz);
        float3 correctDir = refract(-mainLitDir, surfaceNor, eta);
        float3 correctRay = correctDir * (positionWS.y - scenePosWS.y) / correctDir.y;
        float3 correctHit = scenePosWS + correctRay;
        float3 correctNor = ComputeNormalWS(float3(0, 1, 0), float3(1, 0, 0), float3(0, 0, 1), correctHit.xz);

        float3 corrDDX = ddx(correctHit);
        float3 corrDDY = ddy(correctHit);
        float corrDet = max(length(cross(corrDDX, corrDDY)), 1e-6);
        float3 sceneDDX = ddx(scenePosWS);
        float3 sceneDDY = ddy(scenePosWS);
        float sceneDet = max(length(cross(sceneDDX, sceneDDY)), 1e-6);
        float intensity = sceneDet / corrDet;
        float confidence = dot(surfaceNor, correctNor) * 0.5 + 0.5;
        intensity *= confidence * mask;

        float3 correctDirR = refract(-mainLitDir, surfaceNor, eta * 1.1);
        float3 correctRayR = correctDirR * (positionWS.y - scenePosWS.y) / correctDirR.y;
        float3 correctHitR = scenePosWS + correctRayR;
        float3 correctDirB = refract(-mainLitDir, surfaceNor, eta * 0.9);
        float3 correctRayB = correctDirB * (positionWS.y - scenePosWS.y) / correctDirB.y;
        float3 correctHitB = scenePosWS + correctRayB;

        float3 refractDirG = refractDir;
        float3 surfaceHitG = surfaceHit;
        float3 refractDirR = refract(-mainLitDir, float3(0, 1, 0), eta * 1.1);
        float3 surfaceHitR = refractDirR * (positionWS.y - scenePosWS.y) / refractDirR.y + scenePosWS;
        float3 refractDirB = refract(-mainLitDir, float3(0, 1, 0), eta * 0.9);
        float3 surfaceHitB = refractDirB * (positionWS.y - scenePosWS.y) / refractDirB.y + scenePosWS;
        float colorR = _FoamTex.Sample(sampler_FoamTex, surfaceHitR.xz * _FoamScale + _Time.y * _FoamSpeed * 0.1).g;
        float colorG = _FoamTex.Sample(sampler_FoamTex, surfaceHitG.xz * _FoamScale + _Time.y * _FoamSpeed * 0.1).g;
        float colorB = _FoamTex.Sample(sampler_FoamTex, surfaceHitB.xz * _FoamScale + _Time.y * _FoamSpeed * 0.1).g;
        float3 caustics = float3(colorR, colorG, colorB);
        return caustics * intensity * _Caustics;
    }

    float ComputeWaveFoam(float3 positionWS)
    {
        float2 foamUV = positionWS.xz / _WaveFoamWorldTexSize;
        return SAMPLE_TEXTURE2D(_WaveFoamTex, sampler_WaveFoamTex, foamUV).r;
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
        // float2 screenSize = _ScreenParams.xy;
        // float2 screenUV = (floor(positionSSDetail.xy / positionSSDetail.w * screenSize) + 0.5) / screenSize;
        // float depthRaw = SampleSceneDepth(screenUV);
        // float depthEye = LinearEyeDepth(depthRaw, _ZBufferParams);
        // float branch = step((depthEye - positionSSBasic.w), 0.0);
        float  depth = ComputeRelDepthDiff(positionWS, positionSSDetail);
        float  branch = step(depth, 0.0);
        float4 positionSS = lerp(positionSSDetail, positionSSBasic, branch);
        return positionSS;
    }

    WaterVaryings Vert(WaterAttributes input)
    {
        WaterVaryings output;
        output.positionCS = TransformObjectToHClip(input.positionOS);
        output.positionWS = TransformObjectToWorld(input.positionOS);
        output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
        output.tangentWS  = TransformObjectToWorldDir(input.tangentOS.xyz);
        output.bitanentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w * unity_WorldTransformParams.w;
        output.uv = input.uv;
        return output;
    }

    half4 Frag(WaterVaryings input) : SV_Target
    {
        float3 camPos      = GetCameraPositionWS();
        float3 normalWS    = ComputeNormalWS(input.normalWS, input.tangentWS, input.bitanentWS, input.positionWS.xz);
        float3 positionWS  = input.positionWS;
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

        float normalDiff = dot(normalWS, input.normalWS);
        float sceneDepDf = positionWS.y - scenePosWS.y;
        float3 baseColor = lerp(_baseColorA.rgb, _baseColorB.rgb, saturate(sceneDepDf));

        float3 opaque = ComputeOpaque(baseColor, positionWS, normalWS, viewDirWS, mainLitDir, mainLitColor, mainLitDistanceAtten, mainLitShadowAtten);
        float3 transparent = SampleSceneColor(screenUV);
        float3 caustics = ComputeCaustics(positionWS, mainLitDir, scenePosWS, sceneDepDf, sceneNorWS);
        float  edge = ComputeEdgeFoam(sceneDepDf, positionWS, normalDiff);
        float  wave = ComputeWaveFoam(positionWS);
        float  foam = saturate(edge + wave);
        float3 color = lerp(transparent, opaque, _Alpha);
        color += (foam + caustics) * mainLitColor;

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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX
            ENDHLSL
        }
    }
}