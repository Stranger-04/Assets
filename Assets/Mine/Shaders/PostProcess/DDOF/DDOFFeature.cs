
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
        private RTHandle maskRT;
        private RTHandle tempRT;
        private RTHandle blur1RT;
        private RTHandle blur2RT;

        public void ReleaseRT()
        {
            maskRT?.Release();
            tempRT?.Release();
            blur1RT?.Release();
            blur2RT?.Release();

            maskRT = null;
            tempRT = null;
            blur1RT = null;
            blur2RT = null;
        }

        public DDOFPass(Shader shader, Settings s)
        {
            settings = s;
            ddofMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        [System.Obsolete]
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
            var source = cameraData.renderer.cameraColorTargetHandle.nameID;
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateHandleIfNeeded(ref maskRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DDOFMaskRT");
            RenderingUtils.ReAllocateHandleIfNeeded(ref tempRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DDOFTempMainRT");
            RenderingUtils.ReAllocateHandleIfNeeded(ref blur1RT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DDOFBlur1RT");
            RenderingUtils.ReAllocateHandleIfNeeded(ref blur2RT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DDOFBlur2RT");

            cmd.Blit(source, tempRT.nameID);
            cmd.SetGlobalTexture("_DDOFTempMainTex", tempRT.nameID);
            cmd.Blit(null, maskRT.nameID, ddofMaterial, 0);
            cmd.SetGlobalTexture("_DDOFCoCTex", maskRT.nameID);

            cmd.Blit(source, blur1RT.nameID, ddofMaterial, 1);
            cmd.Blit(blur1RT.nameID, blur2RT.nameID, ddofMaterial, 2);
            cmd.Blit(blur2RT.nameID, source, ddofMaterial, 3);
        }
    }

    public Settings settings = new Settings();
    DDOFPass ddofpass;

    public override void Create()
    {
        if (ddofpass != null)
        {
            ddofpass.ReleaseRT();
            ddofpass = null;
        }

        if (settings.ddofShader == null) return;
        ddofpass = new DDOFPass(settings.ddofShader, settings);
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ddofpass == null) return;
        renderer.EnqueuePass(ddofpass);
    }
}
