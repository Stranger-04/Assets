using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Mine.Interaction
{
    /// <summary>
    /// 正交交互系统总控。
    ///
    /// 正交相机使用 CustomRenderer（自定义管线）渲染到 _InteractionTex，
    /// UniversalInteractionManager 管理 RT 生命周期 + Compute 处理调度 + 全局 Shader 属性。
    ///
    /// IUniversalInteractionProcessor 实现者挂载在同一 GameObject 上，
    /// Manager 通过 GetComponent 自动发现，无需硬编码类型切换。
    /// </summary>
    ///
    /// <remarks>
    /// 设置步骤：
    /// 1. 创建 CustomRendererData 资产 → 添加到 URP Renderer List
    /// 2. 场景中创建正交相机子物体，挂载 UniversalAdditionalCameraData，Renderer 设为 CustomRenderer
    /// 3. 拖入 _orthoCamera 字段
    /// 4. 在同一 GameObject 上挂载 IUniversalInteractionProcessor 实现（如 WaterInteractionProcessor）
    /// </remarks>
    [ExecuteAlways]
    public class UniversalInteractionManager : MonoBehaviour
    {
        [Header("正交相机")]
        [SerializeField] private Camera _orthoCamera;
        [SerializeField] private int    _rendererIndex = 1;
        [SerializeField] private float  _areaSize      = 10f;
        [SerializeField] private float  _orthoNear     = -10f;
        [SerializeField] private float  _orthoFar      = 10f;

        [Header("RT 设置")]
        [SerializeField] private int _resolution = 256;

        // ── Shader Property IDs ─────────────────────────────────

        static readonly int s_InteractionTexID       = Shader.PropertyToID("_InteractionTex");
        static readonly int s_InteractionResultTexID = Shader.PropertyToID("_InteractionResultTex");
        static readonly int s_InteractionOrthoVID    = Shader.PropertyToID("_InteractionOrthoV");
        static readonly int s_InteractionOrthoPID    = Shader.PropertyToID("_InteractionOrthoP");
        static readonly int s_InteractionAreaPosID   = Shader.PropertyToID("_InteractionAreaPos");
        static readonly int s_InteractionAreaSizeID  = Shader.PropertyToID("_InteractionAreaSize");
        static readonly int s_InteractionResID       = Shader.PropertyToID("_InteractionResolution");

        // ── 运行时 ──────────────────────────────────────────────

        private RenderTexture              _interactionRT;
        private RenderTexture              _resultRT;
        private IUniversalInteractionProcessor _processor;
        private Matrix4x4                  _orthoV, _orthoP;

        [ReadOnly]
        [SerializeField]
        private bool _initialized;

        // ── 公开属性 ────────────────────────────────────────────

        public static UniversalInteractionManager Instance { get; private set; }

        public int Resolution => _resolution;

        public Matrix4x4 OrthoViewMatrix       => _orthoV;
        public Matrix4x4 OrthoProjectionMatrix => _orthoP;

        // ════════════════════════════════════════════════════════
        //  生命周期
        // ════════════════════════════════════════════════════════

        void OnEnable()
        {
            Instance = this;
            FindProcessor();
            SyncMatrices();
            EnsureInteractionRT();
            EnsureResultRT();
            InitializeProcessor();
            SyncGlobalProperties();
            _initialized = true;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            ReleaseProcessor();
            ReleaseRT();
            _initialized = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_initialized) return;

            SyncMatrices();
            SyncGlobalProperties();

            if (Application.isPlaying && _processor != null)
                _processor.Process(Time.deltaTime);
        }

        void OnValidate()
        {
            SyncMatrices();
        }

        // ════════════════════════════════════════════════════════
        //  Processor 发现与初始化 — GetComponent 模式，无需硬编码切换
        // ════════════════════════════════════════════════════════

        private void FindProcessor()
        {
            if (_processor == null)
                _processor = GetComponent<IUniversalInteractionProcessor>();
        }

        private void InitializeProcessor()
        {
            if (_processor == null || _resultRT == null || _interactionRT == null) return;
            _processor.Initialize(_resolution, _interactionRT, _resultRT);
        }

        private void ReleaseProcessor()
        {
            _processor?.Release();
            _processor = null;
        }

        // ════════════════════════════════════════════════════════
        //  正交矩阵
        // ════════════════════════════════════════════════════════

        private void SyncMatrices()
        {
            var center = transform.position;
            var camPos = center + Vector3.up * (_orthoFar * 0.5f);
            _orthoV = Matrix4x4.LookAt(camPos, center, Vector3.forward);
            float hs = _areaSize * 0.5f;
            _orthoP = Matrix4x4.Ortho(-hs, hs, -hs, hs, _orthoNear, _orthoFar);
            _orthoP = GL.GetGPUProjectionMatrix(_orthoP, false);
        }

        // ════════════════════════════════════════════════════════
        //  RT 管理
        // ════════════════════════════════════════════════════════

        private void EnsureInteractionRT()
        {
            if (_interactionRT != null) return;
            _interactionRT = new RenderTexture(_resolution, _resolution, 0, RenderTextureFormat.R8)
            {
                name              = "_InteractionTex",
                filterMode        = FilterMode.Bilinear,
                wrapMode          = TextureWrapMode.Clamp,
                useMipMap         = false,
                autoGenerateMips  = false,
            };
            _interactionRT.Create();
            ClearRT(_interactionRT);
            if (_orthoCamera != null) _orthoCamera.targetTexture = _interactionRT;
        }

        private void EnsureResultRT()
        {
            if (_resultRT != null) return;
            _resultRT = new RenderTexture(_resolution, _resolution, 0, RenderTextureFormat.RFloat)
            {
                name              = "_InteractionResultTex",
                filterMode        = FilterMode.Bilinear,
                wrapMode          = TextureWrapMode.Clamp,
                useMipMap         = false,
                autoGenerateMips  = false,
                enableRandomWrite = true,
            };
            _resultRT.Create();
            ClearRT(_resultRT);
        }

        private void ReleaseRT()
        {
            if (_orthoCamera != null) _orthoCamera.targetTexture = null;

            if (_interactionRT != null)
            {
                _interactionRT.Release();
                if (Application.isPlaying) Destroy(_interactionRT);
                else                       DestroyImmediate(_interactionRT);
                _interactionRT = null;
            }
            if (_resultRT != null)
            {
                _resultRT.Release();
                if (Application.isPlaying) Destroy(_resultRT);
                else                       DestroyImmediate(_resultRT);
                _resultRT = null;
            }
        }

        private static void ClearRT(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }

        // ════════════════════════════════════════════════════════
        //  全局 Shader 属性 — 向后兼容所有现有 Shader
        // ════════════════════════════════════════════════════════

        private void SyncGlobalProperties()
        {
            if (_interactionRT != null)
                Shader.SetGlobalTexture(s_InteractionTexID, _interactionRT);

            if (_resultRT != null)
                Shader.SetGlobalTexture(s_InteractionResultTexID, _resultRT);

            Shader.SetGlobalMatrix(s_InteractionOrthoVID,  _orthoV);
            Shader.SetGlobalMatrix(s_InteractionOrthoPID,  _orthoP);
            Shader.SetGlobalVector(s_InteractionAreaPosID,  transform.position);
            Shader.SetGlobalFloat(s_InteractionAreaSizeID, _areaSize);
            Shader.SetGlobalInt(s_InteractionResID,        _resolution);
        }

        // ════════════════════════════════════════════════════════
        //  Editor
        // ════════════════════════════════════════════════════════

#if UNITY_EDITOR
        [CustomEditor(typeof(UniversalInteractionManager))]
        public class UniversalInteractionManagerEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var t = (UniversalInteractionManager)target;
                EditorGUILayout.Space();

                if (t._initialized)
                    EditorGUILayout.HelpBox("已初始化", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("未初始化", MessageType.Warning);

                // 通过反射读取私有字段用于调试显示
                var rtField = typeof(UniversalInteractionManager).GetField("_resultRT",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rtField != null)
                {
                    var resultRT = rtField.GetValue(t) as RenderTexture;
                    if (resultRT != null)
                        EditorGUILayout.ObjectField("RT_Result", resultRT, typeof(RenderTexture), false);
                }
            }
        }
#endif
    }
}
