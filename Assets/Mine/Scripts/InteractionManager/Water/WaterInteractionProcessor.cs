using UnityEngine;

namespace Mine.Interaction
{
    /// <summary>
    /// 水面交互处理器 — 在 Compute Shader 中执行波纹扩散 + 指数时间衰减。
    ///
    /// CSWater kernel: 8×8 线程组，读取 _InteractionTex (当前帧正交相机渲染的原始输入)
    /// 和 _InteractionResultTex (上一帧结果)，计算衰减 + 四邻域扩散，写入 _InteractionResultTex。
    ///
    /// 挂载在 UniversalInteractionManager 同一 GameObject 上，由 Manager 通过
    /// GetComponent&lt;IUniversalInteractionProcessor&gt;() 自动发现。
    /// </summary>
    [AddComponentMenu("Mine/Interaction/Water Interaction Processor")]
    public class WaterInteractionProcessor : MonoBehaviour, IUniversalInteractionProcessor
    {
        [Header("Compute")]
        [SerializeField] private ComputeShader _computeShader;

        // ── Shader Property IDs ─────────────────────────────────

        static readonly int s_InteractionTexID       = Shader.PropertyToID("_InteractionTex");
        static readonly int s_InteractionResultTexID = Shader.PropertyToID("_InteractionResultTex");
        static readonly int s_DeltaTimeID            = Shader.PropertyToID("_DeltaTime");
        static readonly int s_ResolutionID           = Shader.PropertyToID("_Resolution");

        // ── 运行时 ──────────────────────────────────────────────

        int _kernel;
        int _resolution;

        // ════════════════════════════════════════════════════════
        //  IUniversalInteractionProcessor
        // ════════════════════════════════════════════════════════

        public void Initialize(int resolution, RenderTexture sourceRT, RenderTexture resultRT)
        {
            if (_computeShader == null)
            {
                Debug.LogError("[WaterInteractionProcessor] ComputeShader is null.");
                return;
            }

            _resolution = resolution;
            _kernel     = _computeShader.FindKernel("CSWater");

            _computeShader.SetTexture(_kernel, s_InteractionTexID,       sourceRT);
            _computeShader.SetTexture(_kernel, s_InteractionResultTexID, resultRT);
            _computeShader.SetInt(s_ResolutionID, _resolution);
        }

        public void Process(float deltaTime)
        {
            if (_computeShader == null) return;

            _computeShader.SetFloat(s_DeltaTimeID, deltaTime);

            int threadGroups = Mathf.CeilToInt(_resolution / 8f);
            _computeShader.Dispatch(_kernel, threadGroups, threadGroups, 1);
        }

        public void Release()
        {
            if (_computeShader == null) return;

            _computeShader.SetTexture(_kernel, s_InteractionTexID,       null);
            _computeShader.SetTexture(_kernel, s_InteractionResultTexID, null);
        }
    }
}
