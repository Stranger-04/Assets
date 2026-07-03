using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// PCSS 后处理 Feature — 生成 _PCSS_SoftShadow 全局纹理。
/// 标准后处理模式，参考 SSSMFeature。
/// </summary>
public class PCSSFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Resources")]
        public Shader pcssShader;

        [Header("Light")]
        [Range(0.1f, 10f)] public float lightSize = 1.0f;

        [Header("Blocker Search")]
        [Range(4, 64)]  public int blockerSamples     = 16;
        [Range(1, 32)]  public int blockerSearchRadius = 8;

        [Header("PCF")]
        [Range(4, 64)]      public int   pcfSamples = 16;
        [Range(0.1f, 2f)]   public float softness   = 1.0f;

        [Header("Debug")]
        public bool showShadow = true; // ON=显示 RT 到屏幕

        internal static readonly int LightSizeID      = Shader.PropertyToID("_PCSS_LightSize");
        internal static readonly int BlockerSamplesID = Shader.PropertyToID("_PCSS_BlockerSamples");
        internal static readonly int BlockerRadiusID  = Shader.PropertyToID("_PCSS_BlockerRadius");
        internal static readonly int PCFSamplesID     = Shader.PropertyToID("_PCSS_PCFSamples");
        internal static readonly int SoftnessID       = Shader.PropertyToID("_PCSS_Softness");
        internal static readonly int ShadowTexID      = Shader.PropertyToID("_PCSS_SoftShadow");
    }

    class PCSSPass : ScriptableRenderPass
    {
        Settings  m_Settings;
        Material  m_Material;

        class PassData
        {
            public Material      material;
            public TextureHandle source;
            public TextureHandle shadowRT;
            public bool          showShadow;
        }

        public PCSSPass(Settings settings, Material mat)
        {
            m_Settings = settings;
            m_Material = mat;
            profilingSampler = new ProfilingSampler("PCSS");
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData   cameraData   = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            // ── 材质参数 ──
            m_Material.SetFloat(Settings.LightSizeID,     m_Settings.lightSize);
            m_Material.SetInt(Settings.BlockerSamplesID,   m_Settings.blockerSamples);
            m_Material.SetInt(Settings.BlockerRadiusID,    m_Settings.blockerSearchRadius);
            m_Material.SetInt(Settings.PCFSamplesID,        m_Settings.pcfSamples);
            m_Material.SetFloat(Settings.SoftnessID,        m_Settings.softness);

            // ── 创建输出 RT ──
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            desc.msaaSamples = 1;

            TextureHandle shadowRT = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_PCSS_SoftShadow", false);

            using (var builder = renderGraph.AddUnsafePass<PassData>("PCSS", out var passData, profilingSampler))
            {
                passData.material    = m_Material;
                passData.source     = source;
                passData.shadowRT   = shadowRT;
                passData.showShadow = m_Settings.showShadow;

                builder.UseTexture(source,   AccessFlags.ReadWrite);
                builder.UseTexture(shadowRT, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // ── Pass 0: PCSS 生成 → Shadow RT ──
                    Blitter.BlitCameraTexture(cmd, data.source, data.shadowRT,
                        data.material, PCSSFeature.s_DebugPass);

                    // ── 暴露为全局纹理 ──
                    cmd.SetGlobalTexture(Settings.ShadowTexID, data.shadowRT);

                    // ── Debug：显示到屏幕 ──
                    if (data.showShadow)
                        Blitter.BlitCameraTexture(cmd, data.shadowRT, data.source);
                });
            }
        }
    }

    public Settings settings = new();
    public static int s_DebugPass = 0; // 0=PCSS, 1=Depth, 2=HardShadow, 3=ShadowUV

    private PCSSPass m_PCSSPass;
    private Material m_Material;

    public override void Create()
    {
        if (settings.pcssShader != null)
            m_Material = CoreUtils.CreateEngineMaterial(settings.pcssShader);
        m_PCSSPass = new PCSSPass(settings, m_Material);
        m_PCSSPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Material == null) return;
        renderer.EnqueuePass(m_PCSSPass);
    }

    protected override void Dispose(bool disposing)
    {
        m_PCSSPass = null;
        CoreUtils.Destroy(m_Material);
    }
}
