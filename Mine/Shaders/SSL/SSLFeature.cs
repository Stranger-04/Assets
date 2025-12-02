using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SSLFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader sslShader;
        [Range(1, 256)] public int   maxSteps = 32;
        [Range(0.1f, 100f)] public float maxDistance = 10f;
        [Range(0f, 5f)] public float intensity = 1f;
        [Range(0f, 1f)] public float jitterScale = 0.5f;
        [Range(0f, 1f)] public float blurScale = 0.5f;
        public int blurLevels = 1;
        public int blurIterations = 1;
        public bool SSLFeature = true;
    }

    class SSLRenderPass : ScriptableRenderPass
    {
        private Material sslMaterial;
        private Settings settings;
        private RenderTargetHandle sslRT;
        private RenderTargetHandle sslBlurRT1;
        private RenderTargetHandle sslBlurRT2;
        private RenderTargetHandle tempMainRT;
        public SSLRenderPass(Shader shader, Settings s)
        {
            settings = s;
            sslRT.Init("_SSLResultRT"); 
            sslBlurRT1.Init("_SSLBlurRT1");
            sslBlurRT2.Init("_SSLBlurRT2");
            tempMainRT.Init("_TempMainRT");
            sslMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var desc = cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;

            cmd.GetTemporaryRT(sslRT.id, desc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(sslBlurRT1.id, desc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(sslBlurRT2.id, desc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(tempMainRT.id, desc, FilterMode.Bilinear);

            ConfigureTarget(sslRT.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (sslMaterial == null) return;

            var cmd = CommandBufferPool.Get("SSL");

            sslMaterial.SetInt("_MaxSteps", settings.maxSteps);
            sslMaterial.SetFloat("_MaxDistance", settings.maxDistance);
            sslMaterial.SetFloat("_Intensity", settings.intensity);
            sslMaterial.SetFloat("_BlurScale", settings.blurScale);
            sslMaterial.SetFloat("_JitterScale", settings.jitterScale);
            cmd.SetGlobalTexture("_MainTex", renderingData.cameraData.renderer.cameraColorTargetHandle);

            var renderer = renderingData.cameraData.renderer;

            cmd.Blit(renderer.cameraColorTargetHandle.nameID, tempMainRT.id);
            cmd.Blit(null, sslRT.Identifier(), sslMaterial, 0);
            cmd.Blit(sslRT.Identifier(), sslBlurRT1.Identifier(), sslMaterial, 1);
            cmd.Blit(sslBlurRT1.Identifier(), sslBlurRT2.Identifier(), sslMaterial, 2);


            cmd.SetGlobalTexture("_SSLResultTex", sslRT.id);
            cmd.Blit(tempMainRT.id, renderer.cameraColorTargetHandle.nameID, sslMaterial, 3);

            if (settings.SSLFeature)
            {
                cmd.Blit(sslRT.Identifier(), renderer.cameraColorTargetHandle.nameID);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) return;
            cmd.ReleaseTemporaryRT(sslRT.id);
            cmd.ReleaseTemporaryRT(sslBlurRT1.id);
            cmd.ReleaseTemporaryRT(sslBlurRT2.id);
            cmd.ReleaseTemporaryRT(tempMainRT.id);
        }
    }

    public Settings settings = new Settings();
    SSLRenderPass sslPass;

    public override void Create()
    {
        if (settings.sslShader != null)
        {
            sslPass = new SSLRenderPass(settings.sslShader, settings);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (sslPass != null)
        {
            renderer.EnqueuePass(sslPass);
        }
    }
}