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
        private RTHandle tempRT;

        public void ReleaseRT()
        {
            tempRT?.Release();
            tempRT = null;
        }

        public KuwaharaPass(Shader shader, Settings s)
        {
            settings = s;
            kuwaharaMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        [System.Obsolete]
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
            var source = cameraData.renderer.cameraColorTargetHandle.nameID;
            var desc = cameraData.cameraTargetDescriptor;

            RenderingUtils.ReAllocateHandleIfNeeded(ref tempRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_KuwaharaTempRT");
            cmd.Blit(source, tempRT.nameID, kuwaharaMaterial);
            cmd.Blit(tempRT.nameID, source);
        }
    }

    public Settings settings = new Settings();
    KuwaharaPass kuwaharaPass;

    public override void Create()
    {
        if (kuwaharaPass != null)
        {
            kuwaharaPass.ReleaseRT();
            kuwaharaPass = null;
        }

        if (settings.kuwaharaShader == null) return;
        kuwaharaPass = new KuwaharaPass(settings.kuwaharaShader, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (kuwaharaPass == null) return;
        renderer.EnqueuePass(kuwaharaPass);
    }
}