using UnityEngine;

namespace Mine.Water
{
    /// <summary>
    /// FFT 波浪编排器 — 3 级 Cascade Phillips 频谱 → Displacement + Normal 纹理
    ///
    /// 每帧对 3 级 cascade 分别执行:
    ///   InitialSpectrum → TimeDependent → PrepareChoppy → IFFT2D → Merger
    ///
    /// 输出全局纹理供 Water.shader (Phase 2) 采样:
    ///   _WaveDisplacement0/1/2  — XYZ 位移
    ///   _WaveNormal0/1/2       — 世界空间法线
    ///
    /// 使用方式: 挂载到场景 Water 物体，在 Inspector 拖入 4 个 .compute 文件。
    /// </summary>
    [ExecuteAlways]
    public class FFTWaveOrchestrator : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════
        //  Inspector
        // ════════════════════════════════════════════════════════════

        [Header("Compute Shaders")]
        [SerializeField] private ComputeShader _initialSpectrumCS;
        [SerializeField] private ComputeShader _timeDependentCS;
        [SerializeField] private ComputeShader _ifft2DCS;
        [SerializeField] private ComputeShader _mergerCS;

        [Header("Cascade 设置 (3 级)")]
        [SerializeField] private float[] _cascadeScales     = { 40f, 8f, 1.6f };
        [SerializeField] private float[] _cascadeAmplitudes  = { 0.3f, 0.15f, 0.05f };

        [Header("波浪参数")]
        [SerializeField] private float   _windSpeed    = 10f;
        [SerializeField] private Vector2 _windDirection = new Vector2(1f, 0.3f);
        [SerializeField] private float   _choppyScale  = 0.3f;
        [SerializeField] private float   _heightScale  = 0.5f;
        [SerializeField] private float   _gravity      = 9.81f;

        // ════════════════════════════════════════════════════════════
        //  Shader Property IDs
        // ════════════════════════════════════════════════════════════

        private static readonly int s_NoiseID         = Shader.PropertyToID("_Noise");
        private static readonly int s_H0ID            = Shader.PropertyToID("_H0");
        private static readonly int s_HtID            = Shader.PropertyToID("_Ht");
        private static readonly int s_ChoppySpecID    = Shader.PropertyToID("_ChoppySpec");
        private static readonly int s_InputID         = Shader.PropertyToID("_Input");
        private static readonly int s_OutputID        = Shader.PropertyToID("_Output");
        private static readonly int s_HeightTexID     = Shader.PropertyToID("_HeightTex");
        private static readonly int s_ChoppyXTexID    = Shader.PropertyToID("_ChoppyXTex");
        private static readonly int s_ChoppyYTexID    = Shader.PropertyToID("_ChoppyYTex");
        private static readonly int s_DisplacementID  = Shader.PropertyToID("_Displacement");
        private static readonly int s_NormalID        = Shader.PropertyToID("_Normal");

        private static readonly int s_AmplitudeID     = Shader.PropertyToID("_Amplitude");
        private static readonly int s_WindSpeedID     = Shader.PropertyToID("_WindSpeed");
        private static readonly int s_WindDirectionID = Shader.PropertyToID("_WindDirection");
        private static readonly int s_LengthScaleID   = Shader.PropertyToID("_LengthScale");
        private static readonly int s_GravityID       = Shader.PropertyToID("_Gravity");
        private static readonly int s_ResolutionID    = Shader.PropertyToID("_Resolution");
        private static readonly int s_TimeID          = Shader.PropertyToID("_Time");
        private static readonly int s_DirectionID     = Shader.PropertyToID("_Direction");
        private static readonly int s_InvID           = Shader.PropertyToID("_Inv");
        private static readonly int s_ChoppyAxisID    = Shader.PropertyToID("_ChoppyAxis");
        private static readonly int s_ChoppyScaleID   = Shader.PropertyToID("_ChoppyScale");
        private static readonly int s_HeightScaleID   = Shader.PropertyToID("_HeightScale");
        private static readonly int s_PatchSizeID     = Shader.PropertyToID("_PatchSize");

