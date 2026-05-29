using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SSRFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ssrShader;
        [Range(0.1f, 2f)] public float stepSize = 0.2f;
        [Range(1f, 200f)] public float maxDistance = 50f;

        [Range(8, 256)] public int stepCount = 64;
        [Range(0, 32)] public int binaryCount = 6;
        [Range(1, 8)] public int mipCount = 4;

        [Range(0.001f, 0.5f)] public float thickness = 0.05f;
        [Range(0f, 1f)] public float smoothness = 1f;
        [Range(0f, 1f)] public float jitterScale = 0.5f;
        [Range(0f, 5f)] public float blurScale = 0.5f;


        public bool SSRFeature = true;

        public enum SSRType
        {
            HiZ2D,
            DDA2D,
            Ray3D
        }
        public SSRType ssrType = SSRType.DDA2D;
    }

    class SSRRenderPass : ScriptableRenderPass
    {
        private Material ssrMaterial;
        private Settings settings;
        private RTHandle ssrRT;
        private RTHandle blur1RT;
        private RTHandle blur2RT;
        private RTHandle mHiZRT;
        private RTHandle[] mHiZRTs;
        private RenderTextureDescriptor mHiZDesc;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;

        public void ReleaseRT()
        {
            ssrRT?.Release();
            blur1RT?.Release();
            blur2RT?.Release();
            mHiZRT?.Release();
            if (mHiZRTs != null)
            {
                foreach (var rt in mHiZRTs)
                {
                    rt?.Release();
                }
            }
            ssrRT = null;
            blur1RT = null;
            blur2RT = null;
            mHiZRT = null;
            mHiZRTs = null;
        }

        public SSRRenderPass(Shader shader, Settings s)
        {
            settings = s;
            mHiZRTs = new RTHandle[settings.mipCount];
            ssrMaterial = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        private void SetupMiZHierarchy(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var renderer = renderingData.cameraData.renderer;
            var desc = renderingData.cameraData.cameraTargetDescriptor;

            // Only setup if screen size changed
            if (lastScreenWidth == desc.width && lastScreenHeight == desc.height)
                return;

            lastScreenWidth = desc.width;
            lastScreenHeight = desc.height;

            var width = Mathf.Max((int)Mathf.Ceil(Mathf.Log(desc.width, 2) - 1.0f), 1);
            var height = Mathf.Max((int)Mathf.Ceil(Mathf.Log(desc.height, 2) - 1.0f), 1);
            width = 1 << width;
            height = 1 << height;
            // mip 0
            mHiZDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat, 0, settings.mipCount);
            mHiZDesc.sRGB = false;
            mHiZDesc.useMipMap = true;
            mHiZDesc.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref mHiZRT, mHiZDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSRmHiZRT");
            // other mips
            RenderTextureDescriptor[] mHiZDescs = new RenderTextureDescriptor[settings.mipCount];
            for (int i = 0; i < settings.mipCount; i++)
            {
                mHiZDescs[i] = new RenderTextureDescriptor(Mathf.Max(1, width >> i), Mathf.Max(1, height >> i), RenderTextureFormat.RFloat, 0, 1);
                mHiZDescs[i].sRGB = false;
                mHiZDescs[i].useMipMap = false;
                mHiZDescs[i].msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref mHiZRTs[i], mHiZDescs[i], FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSRmHiZRT_Mip" + i);
            }
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (ssrMaterial == null) return;
            var cmd = CommandBufferPool.Get("SSR");
            var cameraData = renderingData.cameraData;

            SetupMiZHierarchy(cmd, ref renderingData);

            ssrMaterial.SetFloat("_StepSize", settings.stepSize);
            ssrMaterial.SetFloat("_MaxDistance", settings.maxDistance);

            ssrMaterial.SetFloat("_Thickness", settings.thickness);
            ssrMaterial.SetFloat("_Smoothness", settings.smoothness);
            ssrMaterial.SetFloat("_JitterScale", settings.jitterScale);
            ssrMaterial.SetFloat("_BlurScale", settings.blurScale);

            ssrMaterial.SetInt("_StepCount", settings.stepCount);
            ssrMaterial.SetInt("_BinaryCount", settings.binaryCount);

            ssrMaterial.DisableKeyword("SSR_DDA2D");
            ssrMaterial.DisableKeyword("SSR_RAY3D");
            ssrMaterial.DisableKeyword("SSR_HIZ2D");
            if (settings.ssrType == Settings.SSRType.DDA2D)
            {
                ssrMaterial.EnableKeyword("SSR_DDA2D");
            }
            else if (settings.ssrType == Settings.SSRType.Ray3D)
            {
                ssrMaterial.EnableKeyword("SSR_RAY3D");
            }
            else if (settings.ssrType == Settings.SSRType.HiZ2D)
            {
                ssrMaterial.EnableKeyword("SSR_HIZ2D");
            }

            Matrix4x4 viewMatrix = cameraData.GetViewMatrix();
            Matrix4x4 projectionMatrix = cameraData.GetGPUProjectionMatrix();
            
            ssrMaterial.SetMatrix("_CameraViewMatrix", viewMatrix);
            ssrMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);
            
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

            RenderingUtils.ReAllocateHandleIfNeeded(ref ssrRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSRResultRT");
            RenderingUtils.ReAllocateHandleIfNeeded(ref blur1RT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSRBlur1RT");
            RenderingUtils.ReAllocateHandleIfNeeded(ref blur2RT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSRBlur2RT");

            // mHiz generation
            cmd.Blit(renderer.cameraDepthTargetHandle.nameID, mHiZRTs[0].nameID);
            cmd.CopyTexture(mHiZRTs[0].nameID, 0, 0, mHiZRT.nameID, 0, 0);

            for (int i = 1; i < settings.mipCount; i++)
            {
                ssrMaterial.SetFloat("_FromMipLevel", i - 1);
                ssrMaterial.SetVector("_TexelSize", new Vector4(
                    1.0f / mHiZRTs[i - 1].rt.width, 
                    1.0f / mHiZRTs[i - 1].rt.height, 
                    mHiZRTs[i - 1].rt.width, 
                    mHiZRTs[i - 1].rt.height));

                cmd.Blit(mHiZRTs[i - 1].nameID, mHiZRTs[i].nameID, ssrMaterial, 3);
                cmd.CopyTexture(mHiZRTs[i].nameID, 0, 0, mHiZRT.nameID, 0, i);
            }
            ssrMaterial.SetFloat("_MaxMipLevel", settings.mipCount);
            cmd.SetGlobalTexture("_HiZTex", mHiZRT.nameID);
            // mHiz generation end

            cmd.Blit(null, blur1RT.nameID, ssrMaterial, 0);
            cmd.Blit(blur1RT.nameID, blur2RT.nameID, ssrMaterial, 1);
            cmd.Blit(blur2RT.nameID, ssrRT.nameID, ssrMaterial, 2);
            cmd.SetGlobalTexture("_SSRTexture", ssrRT.nameID);

            if (settings.SSRFeature)
            {
                cmd.Blit(ssrRT.nameID, renderer.cameraColorTargetHandle.nameID);
            }
        }
    }

    public Settings settings = new Settings();
    SSRRenderPass ssrPass;

    public override void Create()
    {
        if (ssrPass != null)
        {
            ssrPass.ReleaseRT();
            ssrPass = null;
        }

        if (settings.ssrShader != null)
        {
            ssrPass = new SSRRenderPass(settings.ssrShader, settings);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ssrPass != null)
        {
            renderer.EnqueuePass(ssrPass);
        }
    }
}
