// ═══════════════════════════════════════════════════════════════
//  Unity 6 URP RenderGraph RenderPass 模板
//
//  基于 Unity 官方 ScriptableRenderPass + RenderGraph API
//  参考: com.unity.render-pipelines.universal/Runtime/Passes/
//
//  使用: 复制→改 ⚠️ → 在 Feature.AddRenderPasses() 注册
// ═══════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class ⚠️YourEffectPass : ScriptableRenderPass
{
    const string k_PassName = "⚠️YourEffect";
    Material m_Material;

    // ═══ PassData — RenderGraph 要求独立的 class ═══
    class PassData
    {
        public Material material;
        public TextureHandle source;
        // ⚠️ public TextureHandle extraTex;
    }

    public ⚠️YourEffectPass(Material material)
    {
        m_Material = material;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        // ⚠️ 可选: AfterRenderingOpaques, AfterRenderingSkybox,
        //         AfterRenderingPostProcessing, BeforeRenderingPostProcessing
    }

    // ═══ Unity 6 主入口 — RenderGraph（不用旧版 Execute）═══
    public override void RecordRenderGraph(RenderGraph renderGraph,
                                           ContextContainer frameData)
    {
        // 1. 获取 camera color
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle cameraColor = resourceData.activeColorTexture;

        // 2. 描述符（后处理不需要深度）
        RenderTextureDescriptor desc = resourceData.activeColorTextureDescriptor;
        desc.depthBufferBits = DepthBits.None;

        // 3. 添加 raster pass
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                   k_PassName, out var passData))
        {
            passData.material = m_Material;
            passData.source = cameraColor;

            // 声明纹理使用
            builder.UseTexture(cameraColor, AccessFlags.Read);
            builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
            // ⚠️ 如需额外纹理: builder.UseTexture(extraTex, AccessFlags.Read);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                // ⚠️ pass index 对应 shader 中的 Pass 顺序
                Blitter.BlitTexture(ctx.cmd, data.source,
                    Vector2.one, data.material, 0);
            });
        }
    }

    // ═══ 多 pass 模板（需创建临时 RT）═══
    // public override void RecordRenderGraph(...)
    // {
    //     var cameraColor = ...;
    //     TextureHandle tempRT = UniversalRenderer.CreateRenderGraphTexture(
    //         renderGraph, desc, "⚠️Temp", false);
    //
    //     // Pass 0: cameraColor → tempRT
    //     using (var builder = renderGraph.AddRasterRenderPass<PassData>(...))
    //     {
    //         ...builder.UseTexture(cameraColor, AccessFlags.Read);
    //         ...builder.SetRenderAttachment(tempRT, 0, AccessFlags.Write);
    //         ...Blitter.BlitTexture(ctx.cmd, source, Vector2.one, material, 0);
    //     }
    //
    //     // Pass 1: tempRT → cameraColor
    //     using (var builder = renderGraph.AddRasterRenderPass<PassData>(...))
    //     {
    //         ...builder.UseTexture(tempRT, AccessFlags.Read);
    //         ...builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
    //         ...Blitter.BlitTexture(ctx.cmd, source, Vector2.one, material, 1);
    //     }
    // }
}
