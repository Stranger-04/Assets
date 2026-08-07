Shader "Hidden/CelToon/SSR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
    #include "Assets/Mine/Special/HLSL/BlurFunction.hlsl"

    // Blit.hlsl provides: Attributes, Varyings (with texcoord), Vert(), TEXTURE2D_X(_BlitTexture)

    float _StepSize;
    float _MaxDistance;

    float _Thickness;
    float _Smoothness;
    float _JitterScale;
    float _BlurScale;

    int _StepCount;
    int _BinaryCount;

    int _FromMipLevel;
    int _MaxMipLevel;
    float4 _TexelSize;

    float4x4 _CameraViewMatrix;
    float4x4 _CameraProjectionMatrix;

    // HiZ depth pyramid — per-mip textures set by C# (RenderGraph compatible)
    TEXTURE2D_X(_HiZTex0);
    TEXTURE2D_X(_HiZTex1);
    TEXTURE2D_X(_HiZTex2);
    TEXTURE2D_X(_HiZTex3);
    TEXTURE2D_X(_HiZTex4);
    TEXTURE2D_X(_HiZTex5);
    TEXTURE2D_X(_HiZTex6);
    TEXTURE2D_X(_HiZTex7);

    float SampleHiZDepthAtMip(float2 uv, int mipLevel)
    {
        [flatten]
        switch (mipLevel)
        {
            case 0: return SAMPLE_TEXTURE2D_X(_HiZTex0, sampler_LinearClamp, uv).r;
            case 1: return SAMPLE_TEXTURE2D_X(_HiZTex1, sampler_LinearClamp, uv).r;
            case 2: return SAMPLE_TEXTURE2D_X(_HiZTex2, sampler_LinearClamp, uv).r;
            case 3: return SAMPLE_TEXTURE2D_X(_HiZTex3, sampler_LinearClamp, uv).r;
            case 4: return SAMPLE_TEXTURE2D_X(_HiZTex4, sampler_LinearClamp, uv).r;
            case 5: return SAMPLE_TEXTURE2D_X(_HiZTex5, sampler_LinearClamp, uv).r;
            case 6: return SAMPLE_TEXTURE2D_X(_HiZTex6, sampler_LinearClamp, uv).r;
            default: return SAMPLE_TEXTURE2D_X(_HiZTex7, sampler_LinearClamp, uv).r;
        }
    }

    float4 HitProcess(float4 color, float3 reflectDir, float2 currentUV)
    {
        float3 normalHitWS = SampleSceneNormals(currentUV);
        float ndotr = dot(normalHitWS, - reflectDir);
        if(ndotr <= 0.0)
            return color;

        float3 result = SampleSceneColor(currentUV);
        float alpha = 1.0;
        float2 edge  = abs(currentUV * 2 - 1);
        float edgeFade = 1 - pow(max(edge.x, edge.y), 4);
        alpha *= edgeFade;
        alpha *= _Smoothness;

        return float4(result, alpha);
    }

    bool HitTest(float dk, float2 ds, float3 dv, inout float K, inout float2 S, inout float3 V, out float depthDiff, out float thickness)
    {
        [loop]
        for (int i = 0; i < _StepCount; i++)
        {
            K += dk;
            S += ds;
            V += dv;

            if (S.x < 0 || S.x > 1 || S.y < 0 || S.y > 1)
                return false;

            float sceneRawDepth = SampleSceneDepth(S);
            float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
            float rayEyeDepth   =  - V.z / K;
            depthDiff = rayEyeDepth - sceneEyeDepth;
            thickness = _Thickness;
            if (depthDiff > 0)
            {
                return true;
            }
        }
        return false;
    }

    half4 BinaryProcess(float4 color, float3 reflectDir, float dk, float2 ds, float3 dv, float K, float2 S, float3 V)
    {
        [loop]
        for (int i = 0; i< _BinaryCount; i++)
        {
            float depthDiff;
            float thickness;
            bool hit = HitTest(dk, ds, dv, K, S, V, depthDiff, thickness);
            if (hit)
            {
                if(depthDiff < thickness)
                {
                    float4 result = HitProcess(color, reflectDir, S);
                    if(result.a > 0.0)
                    {
                        color = half4(result.rgb, result.a);
                        break;
                    }
                }
                K -= dk;
                S -= ds;
                V -= dv;

                dk *= 0.5;
                ds *= 0.5;
                dv *= 0.5;
            }
            else
            {
                break;
            }
        }
        return color;
    }

    half4 SampleDepth(float2 uv, float2 offset, int mipLevel)
    {
        offset *= _TexelSize.xy;
        return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, mipLevel);;
    }

    half4 SampleHiZDepth(float2 uv, int mipLevel)
    {
        return SampleHiZDepthAtMip(uv, mipLevel);
    }

    half4 Frag_HiZDepthMip(Varyings input) : SV_Target
    {
        float2 uv   = input.texcoord;
        half4 depth = half4(
            SampleDepth(uv, float2(-1, -1), _FromMipLevel).r,
            SampleDepth(uv, float2( 1, -1), _FromMipLevel).r,
            SampleDepth(uv, float2(-1,  1), _FromMipLevel).r,
            SampleDepth(uv, float2( 1,  1), _FromMipLevel).r
        );
        return max(max(depth.x, depth.y), max(depth.z, depth.w));
    }

    half4 HiZProcess(float4 color, float3 reflectDir, float dk, float2 ds, float3 dv, float K, float2 S, float3 V)
    {
        int mipLevel = 0;
        [loop]
        for (int i = 0; i< _StepCount; i++)
        {
            K += dk * exp2(mipLevel);
            S += ds * exp2(mipLevel);
            V += dv * exp2(mipLevel);

            if (S.x < 0 || S.x > 1 || S.y < 0 || S.y > 1)
                break;

            float sceneRawDepth = SampleHiZDepth(S, mipLevel).r;
            float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
            float rayEyeDepth   =  - V.z / K;
            float depthDiff = rayEyeDepth - sceneEyeDepth;

            if (depthDiff < 0)
            {
                mipLevel = min(mipLevel + 1, _MaxMipLevel);
            }
            else
            {
                if(mipLevel == 0)
                {
                    if(depthDiff < _Thickness)
                    {
                        float4 result = HitProcess(color, reflectDir, S);
                        if(result.a > 0.0)
                        {
                            color = half4(result.rgb, result.a);
                            break;
                        }
                    }
                }
                else
                {
                    K -= dk * exp2(mipLevel);
                    S -= ds * exp2(mipLevel);
                    V -= dv * exp2(mipLevel);

                    mipLevel --;
                }
            }
        }
        return color;
    }

    // DDA 2D
    half4 Frag_SSR_DDA(Varyings input) : SV_Target
    {
        // Initial setup
        float4 color = half4(0,0,0,0);
        float2 uv = input.texcoord;
        float rawDepth = SampleSceneDepth(uv);

        float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        float3 viewDir   = normalize(positionWS - GetCameraPositionWS());
        float3 normalWS  = SampleSceneNormals(uv);
        float3 reflectDir = reflect(viewDir, normalWS);

        float ndotv = saturate(dot(normalWS, -viewDir));
        if(ndotv <= 0.0)
            return color;
        // Initial setup complete

        // DDA Ray setup
        float3 startWS  = positionWS;
        float3 endWS    = positionWS + reflectDir * _MaxDistance;
        float3 startVS  = mul(_CameraViewMatrix, float4(startWS, 1)).xyz;
        float3 endVS    = mul(_CameraViewMatrix, float4(endWS, 1)).xyz;
        float4 startCS  = mul(_CameraProjectionMatrix, float4(startVS, 1));
        float4 endCS    = mul(_CameraProjectionMatrix, float4(endVS, 1));
        // Main points calculation
        float  startK   = 1.0 / startCS.w;
        float  endK     = 1.0 / endCS.w;
        float2 startS   = (float2(startCS.x, startCS.y * _ProjectionParams.x) * startK) * 0.5 + 0.5;
        float2 endS     = (float2(endCS.x, endCS.y * _ProjectionParams.x) * endK) * 0.5 + 0.5;
        float3 startV   = startVS * startK;
        float3 endV     = endVS * endK;
        // Step calculation
        float  dk   = (endK - startK) / _StepCount * _StepSize;
        float2 ds   = (endS - startS) / _StepCount * _StepSize;
        float3 dv   = (endV - startV) / _StepCount * _StepSize;

        // Jitter setup
        float jitter = frac(sin(dot(uv ,float2(12.9898,78.233))) * 43758.5453);
        float  K = startK + jitter * _JitterScale * dk;
        float2 S = startS + jitter * _JitterScale * ds;
        float3 V = startV + jitter * _JitterScale * dv;

        #if defined(SSR_HIZ2D)
            color = HiZProcess(color, reflectDir, dk, ds, dv, K, S, V);
        #else
            color = BinaryProcess(color, reflectDir, dk, ds, dv, K, S, V);
        #endif
        return color;
    }

    half4 Frag_SSR_Ray3D(Varyings input) : SV_Target
    {
        // Initial setup
        float4 color = half4(0,0,0,0);
        float2 uv = input.texcoord;
        float rawDepth = SampleSceneDepth(uv);

        float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        float3 viewDir   = normalize(positionWS - GetCameraPositionWS());
        float3 normalWS  = SampleSceneNormals(uv);
        float3 reflectDir = reflect(viewDir, normalWS);

        float ndotv = saturate(dot(normalWS, -viewDir));
        if(ndotv <= 0.0)
            return color;
        // Initial setup complete

        float3 startWS  = positionWS;
        float3 endWS    = positionWS + reflectDir * _MaxDistance;

        float3 dw = (endWS - startWS) / _StepCount;
        dw = dw * _StepSize;

        float jitter = frac(sin(dot(uv ,float2(12.9898,78.233))) * 43758.5453);
        float3 W = startWS + jitter * _JitterScale * dw;
        [loop]
        for (int i = 0; i < _StepCount; i++)
        {
            W += dw;

            float4 clip = mul(_CameraProjectionMatrix, mul(_CameraViewMatrix, float4(W,1)));
            float2 S = (float2(clip.x, clip.y * _ProjectionParams.x) * (1.0 / clip.w)) * 0.5 + 0.5;

            if (S.x < 0 || S.x > 1 || S.y < 0 || S.y > 1)
                break;

            float sceneRawDepth = SampleSceneDepth(S);
            float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
            float rayEyeDepth   =  clip.w;
            float depthDiff = rayEyeDepth - sceneEyeDepth;

            if (depthDiff > 0 && depthDiff < _Thickness)
            {
                float4 result = HitProcess(color, reflectDir, S);
                if(result.a > 0.0)
                {
                    color = half4(result.rgb, result.a);
                    break;
                }
            }
        }
        return color;
    }

    half4 Frag_BlurHorizontal(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
        half4 result = BlurHorizontal(uv, texelSize, _BlurScale, _BlitTexture, sampler_LinearClamp);

        return result;
    }

    half4 Frag_BlurVertical(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
        half4 result = BlurVertical(uv, texelSize, _BlurScale, _BlitTexture, sampler_LinearClamp);

        return result;
    }

    half4 Frag(Varyings input) : SV_Target
    {
        #if defined(SSR_DDA2D)
            return Frag_SSR_DDA(input);
        #elif defined(SSR_RAY3D)
            return Frag_SSR_Ray3D(input);
        #elif defined(SSR_HIZ2D)
            return Frag_SSR_DDA(input);
        #else
            return half4(0,0,0,0);
        #endif
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off
        ZWrite Off 
        ZTest Always 
        Blend One Zero

        Pass
        {
            Name "SSR_Raymarch"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma multi_compile _ SSR_DDA2D SSR_RAY3D SSR_HIZ2D
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "SSR_BlurHorizontal"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag_BlurHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "SSR_BlurVertical"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag_BlurVertical
            ENDHLSL
        }

        Pass
        {
            Name "SSR_HiZDepthMip"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag_HiZDepthMip
            ENDHLSL
        }
    }
}