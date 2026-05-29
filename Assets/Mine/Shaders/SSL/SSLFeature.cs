using System.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SSLFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader sslShader;
        [Range(1, 256)] public int   maxSteps = 32;
        [Range(0.1f, 100f)] public float maxDistance = 10f;
        [Range(0f, 5f)] public float intensity = 1f;
        [Range(0f, 2f)] public float sslScale = 0.5f;
        [Range(0f, 1f)] public float jitterScale = 0.5f;
        [Range(0f, 5f)] public float blurScale = 0.5f;
        [Range(0, 4)]   public int blurLevels = 1;
        [Range(0, 4)]   public int blurIterations = 1;
        public bool SSLFeature = true;
    }

    class SSLRenderPass : ScriptableRenderPass
    {
        private Material sslMaterial;
        private Settings settings;
        private RTHandle sslBlurRT1;
        private RTHandle sslBlurRT2;
        private RTHandle tempMainRT;

        public void ReleaseRT()
        {
            sslBlurRT1?.Release();
            sslBlurRT2?.Release();
            tempMainRT?.Release();

            sslBlurRT1 = null;
            sslBlurRT2 = null;
            tempMainRT = null;
        }

        public SSLRenderPass(Shader shader, Settings s)
        {
            settings = s;
            sslMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (sslMaterial == null) return;
            var cmd = CommandBufferPool.Get("SSL");
            var stack = VolumeManager.instance.stack;
            var vol = stack.GetComponent<SSLVolume>();
            if (vol == null || !vol.IsActive() || !renderingData.cameraData.postProcessEnabled)
            {
                CommandBufferPool.Release(cmd);
                return;
            }

            sslMaterial.SetInt("_MaxSteps", vol.maxSteps.value);
            sslMaterial.SetFloat("_MaxDistance", vol.maxDistance.value);
            sslMaterial.SetFloat("_Intensity", vol.intensity.value);
            sslMaterial.SetFloat("_SSLScale", vol.sslScale.value);
            sslMaterial.SetFloat("_BlurScale", vol.blurScale.value);
            sslMaterial.SetFloat("_JitterScale", vol.jitterScale.value);

            sslMaterial.DisableKeyword("SSL_FOG");
            sslMaterial.DisableKeyword("SSL_LIGHT");
            if (vol.sslType == SSLVolume.SSLType.Fog)
            {
                sslMaterial.EnableKeyword("SSL_FOG");
            }
            else if (vol.sslType == SSLVolume.SSLType.Light)
            {
                sslMaterial.EnableKeyword("SSL_LIGHT");
            }

            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var renderer = renderingData.cameraData.renderer;
            var desc = renderingData.cameraData.cameraTargetDescriptor;

            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;

            RenderingUtils.ReAllocateHandleIfNeeded(ref sslBlurRT1, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_SSLBlurRT1");
            RenderingUtils.ReAllocateHandleIfNeeded(ref tempMainRT, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_SSLTempMainRT");

            cmd.Blit(renderer.cameraColorTargetHandle.nameID, tempMainRT.nameID);
            cmd.Blit(null, sslBlurRT1.nameID, sslMaterial, 0);

            // downsampling blur
            for (int i = 0; i < settings.blurLevels; i++)
            {
                int downsampledWidth = Mathf.Max(1, desc.width >> i + 1);
                int downsampledHeight = Mathf.Max(1, desc.height >> i + 1);
                for (int j = 0; j < settings.blurIterations; j++)
                {
                    RenderingUtils.ReAllocateHandleIfNeeded(ref sslBlurRT2, new RenderTextureDescriptor(downsampledWidth, downsampledHeight, RenderTextureFormat.Default, 0) { sRGB = false, useMipMap = false }, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_SSLBlurRT2");
                    cmd.Blit(sslBlurRT1.nameID, sslBlurRT2.nameID, sslMaterial, 1);
                    cmd.Blit(sslBlurRT2.nameID, sslBlurRT1.nameID, sslMaterial, 2);
                }
            }
            
            // upsampling blur
            for (int i = settings.blurLevels - 1; i >= 0; i--)
            {
                int upsampledWidth = Mathf.Max(1, desc.width >> i);
                int upsampledHeight = Mathf.Max(1, desc.height >> i);
                for (int j = 0; j < settings.blurIterations; j++)
                {
                    RenderingUtils.ReAllocateHandleIfNeeded(ref sslBlurRT2, new RenderTextureDescriptor(upsampledWidth, upsampledHeight, RenderTextureFormat.Default, 0) { sRGB = false, useMipMap = false }, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_SSLBlurRT2");
                    cmd.Blit(sslBlurRT1.nameID, sslBlurRT2.nameID, sslMaterial, 1);
                    cmd.Blit(sslBlurRT2.nameID, sslBlurRT1.nameID, sslMaterial, 2);
                }
            }

            // Texture Output
            cmd.SetGlobalTexture("_SSLTex", sslBlurRT1.nameID);

            cmd.Blit(tempMainRT.nameID, renderer.cameraColorTargetHandle.nameID, sslMaterial, 3);

            if (settings.SSLFeature)
            {
                cmd.Blit(sslBlurRT1.nameID, renderer.cameraColorTargetHandle.nameID);
            }
        }
    }

    public Settings settings = new Settings();
    SSLRenderPass sslPass;

    public override void Create()
    {
        if (sslPass != null)
        {
            sslPass.ReleaseRT();
            sslPass = null;
        }

        if (settings.sslShader != null)
        {
            sslPass = new SSLRenderPass(settings.sslShader, settings);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (sslPass != null)
        {
            renderer.EnqueuePass(sslPass);
        }
    }
}