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
        private RTHandle depthTexture;
        private RTHandle normalTexture;
        private RTHandle colorTexture;

        public void ReleaseRT()
        {
            depthTexture?.Release();
            normalTexture?.Release();
            colorTexture?.Release();

            depthTexture = null;
            normalTexture = null;
            colorTexture = null;
        }

        public PreparationPass(Settings s)
        {
            settings = s;
            preparationMaterial = CoreUtils.CreateEngineMaterial(settings.PreparationShader);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Preparation");
            var renderer = renderingData.cameraData.renderer;
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            
            if (settings.useDepthTexture)
            {
                if (settings.depthTexture == null)
                {
                    RenderingUtils.ReAllocateHandleIfNeeded(ref depthTexture, new RenderTextureDescriptor(desc.width, desc.height, RenderTextureFormat.Depth, desc.depthBufferBits), FilterMode.Point, TextureWrapMode.Clamp, name: "_SceneDepthTex");
                    cmd.Blit(null, depthTexture.nameID, preparationMaterial, 0);
                    cmd.SetGlobalTexture("_SceneDepthTex", depthTexture.nameID);
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
                    RenderingUtils.ReAllocateHandleIfNeeded(ref colorTexture, new RenderTextureDescriptor(desc.width, desc.height, RenderTextureFormat.Default, 0), FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SceneColorTex");
                    cmd.Blit(null, colorTexture.nameID, preparationMaterial, 1);
                    cmd.SetGlobalTexture("_SceneColorTex", colorTexture.nameID);
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
                    RenderingUtils.ReAllocateHandleIfNeeded(ref normalTexture, new RenderTextureDescriptor(desc.width, desc.height, RenderTextureFormat.Default, 0), FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SceneNormalTex");
                    cmd.Blit(null, normalTexture.nameID, preparationMaterial, 2);
                    cmd.SetGlobalTexture("_SceneNormalTex", normalTexture.nameID);
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
        if (preparationPass != null)
        {
            preparationPass.ReleaseRT();
            preparationPass = null;
        }

        preparationPass = new PreparationPass(settings);
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(preparationPass);
    }
}