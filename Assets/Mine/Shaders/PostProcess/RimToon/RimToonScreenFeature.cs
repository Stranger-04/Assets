using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class RimToonScreenFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader rimToonScreenShader;
        [Range(1f, 10f)] public float rimPower = 5.0f;
        [Range(0f, 5f)] public float blurScale = 0.5f;
        [Range(0f, 1f)] public float blurIntensity = 0.5f;
        [Range(0, 4)] public int blurLevels = 1;
        [Range(0, 4)] public int blurIterations = 1;
    }

    class RimToonScreenPass : ScriptableRenderPass
    {
        private Material rtsMaterial;
        private Settings settings;

        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tempMainRT;
            public TextureHandle maskRT;
            public TextureHandle maskPongRT;
            public TextureHandle colorRT;
            public TextureHandle[] blurPing;
            public TextureHandle[] blurPong;
            public int blurLevels;
            public int blurIterations;
        }

        public RimToonScreenPass(Shader shader, Settings s)
        {
            settings = s;
            if (shader != null)
                rtsMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid() || rtsMaterial == null) return;

            // ── 材质参数 ──
            rtsMaterial.SetFloat("_RimPower", settings.rimPower);
            rtsMaterial.SetFloat("_BlurScale", settings.blurScale);
            rtsMaterial.SetFloat("_BlurIntensity", settings.blurIntensity);

            // ── RT ──
            RenderTextureDescriptor baseDesc = cameraData.cameraTargetDescriptor;
            baseDesc.depthBufferBits = 0;
            baseDesc.msaaSamples = baseDesc.msaaSamples;

            TextureHandle tempMainRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, baseDesc, "_RTTempMainRT", false);

            var maskDesc = baseDesc;
            maskDesc.colorFormat = RenderTextureFormat.R8;
            TextureHandle maskRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, maskDesc, "_RimToonMaskRT", false);
            TextureHandle maskPongRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, maskDesc, "_RimToonMaskPongRT", false);

            var colorDesc = baseDesc;
            colorDesc.colorFormat = RenderTextureFormat.ARGB32;
            TextureHandle colorRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, colorDesc, "_RimToonColorRT", false);

            // ── 多级模糊纹理（每级一对）──
            int maxLevel = settings.blurLevels;
            var blurPing = new TextureHandle[maxLevel];
            var blurPong = new TextureHandle[maxLevel];
            for (int i = 0; i < maxLevel; i++)
            {
                int w = Mathf.Max(1, colorDesc.width >> i);
                int h = Mathf.Max(1, colorDesc.height >> i);
                var lvlDesc = new RenderTextureDescriptor(w, h, RenderTextureFormat.Default, 0);
                blurPing[i] = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, lvlDesc, $"_RimToon_Ping_L{i}", false);
                blurPong[i] = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, lvlDesc, $"_RimToon_Pong_L{i}", false);
            }

            using (var builder = renderGraph.AddUnsafePass<PassData>("RimToon", out var passData))
            {
                passData.material = rtsMaterial;
                passData.source = source;
                passData.tempMainRT = tempMainRT;
                passData.maskRT = maskRT;
                passData.maskPongRT = maskPongRT;
                passData.colorRT = colorRT;
                passData.blurPing = blurPing;
                passData.blurPong = blurPong;
                passData.blurLevels = settings.blurLevels;
                passData.blurIterations = settings.blurIterations;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(tempMainRT, AccessFlags.ReadWrite);
                builder.UseTexture(maskRT, AccessFlags.ReadWrite);
                builder.UseTexture(maskPongRT, AccessFlags.ReadWrite);
                builder.UseTexture(colorRT, AccessFlags.ReadWrite);
                for (int i = 0; i < maxLevel; i++)
                {
                    builder.UseTexture(blurPing[i], AccessFlags.ReadWrite);
                    builder.UseTexture(blurPong[i], AccessFlags.ReadWrite);
                }
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // Copy source → tempMain
                    Blitter.BlitCameraTexture(cmd, data.source, data.tempMainRT);
                    cmd.SetGlobalTexture("_RTTempMainTex", data.tempMainRT);

                    // Pass 0: Mask generation → maskRT
                    Blitter.BlitCameraTexture(cmd, data.source, data.maskRT, data.material, 0);

                    // Pass 1: Mask post-process → maskPongRT
                    Blitter.BlitCameraTexture(cmd, data.maskRT, data.maskPongRT, data.material, 1);
                    cmd.SetGlobalTexture("_RimToonMaskRT", data.maskPongRT);

                    // Pass 2: Color generation → colorRT
                    Blitter.BlitCameraTexture(cmd, data.source, data.colorRT, data.material, 2);

                    // ── 多级模糊 ──
                    // Copy colorRT → blurPing[0]
                    Blitter.BlitCameraTexture(cmd, data.colorRT, data.blurPing[0]);

                    if (data.blurLevels > 0)
                    {
                        // Downsampling blur (level 0 = full res, level 1 = half, ...)
                        for (int i = 0; i < data.blurLevels; i++)
                        {
                            for (int j = 0; j < data.blurIterations; j++)
                            {
                                Blitter.BlitCameraTexture(cmd, data.blurPing[i], data.blurPong[i], data.material, 3);
                                Blitter.BlitCameraTexture(cmd, data.blurPong[i], data.blurPing[i], data.material, 4);
                            }
                        }

                        // Upsampling blur
                        for (int i = data.blurLevels - 2; i >= 0; i--)
                        {
                            for (int j = 0; j < data.blurIterations; j++)
                            {
                                Blitter.BlitCameraTexture(cmd, data.blurPing[i + 1], data.blurPong[i], data.material, 3);
                                Blitter.BlitCameraTexture(cmd, data.blurPong[i], data.blurPing[i], data.material, 4);
                            }
                        }
                    }

                    // Set globals for final composite
                    cmd.SetGlobalTexture("_RimToonBlurRT", data.blurPing[0]);
                    cmd.SetGlobalTexture("_RimToonColorRT", data.colorRT);

                    // Pass 5: Final composite → screen
                    Blitter.BlitCameraTexture(cmd, data.source, data.source, data.material, 5);
                });
            }
        }
    }

    public Settings settings = new Settings();
    RimToonScreenPass srtpass;

    public override void Create()
    {
        srtpass = null;
        if (settings.rimToonScreenShader != null)
            srtpass = new RimToonScreenPass(settings.rimToonScreenShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (srtpass != null)
            renderer.EnqueuePass(srtpass);
    }
}
