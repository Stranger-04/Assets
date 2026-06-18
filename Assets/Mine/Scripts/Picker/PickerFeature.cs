using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mine.Picker
{
    public class PickerFeature : ScriptableRendererFeature
    {
        public static PickerPass RegisteredPass { get; private set; }

        [SerializeField] private PickerPass.DebugView m_DebugView = PickerPass.DebugView.Off;

        private PickerPass m_Pass;
        public  PickerPass Pass => m_Pass;

        public override void Create()
        {
            m_Pass = new PickerPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            m_Pass.debugView = m_DebugView;
            RegisteredPass = m_Pass;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Pass == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game
                && renderingData.cameraData.cameraType != CameraType.SceneView) return;
            m_Pass.debugView = m_DebugView;
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { m_Pass?.Dispose(); m_Pass = null; RegisteredPass = null; }
            base.Dispose(disposing);
        }
    }
}
