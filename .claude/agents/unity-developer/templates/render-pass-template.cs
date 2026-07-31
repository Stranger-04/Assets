// ═══════════════════════════════════════════════════════════════
//  Unity 6 URP RenderGraph Pass 模板
//
//  使用方式：
//    1. 复制此类，改名为 YourEffectPass
//    2. 替换所有 ⚠️ 标记处
//    3. 在 ScriptableRendererFeature 中注册此 Pass
// ═══════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class PostProcessTemplatePass : ScriptableRenderPass
{
    const string k_PassName = "PostProcessTemplate"; // ⚠️ 替换
    Material m_Material;

    // ═══ PassData — RenderGraph 要求单独的 class ═══
    class PassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle output;
        // ⚠️ 添加你的额外参数
    }

    public PostProcessTemplatePass(Material material)
    {
        m_Material = material;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents; // ⚠️ 按需调整
        // 可选时机: AfterRenderingOpaques, AfterRenderingSkybox, AfterRenderingPostProcessing
    }

    // ⚠️ Unity 6 主入口 — 不使用 Execute()
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // 1. 获取资源
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle cameraColor = resourceData.activeColorTexture;

        // 2. 创建临时目标 (如果只需要一次 pass 则不需要)
        RenderTextureDescriptor desc = resourceData.activeColorTextureDescriptor;
        desc.depthBufferBits = DepthBits.None;
        TextureHandle tempTarget = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, desc, "Template_Temp", false); // ⚠️ 替换名称

        // 3. 添加 Raster Pass
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
        {
            passData.material = m_Material;
            passData.source = cameraColor;
            passData.output = tempTarget;

            builder.UseTexture(cameraColor, AccessFlags.Read);
            builder.SetRenderAttachment(tempTarget, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, Vector2.one, data.material, 0);
            });
        }

        // 4. 如果只需要一次 pass，直接写回 cameraColor，跳过这一步
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName + "_Final", out var passData))
        {
            passData.material = m_Material;
            passData.source = tempTarget;
            passData.output = cameraColor;

            builder.UseTexture(tempTarget, AccessFlags.Read);
            builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, Vector2.one, data.material, 1);
            });
        }
    }
}
