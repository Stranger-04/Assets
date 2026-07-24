using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SSCFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader sscShader;
        public enum NoiseTex
        {
            TEX2DR,
            TEX2DRG,
            TEX3DXYZ
        }
        public NoiseTex noiseTex = NoiseTex.TEX2DR;

        public enum RayCount
        {
            COUNT64,
            COUNT128,
            COUNT256
        }
        public RayCount rayCount = RayCount.COUNT128;

        public Color baseColorA = Color.white;
        public Color baseColorB = Color.black;
        public Texture2D mainTex2D;
        public Texture3D mainTex3D;

        [Range(0f, 100.0f)] public float scale = 1.0f;
        [Range(0f, 100.0f)] public float speed = 1.0f;
        [Range(0f, 100.0f)] public float height = 100.0f;
        [Range(0f, 360.0f)] public float rotation = 0.0f;
        [Range(0f, 1.000f)] public float thickness = 0.5f;
        public Vector3 size = new Vector3(100, 100, 10);

        [Range(0f, 1f)] public float jitter = 1.0f;
        [Range(0 , 4 )] public int downsample = 0;
        public bool SSCFeature = true;

        internal static readonly int BaseColorAID = Shader.PropertyToID("_BaseColorA");
        internal static readonly int BaseColorBID = Shader.PropertyToID("_BaseColorB");
        internal static readonly int MainTex2DID = Shader.PropertyToID("_MainTex2D");
        internal static readonly int MainTex3DID = Shader.PropertyToID("_MainTex3D");
        internal static readonly int ParamAID = Shader.PropertyToID("_CloudParamA");
        internal static readonly int ParamBID = Shader.PropertyToID("_CloudParamB");
        internal static readonly int JitterID = Shader.PropertyToID("_Jitter");
    }

    class SSCRenderPass : ScriptableRenderPass
    {
        private Material sscMaterial;
        private Settings settings;
        private RTHandle sscRT;
        private RTHandle tempMainRT;

        public void ReleaseRT()
        {
            sscRT?.Release();
            tempMainRT?.Release();

            sscRT = null;
            tempMainRT = null;
        }

        public SSCRenderPass(Shader shader, Settings s)
        {
            settings = s;
            sscMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (sscMaterial == null) return;
            var cmd = CommandBufferPool.Get("SSC");
            var cameraData = renderingData.cameraData;

            sscMaterial.SetColor(Settings.BaseColorAID, settings.baseColorA);
            sscMaterial.SetColor(Settings.BaseColorBID, settings.baseColorB);
            sscMaterial.SetTexture(Settings.MainTex2DID, settings.mainTex2D);
            sscMaterial.SetTexture(Settings.MainTex3DID, settings.mainTex3D);
            sscMaterial.SetVector(Settings.ParamAID, new Vector4(settings.scale, settings.speed, settings.rotation, settings.height));
            sscMaterial.SetVector(Settings.ParamBID, new Vector4(settings.size.x, settings.size.y, settings.size.z, settings.thickness));
            sscMaterial.SetFloat(Settings.JitterID, settings.jitter);

            sscMaterial.DisableKeyword("SSC_NOISE_TEX2DR");
            sscMaterial.DisableKeyword("SSC_NOISE_TEX2DRG");
            sscMaterial.DisableKeyword("SSC_NOISE_TEX3DXYZ");
            sscMaterial.DisableKeyword("SSC_RAY_COUNT_64");
            sscMaterial.DisableKeyword("SSC_RAY_COUNT_128");
            sscMaterial.DisableKeyword("SSC_RAY_COUNT_256");
            
            if (settings.noiseTex == Settings.NoiseTex.TEX2DR)
            {
                sscMaterial.EnableKeyword("SSC_NOISE_TEX2DR");
            }
            else if (settings.noiseTex == Settings.NoiseTex.TEX2DRG)
            {
                sscMaterial.EnableKeyword("SSC_NOISE_TEX2DRG");
            }
            else if (settings.noiseTex == Settings.NoiseTex.TEX3DXYZ)
            {
                sscMaterial.EnableKeyword("SSC_NOISE_TEX3DXYZ");
            }

            if (settings.rayCount == Settings.RayCount.COUNT64)
            {
                sscMaterial.EnableKeyword("SSC_RAY_COUNT_64");
            }
            else if (settings.rayCount == Settings.RayCount.COUNT128)
            {
                sscMaterial.EnableKeyword("SSC_RAY_COUNT_128");
            }
            else if (settings.rayCount == Settings.RayCount.COUNT256)
            {
                sscMaterial.EnableKeyword("SSC_RAY_COUNT_256");
            }

            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var renderer = renderingData.cameraData.renderer;
            var source = renderer.cameraColorTargetHandle;
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateHandleIfNeeded(ref tempMainRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSCTempMainRT");
            sscMaterial.SetTexture("_MainTex", tempMainRT);
            desc.width  >>= settings.downsample;
            desc.height >>= settings.downsample;
            RenderingUtils.ReAllocateHandleIfNeeded(ref sscRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSCResultRT");
            sscMaterial.SetTexture("_SSCTex", sscRT);

            cmd.Blit(source, tempMainRT);
            cmd.Blit(null, sscRT, sscMaterial, 0);
            cmd.Blit(tempMainRT, renderer.cameraColorTargetHandle.nameID, sscMaterial, 1);

            if (settings.SSCFeature)
            {    
                cmd.Blit(sscRT, renderer.cameraColorTargetHandle.nameID);
            }
        }
    }

    public Settings settings = new Settings();
    SSCRenderPass sscPass;

    public override void Create()
    {

        if (sscPass != null)
        {
            sscPass.ReleaseRT();
            sscPass = null;
        }

        if (settings.sscShader != null)
        {
            sscPass = new SSCRenderPass(settings.sscShader, settings);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (sscPass != null)
        {
            renderer.EnqueuePass(sscPass);
        }
    }
}