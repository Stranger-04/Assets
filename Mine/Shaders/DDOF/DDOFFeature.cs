
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DDOFFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ddofShader;
        [Range(0f, 50f)] public float focusRange = 2.0f;
        [Range(0f, 5f)] public float blurScale = 2.5f;
    }
    class DDOFPass : ScriptableRenderPass
    {
        private Material ddofMaterial;
        private Settings settings;
        private RenderTargetHandle maskRT;
        private RenderTargetHandle tempRT;
        private RenderTargetHandle blur1RT;
        private RenderTargetHandle blur2RT;

        public DDOFPass(Shader shader, Settings s)
        {
            settings = s;
            maskRT.Init("_DDOFMaskRT");
            tempRT.Init("_DDOFTempMainRT");
            blur1RT.Init("_DDOFBlur1RT");
            blur2RT.Init("_DDOFBlur2RT");
            ddofMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (ddofMaterial == null) return;
            var cmd = CommandBufferPool.Get("DynamicDepthOfField");
            var cameraData = renderingData.cameraData;
            
            ddofMaterial.SetFloat("_FocusRange", settings.focusRange);
            ddofMaterial.SetFloat("_BlurScale", settings.blurScale);

            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            var source = cameraData.renderer.cameraColorTarget;
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            cmd.GetTemporaryRT(maskRT.id, desc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(tempRT.id, desc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(blur1RT.id, desc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(blur2RT.id, desc, FilterMode.Bilinear);

            cmd.Blit(source, tempRT.Identifier());
            cmd.SetGlobalTexture("_DDOFTempMainTex", tempRT.Identifier());
            cmd.Blit(null, maskRT.Identifier(), ddofMaterial, 0);
            cmd.SetGlobalTexture("_DDOFCoCTex", maskRT.Identifier());

            cmd.Blit(source, blur1RT.Identifier(), ddofMaterial, 1);
            cmd.Blit(blur1RT.Identifier(), blur2RT.Identifier(), ddofMaterial, 2);
            cmd.Blit(blur2RT.Identifier(), source, ddofMaterial, 3);

            cmd.ReleaseTemporaryRT(maskRT.id);
            cmd.ReleaseTemporaryRT(tempRT.id);
            cmd.ReleaseTemporaryRT(blur1RT.id);
            cmd.ReleaseTemporaryRT(blur2RT.id);
        }
    }

    public Settings settings = new Settings();
    DDOFPass ddofpass;

    public override void Create()
    {
        if (settings.ddofShader == null) return;
        ddofpass = new DDOFPass(settings.ddofShader, settings);
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ddofpass == null) return;
        renderer.EnqueuePass(ddofpass);
    }
}
