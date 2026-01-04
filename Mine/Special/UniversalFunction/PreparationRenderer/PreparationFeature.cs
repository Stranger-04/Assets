using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PreparationFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader PreparationShader;
        public bool useDepthTexture = true;
        public RenderTexture depthTexture;
        public bool useNormalTexture = false;
        public RenderTexture normalTexture;
        public bool useColorTexture = false;
        public RenderTexture colorTexture;
    }

    class PreparationPass : ScriptableRenderPass
    {
        private Settings settings;
        private Material preparationMaterial;
        private RenderTargetHandle depthTexture;
        private RenderTargetHandle normalTexture;
        private RenderTargetHandle colorTexture;

        public PreparationPass(Settings s)
        {
            settings = s;
            preparationMaterial = CoreUtils.CreateEngineMaterial(settings.PreparationShader);
            if (settings.useDepthTexture)
                depthTexture.Init("_SceneDepthTex");
            if (settings.useNormalTexture)
                normalTexture.Init("_SceneNormalTex");
            if (settings.useColorTexture)
                colorTexture.Init("_SceneColorTex");
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Preparation");
            var renderer = renderingData.cameraData.renderer;
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            
            if (settings.useDepthTexture)
            {
                if (settings.depthTexture == null)
                {
                    cmd.GetTemporaryRT(depthTexture.id, desc.width, desc.height, desc.depthBufferBits, FilterMode.Point);
                    cmd.Blit(null, depthTexture.id, preparationMaterial, 0);
                    cmd.SetGlobalTexture("_SceneDepthTex", depthTexture.id);
                    cmd.ReleaseTemporaryRT(depthTexture.id);
                }
                else
                {
                    cmd.Blit(null, settings.depthTexture, preparationMaterial, 0);
                }
            }

            if (settings.useColorTexture)
            {
                if (settings.colorTexture == null)
                {
                    cmd.GetTemporaryRT(colorTexture.id, desc.width, desc.height, 0, FilterMode.Bilinear);
                    cmd.Blit(null, colorTexture.id, preparationMaterial, 1);
                    cmd.SetGlobalTexture("_SceneColorTex", colorTexture.id);
                    cmd.ReleaseTemporaryRT(colorTexture.id);
                }
                else
                {
                    cmd.Blit(null, settings.colorTexture, preparationMaterial, 1);
                }
            }

            if (settings.useNormalTexture)
            {
                if (settings.normalTexture == null)
                {
                    cmd.GetTemporaryRT(normalTexture.id, desc.width, desc.height, 0, FilterMode.Bilinear);
                    cmd.Blit(null, normalTexture.id, preparationMaterial, 2);
                    cmd.SetGlobalTexture("_SceneNormalTex", normalTexture.id);
                    cmd.ReleaseTemporaryRT(normalTexture.id);
                }
                else
                {
                    cmd.Blit(null, settings.normalTexture, preparationMaterial, 2);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public Settings settings = new Settings();
    private PreparationPass preparationPass;

    public override void Create()
    {
        preparationPass = new PreparationPass(settings);
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(preparationPass);
    }
}