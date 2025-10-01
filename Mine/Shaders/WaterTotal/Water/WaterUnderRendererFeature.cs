using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WaterUnderRendererFeature : ScriptableRendererFeature
{
    class RenderWaterUnderPass : ScriptableRenderPass
    {
        private LayerMask layerMask;
        private RenderTargetHandle waterUnderRT;
        private ShaderTagId shaderTagId = new ShaderTagId("UniversalForward");
        private string profilerTag = "RenderWaterUnder";

        public RenderWaterUnderPass(LayerMask layerMask)
        {
            this.layerMask = layerMask;
            waterUnderRT.Init("_WaterUnderTex");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            // 启用深度缓冲，确保本 Pass 中执行正确的深度测试
            descriptor.depthBufferBits = 24;
            cmd.GetTemporaryRT(waterUnderRT.id, descriptor, FilterMode.Bilinear);
            ConfigureTarget(waterUnderRT.Identifier(), waterUnderRT.Identifier());
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(profilerTag);
            using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 先渲染不透明（前向深度优先），再渲染透明（背向-前向排序）
                var sortFlagsOpaque = SortingCriteria.CommonOpaque;
                var drawSettingsOpaque = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlagsOpaque);
                var opaqueFilter = new FilteringSettings(RenderQueueRange.opaque, layerMask);
                context.DrawRenderers(renderingData.cullResults, ref drawSettingsOpaque, ref opaqueFilter);

                var sortFlagsTransparent = SortingCriteria.CommonTransparent;
                var drawSettingsTransparent = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlagsTransparent);
                var transparentFilter = new FilteringSettings(RenderQueueRange.transparent, layerMask);
                context.DrawRenderers(renderingData.cullResults, ref drawSettingsTransparent, ref transparentFilter);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) return;
            cmd.ReleaseTemporaryRT(waterUnderRT.id);
        }
    }

    [System.Serializable]
    public class Settings
    {
        public LayerMask underWaterLayer = 0; // 在 Inspector 中指定 UnderWater 层
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    public Settings settings = new Settings();
    RenderWaterUnderPass renderPass;

    public override void Create()
    {
        renderPass = new RenderWaterUnderPass(settings.underWaterLayer)
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderPass != null)
            renderer.EnqueuePass(renderPass);
    }
}
