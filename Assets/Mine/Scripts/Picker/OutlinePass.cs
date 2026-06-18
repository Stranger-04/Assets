using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mine.Picker
{
    /// <summary>
    /// Outline Pass — Mask 绘制 + 全屏四邻采样描边合成。
    /// </summary>
    public class OutlinePass : ScriptableRenderPass
    {
        // ── Materials ───────────────────────────────────────────

        private readonly Material m_MaskMaterial;
        private readonly Material m_CompositeMaterial;

        // ── 选中物体 ────────────────────────────────────────────

        public int selectedObjectID { get; set; }

        // ── Debug ───────────────────────────────────────────────

        public bool debugShowMask { get; set; }

        // ── Output ──────────────────────────────────────────────

        public TextureHandle MaskHandle { get; private set; }

        // ════════════════════════════════════════════════════════

        public OutlinePass()
        {
            profilingSampler = new ProfilingSampler("Picker/Outline Mask");
            m_MaskMaterial      = CoreUtils.CreateEngineMaterial(Shader.Find("Mine/Picker/OutlineMask"));
            m_CompositeMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Mine/Picker/OutlineComposite"));
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_MaskMaterial == null || m_CompositeMaterial == null) return;
            if (selectedObjectID <= 0) return;

            var cameraData   = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var camDesc = cameraData.cameraTargetDescriptor;
            camDesc.depthBufferBits = 0;
            camDesc.msaaSamples     = 1;

            var targetRenderer = FindRendererByID(selectedObjectID);
            if (targetRenderer == null) return;

            // ── Mask RT ─────────────────────────────────────────

            var maskDesc = new RenderTextureDescriptor(camDesc.width, camDesc.height, RenderTextureFormat.R8, 0);
            MaskHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, maskDesc, "_OutlineMask", true);

            // ── Sub-pass 1: Mask 绘制 ───────────────────────────

            using (var builder = renderGraph.AddUnsafePass<MaskPassData>(
                "Picker/Outline Mask", out var md))
            {
                md.renderer     = targetRenderer;
                md.maskMaterial = m_MaskMaterial;

                builder.SetRenderAttachment(MaskHandle, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (MaskPassData d, UnsafeGraphContext ctx) =>
                {
                    CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd)
                        .DrawRenderer(d.renderer, d.maskMaterial, 0, 0);
                });
            }

            // ── Sub-pass 2: 描边合成 ────────────────────────────

            var cameraColorHandle = resourceData.activeColorTexture;
            var texelSize = new Vector2(1f / camDesc.width, 1f / camDesc.height);

            using (var builder = renderGraph.AddUnsafePass<CompositePassData>(
                "Picker/Outline Composite", out var cd))
            {
                cd.maskHandle        = MaskHandle;
                cd.cameraColorHandle = cameraColorHandle;
                cd.material          = m_CompositeMaterial;
                cd.debugShowMask     = debugShowMask;
                cd.texelSize         = texelSize;

                builder.UseTexture(MaskHandle,        AccessFlags.Read);
                builder.UseTexture(cameraColorHandle, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (CompositePassData d, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    if (d.debugShowMask)
                    {
                        Blitter.BlitCameraTexture(cmd, d.maskHandle, d.cameraColorHandle);
                    }
                    else
                    {
                        cmd.SetGlobalTexture("_OutlineMaskTex", d.maskHandle);
                        d.material.SetVector("_OutlineMaskTex_TexelSize",
                            new Vector4(d.texelSize.x, d.texelSize.y, 0, 0));
                        Blitter.BlitCameraTexture(cmd, d.cameraColorHandle,
                            d.cameraColorHandle, d.material, 0);
                    }
                });
            }
        }

        // ════════════════════════════════════════════════════════

        private static Renderer FindRendererByID(int id)
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in all)
            {
                if (r.sharedMaterial != null
                    && r.sharedMaterial.HasProperty("_ObjectID")
                    && r.sharedMaterial.GetInt("_ObjectID") == id)
                    return r;
            }
            return null;
        }

        public void Dispose()
        {
            if (m_MaskMaterial != null) Object.DestroyImmediate(m_MaskMaterial);
            if (m_CompositeMaterial != null) Object.DestroyImmediate(m_CompositeMaterial);
        }

        // ── PassData ────────────────────────────────────────────

        private class MaskPassData      { public Renderer renderer; public Material maskMaterial; }
        private class CompositePassData { public TextureHandle maskHandle, cameraColorHandle; public Material material; public bool debugShowMask; public Vector2 texelSize; }
    }
}
