using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SSRFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ssrShader;
        [Range(0.1f, 2f)] public float stepSize = 0.2f;
        [Range(1f, 200f)] public float maxDistance = 50f;

        [Range(8, 256)] public int stepCount = 64;
        [Range(0, 32)] public int binaryCount = 6;
        [Range(1, 8)] public int mipCount = 4;

        [Range(0.001f, 0.5f)] public float thickness = 0.05f;
        [Range(0f, 1f)] public float smoothness = 1f;
        [Range(0f, 1f)] public float jitterScale = 0.5f;
        [Range(0f, 5f)] public float blurScale = 0.5f;

        public bool SSRFeature = true;

        public enum SSRType
        {
            HiZ2D,
            DDA2D,
            Ray3D
        }
        public SSRType ssrType = SSRType.DDA2D;
    }

    // ════════════════════════════════════════════════════════════
    //  SSRRenderPass — Unity 6 RecordRenderGraph
    // ════════════════════════════════════════════════════════════
    class SSRRenderPass : ScriptableRenderPass
    {
        private Material ssrMaterial;
        private Settings settings;

        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle cameraDepth;
            public TextureHandle ssrRT;
            public TextureHandle blur1RT;
            public TextureHandle blur2RT;
            public TextureHandle[] hizMips;
            public int mipCount;
            public bool showSSR;
            public int hizBaseWidth;
            public int hizBaseHeight;
        }

        public SSRRenderPass(Shader shader, Settings s)
        {
            settings = s;
            if (shader != null)
                ssrMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        // ════════════════════════════════════════════════════════════
        //  RecordRenderGraph — Unity 6 入口
        // ════════════════════════════════════════════════════════════
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle cameraDepth = resourceData.cameraDepthTexture;
            if (!source.IsValid() || ssrMaterial == null) return;

            // ── 材质参数 ──
            ssrMaterial.SetFloat("_StepSize", settings.stepSize);
            ssrMaterial.SetFloat("_MaxDistance", settings.maxDistance);
            ssrMaterial.SetFloat("_Thickness", settings.thickness);
            ssrMaterial.SetFloat("_Smoothness", settings.smoothness);
            ssrMaterial.SetFloat("_JitterScale", settings.jitterScale);
            ssrMaterial.SetFloat("_BlurScale", settings.blurScale);
            ssrMaterial.SetInt("_StepCount", settings.stepCount);
            ssrMaterial.SetInt("_BinaryCount", settings.binaryCount);
            ssrMaterial.SetFloat("_MaxMipLevel", settings.mipCount);

            // ── 关键字 ──
            ssrMaterial.DisableKeyword("SSR_DDA2D");
            ssrMaterial.DisableKeyword("SSR_RAY3D");
            ssrMaterial.DisableKeyword("SSR_HIZ2D");
            switch (settings.ssrType)
            {
                case Settings.SSRType.DDA2D: ssrMaterial.EnableKeyword("SSR_DDA2D"); break;
                case Settings.SSRType.Ray3D: ssrMaterial.EnableKeyword("SSR_RAY3D"); break;
                case Settings.SSRType.HiZ2D: ssrMaterial.EnableKeyword("SSR_HIZ2D"); break;
            }

            // ── 相机矩阵（避免 GetGPUProjectionMatrix() 无参版触发 cameraColorTargetHandle）──
            ssrMaterial.SetMatrix("_CameraViewMatrix", cameraData.GetViewMatrix());
            ssrMaterial.SetMatrix("_CameraProjectionMatrix",
                GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true));

            // ── 创建 SSR 临时 RT ──
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;

            TextureHandle ssrRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_SSRResultRT", false);
            TextureHandle blur1RT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_SSRBlur1RT", false);
            TextureHandle blur2RT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_SSRBlur2RT", false);

            // ── 创建 HiZ 金字塔 TextureHandles（固定 8 级，匹配 shader 声明）──
            const int maxMipLevels = 8;
            int hizBaseWidth = Mathf.Max((int)Mathf.Ceil(Mathf.Log(desc.width, 2) - 1.0f), 1);
            int hizBaseHeight = Mathf.Max((int)Mathf.Ceil(Mathf.Log(desc.height, 2) - 1.0f), 1);
            hizBaseWidth = 1 << hizBaseWidth;
            hizBaseHeight = 1 << hizBaseHeight;

            TextureHandle[] hizMips = new TextureHandle[maxMipLevels];
            for (int i = 0; i < maxMipLevels; i++)
            {
                var hizMipDesc = new RenderTextureDescriptor(
                    Mathf.Max(1, hizBaseWidth >> i),
                    Mathf.Max(1, hizBaseHeight >> i),
                    RenderTextureFormat.RFloat, 0, 1);
                hizMipDesc.sRGB = false;
                hizMipDesc.useMipMap = false;
                hizMipDesc.msaaSamples = 1;
                hizMips[i] = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, hizMipDesc, $"_SSR_HiZ_Mip{i}", false);
            }

            // ── UnsafePass：HiZ 生成 + SSR 光线步进 + 模糊 ──
            using (var builder = renderGraph.AddUnsafePass<PassData>("SSR", out var passData))
            {
                passData.material = ssrMaterial;
                passData.source = source;
                passData.cameraDepth = cameraDepth;
                passData.ssrRT = ssrRT;
                passData.blur1RT = blur1RT;
                passData.blur2RT = blur2RT;
                passData.hizMips = hizMips;
                passData.mipCount = settings.mipCount;
                passData.showSSR = settings.SSRFeature;
                passData.hizBaseWidth = hizBaseWidth;
                passData.hizBaseHeight = hizBaseHeight;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(cameraDepth, AccessFlags.Read);
                builder.UseTexture(ssrRT, AccessFlags.ReadWrite);
                builder.UseTexture(blur1RT, AccessFlags.ReadWrite);
                builder.UseTexture(blur2RT, AccessFlags.ReadWrite);
                foreach (var mip in hizMips)
                    builder.UseTexture(mip, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // ════════════════════════════════════════════════════════════
                    //  HiZ 深度金字塔生成
                    // ════════════════════════════════════════════════════════════

                    // Mip 0: 复制相机深度
                    Blitter.BlitCameraTexture(cmd, data.cameraDepth, data.hizMips[0]);
                    cmd.SetGlobalTexture("_HiZTex0", data.hizMips[0]);

                    // Mip 1+: 逐级降采样
                    int actualMips = Mathf.Min(data.mipCount, 8);
                    for (int i = 1; i < actualMips; i++)
                    {
                        int srcW = Mathf.Max(1, data.hizBaseWidth >> (i - 1));
                        int srcH = Mathf.Max(1, data.hizBaseHeight >> (i - 1));
                        data.material.SetFloat("_FromMipLevel", i - 1);
                        data.material.SetVector("_TexelSize", new Vector4(
                            1.0f / srcW, 1.0f / srcH, srcW, srcH));

                        Blitter.BlitCameraTexture(cmd, data.hizMips[i - 1], data.hizMips[i], data.material, 3);
                        cmd.SetGlobalTexture($"_HiZTex{i}", data.hizMips[i]);
                    }

                    // 填充剩余 mip 槽位，避免 unbound texture
                    for (int i = actualMips; i < 8; i++)
                    {
                        cmd.SetGlobalTexture($"_HiZTex{i}", data.hizMips[actualMips - 1]);
                    }

                    // ════════════════════════════════════════════════════════════
                    //  SSR 光线步进（Pass 0）+ 双向模糊（Pass 1, 2）
                    // ════════════════════════════════════════════════════════════

                    // Pass 0: SSR Raymarch → blur1RT
                    Blitter.BlitCameraTexture(cmd, data.source, data.blur1RT, data.material, 0);

                    // Pass 1: Horizontal blur → blur2RT
                    Blitter.BlitCameraTexture(cmd, data.blur1RT, data.blur2RT, data.material, 1);

                    // Pass 2: Vertical blur → ssrRT
                    Blitter.BlitCameraTexture(cmd, data.blur2RT, data.ssrRT, data.material, 2);

                    // ── 全局纹理 ──
                    cmd.SetGlobalTexture("_SSRTexture", data.ssrRT);

                    // ── Debug 叠加 ──
                    if (data.showSSR)
                    {
                        Blitter.BlitCameraTexture(cmd, data.ssrRT, data.source);
                    }
                });
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Feature 入口
    // ════════════════════════════════════════════════════════════

    public Settings settings = new Settings();
    SSRRenderPass ssrPass;

    public override void Create()
    {
        ssrPass = null;

        if (settings.ssrShader != null)
        {
            ssrPass = new SSRRenderPass(settings.ssrShader, settings);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ssrPass != null)
        {
            renderer.EnqueuePass(ssrPass);
        }
    }
}