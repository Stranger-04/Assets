
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            [Range(0f, 0.1f)] public float Thickness = 0.01f;
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
        private RTHandle ssoDiffRT;
        private RTHandle tempMainRT;

        public void ReleaseRT()
        {
            ssoDiffRT?.Release();
            tempMainRT?.Release();

            ssoDiffRT = null;
            tempMainRT = null;
        }

        public SSORenderPass(Shader shader, Settings s)
        {
            settings = s;
            ssoMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques; 
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (ssoMaterial == null) return;
            var cmd = CommandBufferPool.Get("SSO");
            var cameraData = renderingData.cameraData;

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
            if (settings.ssoType == Settings.SSOType.Basic)
            {
                ssoMaterial.EnableKeyword("SSO_Basic");
            }
            else if (settings.ssoType == Settings.SSOType.Sobel)
            {
                ssoMaterial.EnableKeyword("SSO_Sobel");
            }
            else if (settings.ssoType == Settings.SSOType.DDXY)
            {
                ssoMaterial.EnableKeyword("SSO_DDXY");
            }

            ssoMaterial.DisableKeyword("SSO_SHADOW_NONE");
            ssoMaterial.DisableKeyword("SSO_SHADOW_HARD");
            ssoMaterial.DisableKeyword("SSO_SHADOW_SOFT");
            if (settings.shadowParams.shadowType == Settings.ShadowType.Hard)
            {
                ssoMaterial.EnableKeyword("SSO_SHADOW_HARD");
            }
            else if (settings.shadowParams.shadowType == Settings.ShadowType.Soft)
            {
                ssoMaterial.EnableKeyword("SSO_SHADOW_SOFT");
            }
            else if (settings.shadowParams.shadowType == Settings.ShadowType.None)
            {
                ssoMaterial.EnableKeyword("SSO_SHADOW_NONE");
            }
            
            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var renderer = renderingData.cameraData.renderer;
            var source = renderer.cameraColorTarget;
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(ref tempMainRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSOTempMainRT");
            ssoMaterial.SetTexture("_MainTex", tempMainRT);
            desc.width  >>= settings.downsample;
            desc.height >>= settings.downsample;
            RenderingUtils.ReAllocateIfNeeded(ref ssoDiffRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSODiffRT");
            ssoMaterial.SetTexture("_SSODiffTex", ssoDiffRT);

            cmd.Blit(source, tempMainRT);
            cmd.Blit(null, ssoDiffRT, ssoMaterial, 0);
            cmd.Blit(tempMainRT, renderer.cameraColorTargetHandle.nameID, ssoMaterial, 1);

            if (settings.SSOFeature)
            {    
                cmd.Blit(ssoDiffRT, renderer.cameraColorTargetHandle.nameID);
            }
        }
    }

    public Settings settings = new Settings();
    SSORenderPass ssoPass;
    
    public override void Create()
    {

        if (ssoPass != null)
        {
            ssoPass.ReleaseRT();
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