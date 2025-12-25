using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SNNFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader snnShader;
        [Range(1, 10)] public int Radius = 3;
    }
    class SNNPass : ScriptableRenderPass
    {
        private Material snnMaterial;
        private Settings settings;
        private RenderTargetHandle tempRT;

        public SNNPass(Shader shader, Settings s)
        {
            settings = s;
            tempRT.Init("_SNNTempRT");
            snnMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (snnMaterial == null) return;
            var cmd = CommandBufferPool.Get("SNN");
            var cameraData = renderingData.cameraData;
            
            snnMaterial.SetInt("_Radius", settings.Radius);

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
            cmd.Blit(source, tempRT.id, snnMaterial);
            cmd.Blit(tempRT.id, source);

            cmd.ReleaseTemporaryRT(tempRT.id);
        }
    }

    public Settings settings = new Settings();
    SNNPass snnPass;

    public override void Create()
    {   
        if (settings.snnShader == null) return;
        snnPass = new SNNPass(settings.snnShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (snnPass == null) return;
        renderer.EnqueuePass(snnPass);
    }
}