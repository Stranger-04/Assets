using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SNNFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader snnShader;
        [Range(1, 10)] public int Radius = 3;
    }

    class SNNPass : ScriptableRenderPass
    {
        private Material snnMaterial;
        private Settings settings;

        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tempRT;
        }

        public SNNPass(Shader shader, Settings s)
        {
            settings = s;
            if (shader != null)
                snnMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid() || snnMaterial == null) return;

            snnMaterial.SetInt("_Radius", settings.Radius);

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle tempRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_SNNTempRT", false);

            using (var builder = renderGraph.AddUnsafePass<PassData>("SNN", out var passData))
            {
                passData.material = snnMaterial;
                passData.source = source;
                passData.tempRT = tempRT;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(tempRT, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    Blitter.BlitCameraTexture(cmd, data.source, data.tempRT, data.material, 0);
                    Blitter.BlitCameraTexture(cmd, data.tempRT, data.source);
                });
            }
        }
    }

    public Settings settings = new Settings();
    SNNPass snnPass;

    public override void Create()
    {
        snnPass = null;
        if (settings.snnShader != null)
            snnPass = new SNNPass(settings.snnShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (snnPass != null)
            renderer.EnqueuePass(snnPass);
    }
}
