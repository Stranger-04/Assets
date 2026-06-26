using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Mine.Picker
{
    /// <summary>
    /// 鼠标点击 GPU Picker：点击屏幕 → AsyncGPUReadback 读回 ObjectID。
    /// 挂载到场景中任意 GameObject 上即可。
    /// </summary>
    public class PickerReadback : MonoBehaviour
    {
        // ── 配置 ────────────────────────────────────────────────

        [SerializeField] private Camera m_Camera;

        // ── 状态 ────────────────────────────────────────────────

        private RenderTexture m_ObjIDRT;
        private OutlinePass   m_OutlinePass;
        private bool          m_ReadbackPending;
        private bool          m_Initialized;

        // ════════════════════════════════════════════════════════

        private void Start()
        {
            if (m_Camera == null)
                m_Camera = Camera.main;
        }

        private void Update()
        {
            // 每帧刷新 RT 引用（PickerPass 可能在分辨率变化时重建 RT）
            m_ObjIDRT     = PickerFeature.Picker?.ObjIDRenderTexture;
            m_OutlinePass = PickerFeature.Outline;

            if (!m_Initialized)
            {
                if (m_ObjIDRT != null && m_OutlinePass != null)
                {
                    m_Initialized = true;
                    Debug.Log("[PickerReadback] Ready. Click to pick + outline.");
                }
                else return;
            }

            if (m_ReadbackPending) return;

            // Input System: 鼠标左键按下
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                RequestReadback(mousePos);
            }
        }

        // ════════════════════════════════════════════════════════

        private void RequestReadback(Vector2 mousePosition)
        {
            if (m_Camera == null) return;

            // 检查 RT 是否存活（可能被 PickerPass 重建）
            if (m_ObjIDRT == null || !m_ObjIDRT.IsCreated())
            {
                Debug.LogWarning("[PickerReadback] ObjectID RT not ready.");
                return;
            }

            // 屏幕坐标 → 视口坐标 → RT 像素坐标
            // 这样 Game 视图缩放时也能正确映射
            Vector3 viewportPoint = m_Camera.ScreenToViewportPoint(mousePosition);

            // 视口外 → 没点在 Game 视图内
            if (viewportPoint.x < 0 || viewportPoint.x > 1 ||
                viewportPoint.y < 0 || viewportPoint.y > 1)
                return;

            Vector2Int pixel = new Vector2Int(
                Mathf.FloorToInt(viewportPoint.x * m_ObjIDRT.width),
                Mathf.FloorToInt(viewportPoint.y * m_ObjIDRT.height));

            m_ReadbackPending = true;

            // 异步读回 1×1 像素
            // overload: (Texture, mip, x, width, y, height, z, depth, callback)
            AsyncGPUReadback.Request(
                m_ObjIDRT,
                0,            // mipLevel
                pixel.x,      // x
                1,            // width
                pixel.y,      // y
                1,            // height
                0,            // z
                1,            // depth
                OnReadbackComplete
            );
        }

        private void OnReadbackComplete(AsyncGPUReadbackRequest request)
        {
            m_ReadbackPending = false;

            if (request.hasError)
            {
                Debug.LogWarning("[PickerReadback] Readback failed.");
                return;
            }

            if (!request.done) return;

            var data = request.GetData<byte>();
            if (data.Length == 0) return;

            // RGB24 编码：4 字节/像素 (R, G, B, A)
            // id = r << 16 | g << 8 | b
            int objectID = (data[0] << 16) | (data[1] << 8) | data[2];

            if (objectID > 0)
            {
                Debug.Log($"<color=cyan>[PickerReadback]</color> Clicked ObjectID = <b>{objectID}</b>");
                if (m_OutlinePass != null)
                    m_OutlinePass.selectedObjectID = objectID;
            }
            else
            {
                Debug.Log("[PickerReadback] Clicked background (ID=0).");
                if (m_OutlinePass != null)
                    m_OutlinePass.selectedObjectID = 0;
            }
        }

        // ════════════════════════════════════════════════════════

        private void OnDestroy()
        {
            m_ObjIDRT = null;
        }
    }
}
