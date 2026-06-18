using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mine.Picker
{
    /// <summary>
    /// GPU Picker MRT Pass — 1 次绘制输出 ObjectID(ARGB32) / Depth / Normal。
    /// ObjectID 直写持久化 RT，免拷贝，供 CPU Readback。
    /// </summary>
    public class PickerPass : ScriptableRenderPass
    {
        public enum DebugView { Off, ObjectID, Depth, Normal }

        // ── Debug ───────────────────────────────────────────────

        public DebugView debugView { get; set; } = DebugView.Off;

        // ── Persistent RT ───────────────────────────────────────

        private RenderTexture m_ObjIDRT;
        private int           m_ObjIDWidth, m_ObjIDHeight;

        public RenderTexture ObjIDRenderTexture => m_ObjIDRT;

        // ── Outputs ─────────────────────────────────────────────

        public TextureHandle ObjIDHandle  { get; private set; }
        public TextureHandle DepthHandle  { get; private set; }
        public TextureHandle NormalHandle { get; private set; }

        // ════════════════════════════════════════════════════════

        public PickerPass()
        {
            profilingSampler = new ProfilingSampler("Picker/Picker MRT");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData   = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var camDesc = cameraData.cameraTargetDescriptor;
            camDesc.depthBufferBits = 0;
            camDesc.msaaSamples     = 1;

            // ── ObjectID 持久化 RT ──────────────────────────────

            EnsureObjIDRT(camDesc.width, camDesc.height);
            var objIDHandle = renderGraph.ImportTexture(RTHandles.Alloc(m_ObjIDRT));

            DepthHandle  = UniversalRenderer.CreateRenderGraphTexture(renderGraph, camDesc, "_PickerDepth",  true);
            NormalHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, camDesc, "_PickerNormal", true);
            ObjIDHandle  = objIDHandle;

            // ── MRT 绘制 ────────────────────────────────────────

            var renderers = FindAllPickerRenderers();

            using (var builder = renderGraph.AddUnsafePass<DrawPassData>(
                "Picker/Picker MRT", out var drawData))
            {
                drawData.renderers = renderers;
                builder.SetRenderAttachment(objIDHandle,   0, AccessFlags.Write);
                builder.SetRenderAttachment(DepthHandle,   1, AccessFlags.Write);
                builder.SetRenderAttachment(NormalHandle,  2, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (DrawPassData d, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    // 持久化 RT 不会自动清除，手动清为 0（背景 = 无选中）
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    foreach (var r in d.renderers)
                    {
                        if (r.sharedMaterial != null)
                            cmd.DrawRenderer(r, r.sharedMaterial, 0, 0);
                    }
                });
            }

            // ── Debug 视图 ──────────────────────────────────────

            if (debugView != DebugView.Off)
            {
                var debugTex = debugView switch
                {
                    DebugView.Depth  => DepthHandle,
                    DebugView.Normal => NormalHandle,
                    _                => objIDHandle,
                };

                using (var builder = renderGraph.AddUnsafePass<DebugBlitData>(
                    "Picker/Picker Debug", out var d))
                {
                    d.source = debugTex;
                    d.dest   = resourceData.activeColorTexture;
                    builder.UseTexture(debugTex, AccessFlags.Read);
                    builder.UseTexture(d.dest, AccessFlags.Write);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (DebugBlitData bd, UnsafeGraphContext ctx) =>
                    {
                        Blitter.BlitCameraTexture(
                            CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd),
                            bd.source, bd.dest);
                    });
                }
            }
        }

        // ════════════════════════════════════════════════════════

        private static Renderer[] FindAllPickerRenderers()
        {
            var shader = Shader.Find("Mine/Picker/Picker");
            if (shader == null) return System.Array.Empty<Renderer>();

            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in all)
                if (r.sharedMaterial != null && r.sharedMaterial.shader == shader)
                    list.Add(r);
            return list.ToArray();
        }

        private void EnsureObjIDRT(int w, int h)
        {
            if (m_ObjIDRT != null && m_ObjIDWidth == w && m_ObjIDHeight == h) return;
            if (m_ObjIDRT != null) { m_ObjIDRT.Release(); Object.DestroyImmediate(m_ObjIDRT); }

            m_ObjIDWidth = w; m_ObjIDHeight = h;
            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = false };
            m_ObjIDRT = new RenderTexture(desc) { name = "_PickerObjID", filterMode = FilterMode.Point };
            m_ObjIDRT.Create();
        }

        public void Dispose()
        {
            if (m_ObjIDRT != null) { m_ObjIDRT.Release(); Object.DestroyImmediate(m_ObjIDRT); m_ObjIDRT = null; }
        }

        // ── PassData ────────────────────────────────────────────

        private class DrawPassData  { public Renderer[] renderers; }
        private class DebugBlitData { public TextureHandle source, dest; }
    }
}
