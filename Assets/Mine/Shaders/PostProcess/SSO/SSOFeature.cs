
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SSOFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ssoShader;
        public enum SSOType
        {
            DDXY,
            Basic,
            Sobel
        }
        public SSOType ssoType = SSOType.Basic;

        [System.Serializable]
        public class OutlineParams
        {        
            [Range(0f, 1f)] public float intensity = 1.0f;
            [Range(0f, 5f)] public float thickness = 1;
            public Vector2 threshold = new Vector2(0.1f, 0.2f);
        }

        public OutlineParams depthParams  = new OutlineParams();
        public OutlineParams normalParams = new OutlineParams();

        public enum ShadowType
        {
            None,
            Hard,
            Soft
        }

        [System.Serializable]
        public class ShadowParams
        {
            public ShadowType shadowType = ShadowType.Soft;
            [Range(0f, 1f)] public float Intensity = 0.5f;
            [Range(0f, 1f)] public float Sharpness = 1f;
            [Range(0f, 1f)] public float Thickness = 0.1f;
            [Range(0f, 1f)] public float Density = 0.5f;
        }
        public ShadowParams shadowParams = new ShadowParams();

        [Range(0f, 1f)] public float jitter = 1.0f;
        [Range(0 , 4 )] public int downsample = 0;
        public Color OutlineColor = Color.white;
        public bool SSOFeature = true;


        internal static readonly int DepthIntensityID = Shader.PropertyToID("_DepthIntensity");
        internal static readonly int DepthThicknessID = Shader.PropertyToID("_DepthThickness");
        internal static readonly int DepthThresholdID = Shader.PropertyToID("_DepthThreshold");

        internal static readonly int NormalIntensityID = Shader.PropertyToID("_NormalIntensity");
        internal static readonly int NormalThicknessID = Shader.PropertyToID("_NormalThickness");
        internal static readonly int NormalThresholdID = Shader.PropertyToID("_NormalThreshold");

        internal static readonly int ShadowIntensityID = Shader.PropertyToID("_ShadowIntensity");
        internal static readonly int ShadowSharpnessID = Shader.PropertyToID("_ShadowSharpness");
        internal static readonly int ShadowThicknessID = Shader.PropertyToID("_ShadowThickness");
        internal static readonly int ShadowDensityID = Shader.PropertyToID("_ShadowDensity");

        internal static readonly int JitterID = Shader.PropertyToID("_Jitter");
        internal static readonly int DownsampleID = Shader.PropertyToID("_Downsample");
        internal static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    }

    class SSORenderPass : ScriptableRenderPass
    {
        private Material ssoMaterial;
        private Settings settings;

        public SSORenderPass(Shader shader, Settings s)
        {
            settings = s;
            ssoMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle tempMain;
            public TextureHandle diff;
            public bool writeToSource;
        }

        // RenderGraph implementation for URP 17+ (Unity 6.0+)
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (ssoMaterial == null) return;

            ssoMaterial.SetFloat(Settings.DepthIntensityID, settings.depthParams.intensity);
            ssoMaterial.SetFloat(Settings.DepthThicknessID, settings.depthParams.thickness);
            ssoMaterial.SetVector(Settings.DepthThresholdID, settings.depthParams.threshold);

            ssoMaterial.SetFloat(Settings.NormalIntensityID, settings.normalParams.intensity);
            ssoMaterial.SetFloat(Settings.NormalThicknessID, settings.normalParams.thickness);
            ssoMaterial.SetVector(Settings.NormalThresholdID, settings.normalParams.threshold);

            ssoMaterial.SetFloat(Settings.ShadowIntensityID, settings.shadowParams.Intensity);
            ssoMaterial.SetFloat(Settings.ShadowSharpnessID, settings.shadowParams.Sharpness);
            ssoMaterial.SetFloat(Settings.ShadowThicknessID, settings.shadowParams.Thickness);
            ssoMaterial.SetFloat(Settings.ShadowDensityID, settings.shadowParams.Density);
            
            ssoMaterial.SetFloat(Settings.JitterID, settings.jitter);
            ssoMaterial.SetFloat(Settings.DownsampleID, settings.downsample);
            ssoMaterial.SetColor(Settings.OutlineColorID, settings.OutlineColor);

            ssoMaterial.DisableKeyword("SSO_DDXY");
            ssoMaterial.DisableKeyword("SSO_Basic");
            ssoMaterial.DisableKeyword("SSO_Sobel");
            if (settings.ssoType == Settings.SSOType.Basic) ssoMaterial.EnableKeyword("SSO_Basic");
            else if (settings.ssoType == Settings.SSOType.Sobel) ssoMaterial.EnableKeyword("SSO_Sobel");
            else if (settings.ssoType == Settings.SSOType.DDXY) ssoMaterial.EnableKeyword("SSO_DDXY");

            ssoMaterial.DisableKeyword("SSO_SHADOW_NONE");
            ssoMaterial.DisableKeyword("SSO_SHADOW_HARD");
            ssoMaterial.DisableKeyword("SSO_SHADOW_SOFT");
            if (settings.shadowParams.shadowType == Settings.ShadowType.Hard) ssoMaterial.EnableKeyword("SSO_SHADOW_HARD");
            else if (settings.shadowParams.shadowType == Settings.ShadowType.Soft) ssoMaterial.EnableKeyword("SSO_SHADOW_SOFT");
            else if (settings.shadowParams.shadowType == Settings.ShadowType.None) ssoMaterial.EnableKeyword("SSO_SHADOW_NONE");

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            TextureHandle tempMain = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSOTempMainRT", false);
            
            desc.width >>= settings.downsample;
            desc.height >>= settings.downsample;
            TextureHandle diff = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSODiffRT", false);

            using (var builder = renderGraph.AddUnsafePass<PassData>("SSO RenderGraph Pass", out var passData))
            {
                passData.material = ssoMaterial;
                passData.source = source;
                passData.tempMain = tempMain;
                passData.diff = diff;
                passData.writeToSource = settings.SSOFeature;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(tempMain, AccessFlags.ReadWrite);
                builder.UseTexture(diff, AccessFlags.ReadWrite);

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    cmd.SetGlobalTexture("_MainTex", data.tempMain);
                    cmd.SetGlobalTexture("_SSOTex", data.diff);

                    Blitter.BlitCameraTexture(cmd, data.source, data.tempMain);
                    Blitter.BlitCameraTexture(cmd, data.tempMain, data.diff, data.material, 0);
                    Blitter.BlitCameraTexture(cmd, data.tempMain, data.source, data.material, 1);

                    if (data.writeToSource)
                    {
                        Blitter.BlitCameraTexture(cmd, data.diff, data.source);
                    }
                });
            }
        }
    }

    public Settings settings = new Settings();
    SSORenderPass ssoPass;
    
    public override void Create()
    {
        if (ssoPass != null)
        {
            // Resource release is now handled intrinsically by RenderGraph's TextureHandles
            ssoPass = null;
        }

        if (settings.ssoShader != null)
        {
            ssoPass = new SSORenderPass(settings.ssoShader, settings);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ssoPass != null)
        {
            renderer.EnqueuePass(ssoPass);
        }
    }
}