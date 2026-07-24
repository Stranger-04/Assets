using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class KuwaharaFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader kuwaharaShader;
        [Range(1, 10)] public int Radius = 5;

        // 共用参数（Basic / Generalized / Aniso）
        [Range(1.0f, 18.0f)] public float sharpness = 8.0f;
        [Range(1.0f, 100.0f)] public float hardness = 8.0f;
        [Range(1.0f, 2000.0f)] public float weightScale = 1000.0f;
        // Aniso 专用
        [Range(0.01f, 2.0f)] public float alpha = 1.0f;
        [Range(0, 3)] public int downsampleLevel = 0;

        public enum KuwaharaType { Basic, Generalized, Anisotropic }
        public KuwaharaType kuwaharaType = KuwaharaType.Basic;
    }

    class KuwaharaPass : ScriptableRenderPass
    {
        private Material material;
        private Settings settings;

        public KuwaharaPass(Shader shader, Settings s)
        {
            settings = s;
            if (shader != null)
                material = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        // ── Basic / Generalized 共用数据 ──
        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tempRT;
            public int passIndex;
        }

        // ── Aniso 多 Pass 数据 ──
        class AnisoPassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tensorRT0;
            public TextureHandle tensorRT1;
            public TextureHandle tensorRT2;
            public TextureHandle outputRT;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            switch (settings.kuwaharaType)
            {
                case Settings.KuwaharaType.Anisotropic:
                    RecordAnisotropic(renderGraph, source, cameraData);
                    break;
                default:
                    RecordSinglePass(renderGraph, source, cameraData);
                    break;
            }
        }

        // ── Basic (Pass 0) / Generalized (Pass 1) ──
        void RecordSinglePass(RenderGraph renderGraph, TextureHandle source, UniversalCameraData cameraData)
        {
            if (material == null) return;

            int passIndex = settings.kuwaharaType == Settings.KuwaharaType.Basic ? 0 : 1;

            material.SetInt("_Radius", settings.Radius);
            // 单 Pass（Basic/Generalized）：Q 仅 Generalized 生效
            // Q=2 使方差线性衰减；WeightScale 控制敏感度
            material.SetFloat("_Q", 2.0f);
            material.SetFloat("_WeightScale", settings.weightScale);
            material.SetFloat("_Hardness", settings.hardness);

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            TextureHandle tempRT = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_KuwaharaTempRT", false);

            using (var builder = renderGraph.AddUnsafePass<PassData>("Kuwahara Pass", out var passData))
            {
                passData.material = material;
                passData.source = source;
                passData.tempRT = tempRT;
                passData.passIndex = passIndex;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(tempRT, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    Blitter.BlitCameraTexture(cmd, data.source, data.tempRT, data.material, data.passIndex);
                    Blitter.BlitCameraTexture(cmd, data.tempRT, data.source);
                });
            }
        }

        // ── Aniso multi‑pass pipeline (Pass 2→3→4→5) ──
        void RecordAnisotropic(RenderGraph renderGraph, TextureHandle source, UniversalCameraData cameraData)
        {
            if (material == null) return;

            material.SetFloat("_BlurScale", settings.downsampleLevel + 1.0f);
            material.SetInt("_Radius", settings.Radius);
            material.SetFloat("_Q", settings.sharpness);
            material.SetFloat("_Hardness", settings.hardness);
            material.SetFloat("_Alpha", settings.alpha);
            material.SetFloat("_WeightScale", settings.weightScale);

            RenderTextureDescriptor fullDesc = cameraData.cameraTargetDescriptor;
            fullDesc.depthBufferBits = 0;

            RenderTextureDescriptor lowDesc = fullDesc;
            int shift = Mathf.Clamp(settings.downsampleLevel, 0, 4);
            lowDesc.width = Mathf.Max(1, fullDesc.width >> shift);
            lowDesc.height = Mathf.Max(1, fullDesc.height >> shift);

            TextureHandle t0 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, fullDesc, "_TensorRT0", false);
            TextureHandle t1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowDesc, "_TensorRT1", false);
            TextureHandle t2 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowDesc, "_TensorRT2", false);
            TextureHandle outRT = UniversalRenderer.CreateRenderGraphTexture(renderGraph, fullDesc, "_KuwaharaOutput", false);

            using (var builder = renderGraph.AddUnsafePass<AnisoPassData>("KuwaharaAniso", out var passData))
            {
                passData.material = material;
                passData.source = source;
                passData.tensorRT0 = t0;
                passData.tensorRT1 = t1;
                passData.tensorRT2 = t2;
                passData.outputRT = outRT;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(t0, AccessFlags.Write);
                builder.UseTexture(t1, AccessFlags.Write);
                builder.UseTexture(t2, AccessFlags.ReadWrite);
                builder.UseTexture(outRT, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((AnisoPassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // Pass 2: Structure Tensor (ddx/ddy)
                    Blitter.BlitCameraTexture(cmd, data.source, data.tensorRT0, data.material, 2);
                    // Pass 3: Horizontal Blur
                    Blitter.BlitCameraTexture(cmd, data.tensorRT0, data.tensorRT1, data.material, 3);
                    // Pass 4: Vertical Blur + Eigen
                    Blitter.BlitCameraTexture(cmd, data.tensorRT1, data.tensorRT2, data.material, 4);

                    // Pass 5: Anisotropic Filter
                    cmd.SetGlobalTexture("_TFM", data.tensorRT2);
                    Blitter.BlitCameraTexture(cmd, data.source, data.outputRT, data.material, 5);
                    Blitter.BlitCameraTexture(cmd, data.outputRT, data.source);
                });
            }
        }
    }

    public Settings settings = new Settings();
    KuwaharaPass kuwaharaPass;

    public override void Create()
    {
        if (kuwaharaPass != null)
            kuwaharaPass = null;

        if (settings.kuwaharaShader == null) return;
        kuwaharaPass = new KuwaharaPass(settings.kuwaharaShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (kuwaharaPass == null) return;
        renderer.EnqueuePass(kuwaharaPass);
    }
}
