using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class DDOFFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ddofShader;
        [Range(0f, 50f)] public float focusRange = 2.0f;
        [Range(0f, 5f)] public float blurScale = 2.5f;
    }

    class DDOFPass : ScriptableRenderPass
    {
        private Material ddofMaterial;
        private Settings settings;

        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle maskRT;
            public TextureHandle tempRT;
            public TextureHandle blur1RT;
            public TextureHandle blur2RT;
        }

        public DDOFPass(Shader shader, Settings s)
        {
            settings = s;
            if (shader != null)
                ddofMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid() || ddofMaterial == null) return;

            ddofMaterial.SetFloat("_FocusRange", settings.focusRange);
            ddofMaterial.SetFloat("_BlurScale", settings.blurScale);

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            TextureHandle maskRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_DDOFMaskRT", false);
            TextureHandle tempRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_DDOFTempMainRT", false);
            TextureHandle blur1RT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_DDOFBlur1RT", false);
            TextureHandle blur2RT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_DDOFBlur2RT", false);

            using (var builder = renderGraph.AddUnsafePass<PassData>("DDOF", out var passData))
            {
                passData.material = ddofMaterial;
                passData.source = source;
                passData.maskRT = maskRT;
                passData.tempRT = tempRT;
                passData.blur1RT = blur1RT;
                passData.blur2RT = blur2RT;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(maskRT, AccessFlags.ReadWrite);
                builder.UseTexture(tempRT, AccessFlags.ReadWrite);
                builder.UseTexture(blur1RT, AccessFlags.ReadWrite);
                builder.UseTexture(blur2RT, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // Copy source to temp
                    Blitter.BlitCameraTexture(cmd, data.source, data.tempRT);

                    // Set globals for shader
                    cmd.SetGlobalTexture("_DDOFTempMainTex", data.tempRT);

                    // Pass 0: CoC mask
                    Blitter.BlitCameraTexture(cmd, data.source, data.maskRT, data.material, 0);
                    cmd.SetGlobalTexture("_DDOFCoCTex", data.maskRT);

                    // Pass 1: Horizontal blur
                    Blitter.BlitCameraTexture(cmd, data.source, data.blur1RT, data.material, 1);

                    // Pass 2: Vertical blur
                    Blitter.BlitCameraTexture(cmd, data.blur1RT, data.blur2RT, data.material, 2);

                    // Pass 3: Composite to screen
                    Blitter.BlitCameraTexture(cmd, data.blur2RT, data.source, data.material, 3);
                });
            }
        }
    }

    public Settings settings = new Settings();
    DDOFPass ddofpass;

    public override void Create()
    {
        ddofpass = null;
        if (settings.ddofShader != null)
            ddofpass = new DDOFPass(settings.ddofShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ddofpass != null)
            renderer.EnqueuePass(ddofpass);
    }
}
