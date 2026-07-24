using UnityEngine;

namespace Mine.Water
{
    /// <summary>
    /// WaveFoam 预计算管理器：每帧将 ComputeShader 生成的泡沫遮罩写入全局 RenderTexture，
    /// 供 Water.shader 采样，将 per-fragment 的 4 层 octave 纹理采样降为 1 次。
    /// </summary>
    ///
    /// <remarks>
    /// 使用方式：挂载到场景中的 Water 物体上，在 Inspector 中拖入 WaveFoam.compute 和泡沫贴图。
    /// 纹理以世界空间平面投影 + Repeat 平铺方式工作，_worldTexSize 控制单 tile 覆盖范围。
    /// </remarks>
    [ExecuteAlways]
    public class WaveFoamManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────

        [Header("Compute")]
        [SerializeField] private ComputeShader _waveFoamCS;
        [SerializeField] private Texture2D    _foamTex;

        [Header("纹理设置")]
        [SerializeField] private int   _resolution   = 256;
        [SerializeField] private float _worldTexSize = 10f;

        // ── Shader Property IDs ────────────────────────────────

        private static readonly int s_FoamTexID        = Shader.PropertyToID("_FoamTex");
        private static readonly int s_FoamScaleID       = Shader.PropertyToID("_FoamScale");
        private static readonly int s_FoamSpeedID       = Shader.PropertyToID("_FoamSpeed");
        private static readonly int s_FoamIntensityID   = Shader.PropertyToID("_FoamIntensity");
        private static readonly int s_TimeID            = Shader.PropertyToID("_Time");
        private static readonly int s_WorldTexSizeID    = Shader.PropertyToID("_WaveFoamWorldTexSize");
        private static readonly int s_ResolutionID      = Shader.PropertyToID("_Resolution");
        private static readonly int s_ResultID          = Shader.PropertyToID("_Result");
        private static readonly int s_WaveFoamTexID     = Shader.PropertyToID("_WaveFoamTex");

        // ── 运行时状态 ────────────────────────────────────────

        private RenderTexture _foamRT;
        private int           _kernelIndex;
        private bool          _initialized;

        // ════════════════════════════════════════════════════════════
        //  生命周期
        // ════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (_waveFoamCS == null || _foamTex == null)
            {
                Debug.LogWarning("WaveFoamManager: ComputeShader 或 FoamTex 未赋值，已禁用。");
                enabled = false;
                return;
            }

            InitRenderTexture();
            _kernelIndex = _waveFoamCS.FindKernel("CSMain");
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            // Edit 模式下仅维持 RT 存在（保证 Shader 不会采样到未绑定纹理），
            // 不 Dispatch Compute Shader，Scene 视图无泡沫为预期行为。
            if (Application.isPlaying)
                DispatchFoam();
        }

        private void OnDisable()
        {
            if (_initialized)
                ReleaseRT();
            _initialized = false;
        }

        private void OnDestroy()
        {
            ReleaseRT();
        }

        // ════════════════════════════════════════════════════════════
        //  初始化 — 创建全局泡沫纹理
        // ════════════════════════════════════════════════════════════

        private void InitRenderTexture()
        {
            _foamRT = new RenderTexture(_resolution, _resolution, 0, RenderTextureFormat.RFloat)
            {
                name             = "WaveFoamRT",
                enableRandomWrite = true,
                wrapMode         = TextureWrapMode.Repeat,
                filterMode       = FilterMode.Bilinear,
                useMipMap        = false,
                autoGenerateMips = false,
            };
            _foamRT.Create();

            // 注册为全局纹理，所有使用 Water.shader 的材质均可采样
            Shader.SetGlobalTexture(s_WaveFoamTexID, _foamRT);
            Shader.SetGlobalFloat(s_WorldTexSizeID, _worldTexSize);
        }

        // ════════════════════════════════════════════════════════════
        //  每帧 Dispatch — 计算泡沫值写入 RenderTexture
        // ════════════════════════════════════════════════════════════

        private void DispatchFoam()
        {
            _waveFoamCS.SetTexture(_kernelIndex, s_ResultID, _foamRT);
            _waveFoamCS.SetTexture(_kernelIndex, s_FoamTexID, _foamTex);
            _waveFoamCS.SetFloat(s_TimeID, Time.time);

            // 与 Water.shader 材质参数保持同步（从第一个使用此 Shader 的材质读取）
            // 如果场景中有多个 Water 材质且参数不同，需自行调整取值逻辑。
            var waterMat = GetWaterMaterial();
            if (waterMat != null)
            {
                _waveFoamCS.SetFloat(s_FoamScaleID,     waterMat.GetFloat("_FoamScale"));
                _waveFoamCS.SetFloat(s_FoamSpeedID,      waterMat.GetFloat("_FoamSpeed"));
                _waveFoamCS.SetFloat(s_FoamIntensityID,  waterMat.GetFloat("_FoamIntensity"));
            }

            _waveFoamCS.SetFloat(s_WorldTexSizeID, _worldTexSize);
            _waveFoamCS.SetInt(s_ResolutionID, _resolution);

            int threadGroups = Mathf.CeilToInt(_resolution / 8f);
            _waveFoamCS.Dispatch(_kernelIndex, threadGroups, threadGroups, 1);
        }

        // ════════════════════════════════════════════════════════════
        //  辅助 — 获取场景中 Water 材质引用以同步参数
        // ════════════════════════════════════════════════════════════

        private Material GetWaterMaterial()
        {
            // 优先使用自身 Renderer 的材质
            var r = GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null &&
                r.sharedMaterial.shader.name == "Custom/Water")
            {
                return r.sharedMaterial;
            }

            // 回退：查找场景中任意 Water 材质
            var allRenderers = FindObjectsOfType<Renderer>();
            foreach (var renderer in allRenderers)
            {
                var mats = renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat != null && mat.shader.name == "Custom/Water")
                        return mat;
                }
            }

            return null;
        }

        // ════════════════════════════════════════════════════════════
        //  清理
        // ════════════════════════════════════════════════════════════

        private void ReleaseRT()
        {
            if (_foamRT != null)
            {
                _foamRT.Release();
                if (Application.isPlaying)
                    Destroy(_foamRT);
                else
                    DestroyImmediate(_foamRT);
                _foamRT = null;
            }
        }
    }
}