        // 全局纹理 ID（供 Water.shader 采样）
        private static readonly int[] s_WaveDisplacementIDs =
        {
            Shader.PropertyToID("_WaveDisplacement0"),
            Shader.PropertyToID("_WaveDisplacement1"),
            Shader.PropertyToID("_WaveDisplacement2"),
        };
        private static readonly int[] s_WaveNormalIDs =
        {
            Shader.PropertyToID("_WaveNormal0"),
            Shader.PropertyToID("_WaveNormal1"),
            Shader.PropertyToID("_WaveNormal2"),
        };
        private static readonly int[] s_WavePatchSizeIDs =
        {
            Shader.PropertyToID("_WavePatchSize0"),
            Shader.PropertyToID("_WavePatchSize1"),
            Shader.PropertyToID("_WavePatchSize2"),
        };

        // ════════════════════════════════════════════════════════════
        //  运行时状态
        // ════════════════════════════════════════════════════════════

        private const int RESOLUTION = 256;

        private Texture2D _noiseTex;

        // 共享 RT（跨 cascade 复用，ARGBFloat 与 HtTex 匹配）
        private RenderTexture _pingA;
        private RenderTexture _pingB;
        private RenderTexture _heightRaw;
        private RenderTexture _choppyXRaw;
        private RenderTexture _choppyYRaw;

        private struct CascadeData
        {
            public RenderTexture h0Tex;
            public RenderTexture htTex;
            public RenderTexture displacementTex;
            public RenderTexture normalTex;
        }
        private CascadeData[] _cascades;

        private int _kInitSpectrum;
        private int _kTimeDependent;
        private int _kPrepareChoppy;
        private int _kIFFT;
        private int _kMerger;

        private bool _initialized;

