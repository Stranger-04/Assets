using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mine.Picker
{
    public class OutlineFeature : ScriptableRendererFeature
    {
        public static OutlinePass RegisteredPass { get; private set; }

        [SerializeField] private bool m_DebugShowMask = false;

        private OutlinePass m_Pass;
        public  OutlinePass Pass => m_Pass;

        public override void Create()
        {
            m_Pass = new OutlinePass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            RegisteredPass = m_Pass;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Pass == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game
                && renderingData.cameraData.cameraType != CameraType.SceneView) return;
            m_Pass.debugShowMask = m_DebugShowMask;
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { m_Pass?.Dispose(); m_Pass = null; RegisteredPass = null; }
            base.Dispose(disposing);
        }
    }
}
