using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SSRFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ssrShader;
        [Range(0.01f,1f)] public float stepSize = 0.2f;
        [Range(1f,200f)] public float maxDistance = 50f;
        [Range(8,256)] public int maxSteps = 64;
        [Range(0.001f,0.5f)] public float thickness = 0.05f;
        [Range(0f,1f)] public float smoothness = 1f;

        public bool SSRFeature = true;
    }

    class SSRRenderPass : ScriptableRenderPass
    {
        private Material ssrMaterial;
        private Settings settings;
        private RenderTargetHandle ssrRT;

        public SSRRenderPass(Shader shader, Settings s)
        {
            ssrMaterial = CoreUtils.CreateEngineMaterial(shader);
            settings = s;
            ssrRT.Init("_SSRResultRT");
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents; 
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var desc = cameraTextureDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;

            cmd.GetTemporaryRT(ssrRT.id, desc, FilterMode.Bilinear);
            ConfigureTarget(ssrRT.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (ssrMaterial == null) return;
            var cmd = CommandBufferPool.Get("SSR");

            ssrMaterial.SetFloat("_StepSize", settings.stepSize);
            ssrMaterial.SetFloat("_MaxDistance", settings.maxDistance);
            ssrMaterial.SetInt("_MaxSteps", settings.maxSteps);
            ssrMaterial.SetFloat("_Thickness", settings.thickness);
            ssrMaterial.SetFloat("_Smoothness", settings.smoothness);

            var renderer = renderingData.cameraData.renderer;
            var cameraData = renderingData.cameraData;
            Matrix4x4 viewMatrix = cameraData.GetViewMatrix();
            Matrix4x4 projectionMatrix = cameraData.GetGPUProjectionMatrix();
            Matrix4x4 viewProjectionMatrix = projectionMatrix * viewMatrix;
            Matrix4x4 inverseViewProjectionMatrix = viewProjectionMatrix.inverse;
            
            ssrMaterial.SetMatrix("_ViewProjectionMatrix", viewProjectionMatrix);
            ssrMaterial.SetMatrix("_InverseViewProjectionMatrix", inverseViewProjectionMatrix);
            ssrMaterial.SetVector("_WorldSpaceCameraPosCustom", cameraData.camera.transform.position);

            cmd.Blit(renderer.cameraColorTargetHandle.nameID, ssrRT.Identifier(), ssrMaterial, 0);
            cmd.SetGlobalTexture("_SSRTexture", ssrRT.id);

            if (settings.SSRFeature)
            {
                cmd.Blit(ssrRT.Identifier(), renderer.cameraColorTargetHandle.nameID);
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd != null)
            {
                cmd.ReleaseTemporaryRT(ssrRT.id);
            }
        }
    }

    public Settings settings = new Settings();
    SSRRenderPass ssrPass;

    public override void Create()
    {
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