        // ════════════════════════════════════════════════════════════
        //  生命周期
        // ════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (_initialSpectrumCS == null || _timeDependentCS == null ||
                _ifft2DCS == null || _mergerCS == null)
            {
                Debug.LogWarning("FFTWaveOrchestrator: 缺少 ComputeShader 引用，已禁用。");
                enabled = false;
                return;
            }

            if (_cascadeScales == null || _cascadeScales.Length < 3)
            {
                Debug.LogWarning("FFTWaveOrchestrator: _cascadeScales 需要 3 个元素。");
                enabled = false;
                return;
            }

            InitKernels();
            CreateNoiseTexture();
            CreateRenderTextures();
            GenerateInitialSpectra();
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            DispatchFrame();
        }

        private void OnDisable()
        {
            ReleaseAll();
            _initialized = false;
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        // ════════════════════════════════════════════════════════════
        //  初始化
        // ════════════════════════════════════════════════════════════

        private void InitKernels()
        {
            _kInitSpectrum  = _initialSpectrumCS.FindKernel("CSMain");
            _kTimeDependent = _timeDependentCS.FindKernel("CSMain");
            _kPrepareChoppy = _timeDependentCS.FindKernel("PrepareChoppy");
            _kIFFT          = _ifft2DCS.FindKernel("CSMain");
            _kMerger        = _mergerCS.FindKernel("CSMain");
        }

        private void CreateNoiseTexture()
        {
            _noiseTex = new Texture2D(RESOLUTION, RESOLUTION, TextureFormat.RGBAFloat, false, true)
            {
                name = "FFT_GaussianNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };

            var pixels = new Color[RESOLUTION * RESOLUTION];
            var rng = new System.Random(42);
            for (int i = 0; i < pixels.Length; i++)
            {
                float u1 = (float)rng.NextDouble();
                float u2 = (float)rng.NextDouble();
                float g1 = Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(u1, 0.0001f))) * Mathf.Cos(2f * Mathf.PI * u2);
                float g2 = Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(u1, 0.0001f))) * Mathf.Sin(2f * Mathf.PI * u2);
                pixels[i] = new Color(g1, g2, 0, 0);
            }
            _noiseTex.SetPixels(pixels);
            _noiseTex.Apply();
        }

        private void CreateRenderTextures()
        {
            // 共享 RT — 用 ARGBFloat 与 HtTex 格式一致，CopyTexture 无需转换
            _pingA      = CreateRT("FFT_PingA");
            _pingB      = CreateRT("FFT_PingB");
            _heightRaw  = CreateRT("FFT_HeightRaw");
            _choppyXRaw = CreateRT("FFT_ChoppyXRaw");
            _choppyYRaw = CreateRT("FFT_ChoppyYRaw");

            _cascades = new CascadeData[3];
            for (int i = 0; i < 3; i++)
            {
                _cascades[i] = new CascadeData
                {
                    h0Tex           = CreateRT($"FFT_C{i}_H0"),
                    htTex           = CreateRT($"FFT_C{i}_Ht"),
                    displacementTex = CreateRT($"FFT_C{i}_Disp"),
                    normalTex       = CreateRT($"FFT_C{i}_Norm"),
                };
            }
        }

        private static RenderTexture CreateRT(string name)
        {
            var rt = new RenderTexture(RESOLUTION, RESOLUTION, 0, RenderTextureFormat.ARGBFloat)
            {
                name              = name,
                enableRandomWrite = true,
                wrapMode          = TextureWrapMode.Repeat,
                filterMode        = FilterMode.Bilinear,
                useMipMap         = false,
                autoGenerateMips  = false,
            };
            rt.Create();
            return rt;
        }

        private void GenerateInitialSpectra()
        {
            // H0 只生成一次（噪声不变，时间演化在 TimeDependent 中）
            for (int c = 0; c < 3; c++)
            {
                _initialSpectrumCS.SetTexture(_kInitSpectrum, s_NoiseID, _noiseTex);
                _initialSpectrumCS.SetTexture(_kInitSpectrum, s_H0ID, _cascades[c].h0Tex);
                _initialSpectrumCS.SetFloat(s_AmplitudeID, _cascadeAmplitudes[c]);
                _initialSpectrumCS.SetFloat(s_WindSpeedID, _windSpeed);
                _initialSpectrumCS.SetVector(s_WindDirectionID, _windDirection.normalized);
                _initialSpectrumCS.SetFloat(s_LengthScaleID, _cascadeScales[c]);
                _initialSpectrumCS.SetFloat(s_GravityID, _gravity);
                _initialSpectrumCS.SetInt(s_ResolutionID, RESOLUTION);
                Dispatch(_initialSpectrumCS, _kInitSpectrum);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  每帧 Dispatch
        // ════════════════════════════════════════════════════════════

        private void DispatchFrame()
        {
            float t = Time.time;

            for (int c = 0; c < 3; c++)
            {
                var cd    = _cascades[c];
                float scl = _cascadeScales[c];

                // Step 1: TimeDependent — H0 → Ht
                RunTimeDependent(cd.h0Tex, cd.htTex, scl, t);

                // Step 2: Height 通道
                Graphics.CopyTexture(cd.htTex, _pingA);
                RunIFFT();
                Graphics.CopyTexture(_pingA, _heightRaw);

                // Step 3: ChoppyX 通道
                RunPrepareChoppy(cd.htTex, _pingA, scl, 0);
                RunIFFT();
                Graphics.CopyTexture(_pingA, _choppyXRaw);

                // Step 4: ChoppyY 通道
                RunPrepareChoppy(cd.htTex, _pingA, scl, 1);
                RunIFFT();
                Graphics.CopyTexture(_pingA, _choppyYRaw);

                // Step 5: Merger → Displacement + Normal
                RunMerger(cd.displacementTex, cd.normalTex, scl);

                // Step 6: 注册全局纹理
                Shader.SetGlobalTexture(s_WaveDisplacementIDs[c], cd.displacementTex);
                Shader.SetGlobalTexture(s_WaveNormalIDs[c],       cd.normalTex);
                Shader.SetGlobalFloat(s_WavePatchSizeIDs[c],      scl);
            }
        }

        // ── 子步骤 ────────────────────────────────────────────────

        private void RunTimeDependent(RenderTexture h0, RenderTexture ht, float scale, float t)
        {
            _timeDependentCS.SetTexture(_kTimeDependent, s_H0ID, h0);
            _timeDependentCS.SetTexture(_kTimeDependent, s_HtID, ht);
            _timeDependentCS.SetFloat(s_TimeID, t);
            _timeDependentCS.SetFloat(s_LengthScaleID, scale);
            _timeDependentCS.SetFloat(s_GravityID, _gravity);
            _timeDependentCS.SetInt(s_ResolutionID, RESOLUTION);
            Dispatch(_timeDependentCS, _kTimeDependent);
        }

        private void RunPrepareChoppy(RenderTexture ht, RenderTexture choppyOut, float scale, uint axis)
        {
            _timeDependentCS.SetTexture(_kPrepareChoppy, s_HtID, ht);
            _timeDependentCS.SetTexture(_kPrepareChoppy, s_ChoppySpecID, choppyOut);
            _timeDependentCS.SetFloat(s_LengthScaleID, scale);
            _timeDependentCS.SetInt(s_ResolutionID, RESOLUTION);
            _timeDependentCS.SetInt(s_ChoppyAxisID, (int)axis);
            Dispatch(_timeDependentCS, _kPrepareChoppy);
        }

        /// <summary>
        /// 对 _pingA 做 2D IFFT，结果写回 _pingA。
        /// 输入数据必须在 _pingA 中。水平→_pingB，垂直→_pingA。
        /// </summary>
        private void RunIFFT()
        {
            _ifft2DCS.SetInt(s_InvID, 1);
            _ifft2DCS.SetInt(s_ResolutionID, RESOLUTION);

            // 水平: 每行一个 thread group, 256 threads/group
            _ifft2DCS.SetInt(s_DirectionID, 0);
            _ifft2DCS.SetTexture(_kIFFT, s_InputID, _pingA);
            _ifft2DCS.SetTexture(_kIFFT, s_OutputID, _pingB);
            _ifft2DCS.Dispatch(_kIFFT, 1, RESOLUTION, 1);

            // 垂直: 每列一个 thread group
            _ifft2DCS.SetInt(s_DirectionID, 1);
            _ifft2DCS.SetTexture(_kIFFT, s_InputID, _pingB);
            _ifft2DCS.SetTexture(_kIFFT, s_OutputID, _pingA);
            _ifft2DCS.Dispatch(_kIFFT, RESOLUTION, 1, 1);
        }

        private void RunMerger(RenderTexture displacement, RenderTexture normal, float scale)
        {
            _mergerCS.SetTexture(_kMerger, s_HeightTexID,   _heightRaw);
            _mergerCS.SetTexture(_kMerger, s_ChoppyXTexID,  _choppyXRaw);
            _mergerCS.SetTexture(_kMerger, s_ChoppyYTexID,  _choppyYRaw);
            _mergerCS.SetTexture(_kMerger, s_DisplacementID, displacement);
            _mergerCS.SetTexture(_kMerger, s_NormalID,       normal);
            _mergerCS.SetFloat(s_ChoppyScaleID, _choppyScale);
            _mergerCS.SetFloat(s_HeightScaleID, _heightScale);
            _mergerCS.SetFloat(s_PatchSizeID, scale);
            _mergerCS.SetInt(s_ResolutionID, RESOLUTION);
            Dispatch(_mergerCS, _kMerger);
        }

        // ════════════════════════════════════════════════════════════
        //  辅助
        // ════════════════════════════════════════════════════════════

        private void Dispatch(ComputeShader cs, int kernel)
        {
            int tg = RESOLUTION / 8;
            cs.Dispatch(kernel, tg, tg, 1);
        }

        private void ReleaseAll()
        {
            void Rel(ref RenderTexture rt)
            {
                if (rt != null)
                {
                    rt.Release();
                    if (Application.isPlaying) Destroy(rt);
                    else DestroyImmediate(rt);
                    rt = null;
                }
            }

            Rel(ref _pingA);
            Rel(ref _pingB);
            Rel(ref _heightRaw);
            Rel(ref _choppyXRaw);
            Rel(ref _choppyYRaw);

            if (_cascades != null)
            {
                for (int i = 0; i < _cascades.Length; i++)
                {
                    Rel(ref _cascades[i].h0Tex);
                    Rel(ref _cascades[i].htTex);
                    Rel(ref _cascades[i].displacementTex);
                    Rel(ref _cascades[i].normalTex);
                }
            }

            if (_noiseTex != null)
            {
                if (Application.isPlaying) Destroy(_noiseTex);
                else DestroyImmediate(_noiseTex);
                _noiseTex = null;
            }
        }
    }
}
