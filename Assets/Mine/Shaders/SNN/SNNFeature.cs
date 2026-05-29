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
        private RTHandle tempRT;

        public void ReleaseRT()
        {
            tempRT?.Release();
            tempRT = null;
        }

        public SNNPass(Shader shader, Settings s)
        {
            settings = s;
            snnMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        [System.Obsolete]
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
            var source = cameraData.renderer.cameraColorTargetHandle.nameID;
            var desc = cameraData.cameraTargetDescriptor;

            RenderingUtils.ReAllocateHandleIfNeeded(ref tempRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SNNTempRT");
            cmd.Blit(source, tempRT.nameID, snnMaterial);
            cmd.Blit(tempRT.nameID, source);
        }
    }

    public Settings settings = new Settings();
    SNNPass snnPass;

    public override void Create()
    {
        if (snnPass != null)
        {
            snnPass.ReleaseRT();
            snnPass = null;
        }

        if (settings.snnShader == null) return;
        snnPass = new SNNPass(settings.snnShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (snnPass == null) return;
        renderer.EnqueuePass(snnPass);
    }
}