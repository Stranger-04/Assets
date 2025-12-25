
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RimToonScreenFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader rimToonScreenShader;
        [Range(1f, 10f)] public float rimPower = 5.0f;
        [Range(0f, 5f)] public float blurScale = 0.5f;
        [Range(0f, 1f)] public float blurIntensity = 0.5f;
        [Range(0, 4)]   public int blurLevels = 1;
        [Range(0, 4)]   public int blurIterations = 1;

    }
    class RimToonScreenPass : ScriptableRenderPass
    {
        private Material rtsMaterial;
        private Settings settings;
        private RenderTargetHandle tempRT;
        private RenderTargetHandle maskRT;
        private RenderTargetHandle colorRT;
        private RenderTargetHandle blur1RT;
        private RenderTargetHandle blur2RT;

        public RimToonScreenPass(Shader shader, Settings s)
        {
            settings = s;
            tempRT.Init("_RTTempMainRT");
            maskRT.Init("_RimToonMaskRT");
            colorRT.Init("_RimToonColorRT");
            blur1RT.Init("_RimToonBlurRT1");
            blur2RT.Init("_RimToonBlurRT2");
            rtsMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (rtsMaterial == null) return;
            var cmd = CommandBufferPool.Get("RimToonScreen");
            var cameraData = renderingData.cameraData;
            
            rtsMaterial.SetFloat("_RimPower", settings.rimPower);
            rtsMaterial.SetFloat("_BlurScale", settings.blurScale);
            rtsMaterial.SetFloat("_BlurIntensity", settings.blurIntensity);

            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var renderer = renderingData.cameraData.renderer;
            var source = renderer.cameraColorTargetHandle.nameID;
            var baseDesc = renderingData.cameraData.cameraTargetDescriptor;

            var maskDesc = baseDesc;
            maskDesc.depthBufferBits = 0;
            maskDesc.msaaSamples = baseDesc.msaaSamples;
            maskDesc.colorFormat = RenderTextureFormat.R8;
            cmd.GetTemporaryRT(maskRT.id, maskDesc, FilterMode.Point);

            var colorDesc = baseDesc;
            colorDesc.depthBufferBits = 0;
            colorDesc.msaaSamples = baseDesc.msaaSamples;
            colorDesc.colorFormat = RenderTextureFormat.ARGB32;
            cmd.GetTemporaryRT(colorRT.id, colorDesc, FilterMode.Bilinear);

            cmd.GetTemporaryRT(tempRT.id, baseDesc, FilterMode.Bilinear);
            cmd.Blit(source, tempRT.Identifier());
            cmd.SetGlobalTexture("_RTTempMainTex", tempRT.Identifier());

            cmd.GetTemporaryRT(blur1RT.id, baseDesc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(blur2RT.id, baseDesc, FilterMode.Bilinear);

            var depthTarget = renderer.cameraDepthTargetHandle;
            cmd.SetRenderTarget(maskRT.Identifier(), depthTarget);
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 0);
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 1);

            cmd.SetRenderTarget(colorRT.Identifier());
            cmd.SetGlobalTexture("_RimToonMaskRT", maskRT.Identifier());
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 2);

            cmd.Blit(colorRT.Identifier(), blur1RT.Identifier());
            // downsampling blur
            for (int i = 0; i < settings.blurLevels; i++)
            {
                int downsampledWidth = Mathf.Max(1, colorDesc.width >> i);
                int downsampledHeight = Mathf.Max(1, colorDesc.height >> i);
                for (int j = 0; j < settings.blurIterations; j++)
                {
                    cmd.Blit(blur1RT.Identifier(), blur2RT.Identifier(), rtsMaterial, 3);
                    cmd.ReleaseTemporaryRT(blur1RT.id);
                    cmd.GetTemporaryRT(blur1RT.id, downsampledWidth, downsampledHeight);
                    cmd.Blit(blur2RT.Identifier(), blur1RT.Identifier(), rtsMaterial, 4);
                    cmd.ReleaseTemporaryRT(blur2RT.id);
                    cmd.GetTemporaryRT(blur2RT.id, downsampledWidth, downsampledHeight);
                }
            }

            // upsampling blur
            for (int i = settings.blurLevels - 1; i >= 0; i--)
            {
                int upsampledWidth = Mathf.Max(1, colorDesc.width >> i);
                int upsampledHeight = Mathf.Max(1, colorDesc.height >> i);
                for (int j = 0; j < settings.blurIterations; j++)
                {
                    cmd.Blit(blur1RT.Identifier(), blur2RT.Identifier(), rtsMaterial, 3);
                    cmd.ReleaseTemporaryRT(blur1RT.id);
                    cmd.GetTemporaryRT(blur1RT.id, upsampledWidth, upsampledHeight);
                    cmd.Blit(blur2RT.Identifier(), blur1RT.Identifier(), rtsMaterial, 4);
                    cmd.ReleaseTemporaryRT(blur2RT.id);
                    cmd.GetTemporaryRT(blur2RT.id, upsampledWidth, upsampledHeight);
                }
            }

            cmd.SetGlobalTexture("_RimToonBlurRT", blur1RT.Identifier());

            cmd.SetRenderTarget(source);
            cmd.SetGlobalTexture("_RimToonColorRT", colorRT.Identifier());
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 5);

            cmd.ReleaseTemporaryRT(maskRT.id);
            cmd.ReleaseTemporaryRT(colorRT.id);
        }
    }

    public Settings settings = new Settings();
    RimToonScreenPass srtpass;

    public override void Create()
    {
        if (settings.rimToonScreenShader != null)
        {
            srtpass = new RimToonScreenPass(settings.rimToonScreenShader, settings);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (srtpass != null)
        {
            renderer.EnqueuePass(srtpass);
        }
    }
}
