using System.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SSLFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader sslShader;
        [Range(1, 256)] public int maxSteps = 32;
        [Range(0.1f, 100f)] public float maxDistance = 10f;
        [Range(0f, 5f)] public float intensity = 1f;
        [Range(0f, 2f)] public float sslScale = 0.5f;
        [Range(0f, 1f)] public float jitterScale = 0.5f;
        [Range(0f, 5f)] public float blurScale = 0.5f;
        [Range(0, 4)] public int blurLevels = 1;
        [Range(0, 4)] public int blurIterations = 1;
        public bool SSLFeature = true;
    }

    class SSLRenderPass : ScriptableRenderPass
    {
        private Material sslMaterial;
        private Settings settings;

        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tempMainRT;
            public TextureHandle[] blurPing;
            public TextureHandle[] blurPong;
            public int blurLevels;
            public int blurIterations;
            public bool showSSL;
        }

        public SSLRenderPass(Shader shader, Settings s)
        {
            settings = s;
            if (shader != null)
                sslMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid() || sslMaterial == null) return;

            // ── Volume 参数 ──
            var stack = VolumeManager.instance.stack;
            var vol = stack.GetComponent<SSLVolume>();
            if (vol == null || !vol.IsActive() || !cameraData.postProcessEnabled)
                return;

            // ── 材质参数 ──
            sslMaterial.SetInt("_MaxSteps", vol.maxSteps.value);
            sslMaterial.SetFloat("_MaxDistance", vol.maxDistance.value);
            sslMaterial.SetFloat("_Intensity", vol.intensity.value);
            sslMaterial.SetFloat("_SSLScale", vol.sslScale.value);
            sslMaterial.SetFloat("_BlurScale", vol.blurScale.value);
            sslMaterial.SetFloat("_JitterScale", vol.jitterScale.value);

            sslMaterial.DisableKeyword("SSL_FOG");
            sslMaterial.DisableKeyword("SSL_LIGHT");
            switch (vol.sslType.value)
            {
                case SSLVolume.SSLType.Fog: sslMaterial.EnableKeyword("SSL_FOG"); break;
                case SSLVolume.SSLType.Light: sslMaterial.EnableKeyword("SSL_LIGHT"); break;
            }

            // ── RT ──
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;

            TextureHandle tempMainRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_SSLTempMainRT", false);

            // 多级模糊纹理
            int maxLevel = settings.blurLevels;
            var blurPing = new TextureHandle[maxLevel + 1];
            var blurPong = new TextureHandle[maxLevel + 1];
            for (int i = 0; i <= maxLevel; i++)
            {
                int w = Mathf.Max(1, desc.width >> i);
                int h = Mathf.Max(1, desc.height >> i);
                var lvlDesc = new RenderTextureDescriptor(w, h, RenderTextureFormat.Default, 0)
                { sRGB = false, useMipMap = false };
                blurPing[i] = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, lvlDesc, $"_SSL_Ping_L{i}", false);
                blurPong[i] = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, lvlDesc, $"_SSL_Pong_L{i}", false);
            }

            using (var builder = renderGraph.AddUnsafePass<PassData>("SSL", out var passData))
            {
                passData.material = sslMaterial;
                passData.source = source;
                passData.tempMainRT = tempMainRT;
                passData.blurPing = blurPing;
                passData.blurPong = blurPong;
                passData.blurLevels = settings.blurLevels;
                passData.blurIterations = settings.blurIterations;
                passData.showSSL = settings.SSLFeature;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(tempMainRT, AccessFlags.ReadWrite);
                for (int i = 0; i <= maxLevel; i++)
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

                    // Pass 0: SSL Raymarch → blurPing[0] (full res)
                    Blitter.BlitCameraTexture(cmd, data.source, data.blurPing[0], data.material, 0);

                    // ── 多级模糊 ──
                    if (data.blurLevels > 0)
                    {
                        // Downsampling blur
                        for (int i = 0; i < data.blurLevels; i++)
                        {
                            int srcLvl = i;
                            int dstLvl = i + 1;
                            for (int j = 0; j < data.blurIterations; j++)
                            {
                                Blitter.BlitCameraTexture(cmd, data.blurPing[srcLvl], data.blurPong[dstLvl], data.material, 1);
                                Blitter.BlitCameraTexture(cmd, data.blurPong[dstLvl], data.blurPing[dstLvl], data.material, 2);
                            }
                        }

                        // Upsampling blur
                        for (int i = data.blurLevels - 1; i >= 0; i--)
                        {
                            int srcLvl = i + 1;
                            int dstLvl = i;
                            for (int j = 0; j < data.blurIterations; j++)
                            {
                                Blitter.BlitCameraTexture(cmd, data.blurPing[srcLvl], data.blurPong[dstLvl], data.material, 1);
                                Blitter.BlitCameraTexture(cmd, data.blurPong[dstLvl], data.blurPing[dstLvl], data.material, 2);
                            }
                        }
                    }

                    // Set global for other shaders
                    cmd.SetGlobalTexture("_SSLTex", data.blurPing[0]);

                    // Pass 3: Composite tempMain + SSL → screen
                    Blitter.BlitCameraTexture(cmd, data.tempMainRT, data.source, data.material, 3);

                    // Debug overlay
                    if (data.showSSL)
                    {
                        Blitter.BlitCameraTexture(cmd, data.blurPing[0], data.source);
                    }
                });
            }
        }
    }

    public Settings settings = new Settings();
    SSLRenderPass sslPass;

    public override void Create()
    {
        sslPass = null;
        if (settings.sslShader != null)
            sslPass = new SSLRenderPass(settings.sslShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (sslPass != null)
            renderer.EnqueuePass(sslPass);
    }
}
