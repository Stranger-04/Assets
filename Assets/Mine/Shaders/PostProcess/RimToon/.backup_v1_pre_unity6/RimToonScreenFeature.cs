
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
        private RTHandle tempRT;
        private RTHandle maskRT;
        private RTHandle colorRT;
        private RTHandle blur1RT;
        private RTHandle blur2RT;

        public void ReleaseRT()
        {
            tempRT?.Release();
            maskRT?.Release();
            colorRT?.Release();
            blur1RT?.Release();
            blur2RT?.Release();

            tempRT = null;
            maskRT = null;
            colorRT = null;
            blur1RT = null;
            blur2RT = null;
        }

        public RimToonScreenPass(Shader shader, Settings s)
        {
            settings = s;
            rtsMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        [System.Obsolete]
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
            RenderingUtils.ReAllocateHandleIfNeeded(ref maskRT, maskDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_RimToonMaskRT");

            var colorDesc = baseDesc;
            colorDesc.depthBufferBits = 0;
            colorDesc.msaaSamples = baseDesc.msaaSamples;
            colorDesc.colorFormat = RenderTextureFormat.ARGB32;
            RenderingUtils.ReAllocateHandleIfNeeded(ref colorRT, colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RimToonColorRT");

            RenderingUtils.ReAllocateHandleIfNeeded(ref tempRT, baseDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RTTempMainRT");
            cmd.Blit(source, tempRT.nameID);
            cmd.SetGlobalTexture("_RTTempMainTex", tempRT.nameID);

            RenderingUtils.ReAllocateHandleIfNeeded(ref blur1RT, baseDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RimToonBlurRT1");
            RenderingUtils.ReAllocateHandleIfNeeded(ref blur2RT, baseDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RimToonBlurRT2");

            var depthTarget = renderer.cameraDepthTargetHandle;
            cmd.SetRenderTarget(maskRT.nameID, depthTarget);
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 0);
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 1);

            cmd.SetRenderTarget(colorRT.nameID);
            cmd.SetGlobalTexture("_RimToonMaskRT", maskRT.nameID);
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 2);

            cmd.Blit(colorRT.nameID, blur1RT.nameID);
            // downsampling blur
            for (int i = 0; i < settings.blurLevels; i++)
            {
                int downsampledWidth = Mathf.Max(1, colorDesc.width >> i);
                int downsampledHeight = Mathf.Max(1, colorDesc.height >> i);
                for (int j = 0; j < settings.blurIterations; j++)
                {
                    RenderingUtils.ReAllocateHandleIfNeeded(ref blur2RT, new RenderTextureDescriptor(downsampledWidth, downsampledHeight, RenderTextureFormat.Default, 0), FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RimToonBlurRT2");
                    cmd.Blit(blur1RT.nameID, blur2RT.nameID, rtsMaterial, 3);
                    RenderingUtils.ReAllocateHandleIfNeeded(ref blur1RT, new RenderTextureDescriptor(downsampledWidth, downsampledHeight, RenderTextureFormat.Default, 0), FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RimToonBlurRT1");
                    cmd.Blit(blur2RT.nameID, blur1RT.nameID, rtsMaterial, 4);
                }
            }

            // upsampling blur
            for (int i = settings.blurLevels - 1; i >= 0; i--)
            {
                int upsampledWidth = Mathf.Max(1, colorDesc.width >> i);
                int upsampledHeight = Mathf.Max(1, colorDesc.height >> i);
                for (int j = 0; j < settings.blurIterations; j++)
                {
                    RenderingUtils.ReAllocateHandleIfNeeded(ref blur2RT, new RenderTextureDescriptor(upsampledWidth, upsampledHeight, RenderTextureFormat.Default, 0), FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RimToonBlurRT2");
                    cmd.Blit(blur1RT.nameID, blur2RT.nameID, rtsMaterial, 3);
                    RenderingUtils.ReAllocateHandleIfNeeded(ref blur1RT, new RenderTextureDescriptor(upsampledWidth, upsampledHeight, RenderTextureFormat.Default, 0), FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_RimToonBlurRT1");
                    cmd.Blit(blur2RT.nameID, blur1RT.nameID, rtsMaterial, 4);
                }
            }

            cmd.SetGlobalTexture("_RimToonBlurRT", blur1RT.nameID);

            cmd.SetRenderTarget(source);
            cmd.SetGlobalTexture("_RimToonColorRT", colorRT.nameID);
            CoreUtils.DrawFullScreen(cmd, rtsMaterial, null, 5);
        }
    }

    public Settings settings = new Settings();
    RimToonScreenPass srtpass;

    public override void Create()
    {
        if (srtpass != null)
        {
            srtpass.ReleaseRT();
            srtpass = null;
        }

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
