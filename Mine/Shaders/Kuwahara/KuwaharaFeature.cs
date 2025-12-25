using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KuwaharaFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader kuwaharaShader;
        [Range(1, 10)] public int Radius = 5;
        public enum KuwaharaType
        {
            Basic,
            Generalized
        }
        public KuwaharaType kuwaharaType = KuwaharaType.Basic;
    }
    class KuwaharaPass : ScriptableRenderPass
    {
        private Material kuwaharaMaterial;
        private Settings settings;
        private RenderTargetHandle tempRT;

        public KuwaharaPass(Shader shader, Settings s)
        {
            settings = s;
            tempRT.Init("_KuwaharaTempRT");
            kuwaharaMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (kuwaharaMaterial == null) return;
            var cmd = CommandBufferPool.Get("Kuwahara");
            var cameraData = renderingData.cameraData;
            
            kuwaharaMaterial.SetInt("_Radius", settings.Radius);
            kuwaharaMaterial.DisableKeyword("KUWAHARA_BASIC");
            kuwaharaMaterial.DisableKeyword("KUWAHARA_GENERALIZED");
            if (settings.kuwaharaType == Settings.KuwaharaType.Basic)
            {
                kuwaharaMaterial.EnableKeyword("KUWAHARA_BASIC");
            }
            else if (settings.kuwaharaType == Settings.KuwaharaType.Generalized)
            {
                kuwaharaMaterial.EnableKeyword("KUWAHARA_GENERALIZED");
            }

            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            var source = cameraData.renderer.cameraColorTarget;
            int width = cameraData.camera.scaledPixelWidth;
            int height = cameraData.camera.scaledPixelHeight;

            cmd.GetTemporaryRT(tempRT.id, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmd.Blit(source, tempRT.Identifier(), kuwaharaMaterial);
            cmd.Blit(tempRT.Identifier(), source);

            cmd.ReleaseTemporaryRT(tempRT.id);
        }
    }

    public Settings settings = new Settings();
    KuwaharaPass kuwaharaPass;

    public override void Create()
    {
        if (settings.kuwaharaShader == null) return;
        kuwaharaPass = new KuwaharaPass(settings.kuwaharaShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (kuwaharaPass == null) return;
        renderer.EnqueuePass(kuwaharaPass);
    }
}