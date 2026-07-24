using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// PCSS 软阴影渲染管线：4 级联 PSSM Atlas + Blocker Search + Penumbra 估算 + 变核 PCF + 双边模糊。
/// 在 URP Renderer 的 Renderer Features 中添加，配合 CustomShadowCaster.shader 和 PCSS.shader 使用。
/// </summary>
public class PCSSFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Resources")]
        public Shader shadowCasterShader;
        public Shader pcssShader;
        public ComputeShader pcssComputeShader;

        [Header("Shadow Map")]
        [Range(256, 4096)] public int shadowMapResolution = 2048;

        [Header("Cascades")]
        [Range(1, 8)] public int cascadeCount = 4;
        [Range(0f, 1f)] public float pssmLambda = 0.75f;
        [Range(10f, 200f)] public float shadowDistance = 50f;

        [Header("PCSS")]
        public Quality quality = Quality.High;
        [Range(0.1f, 5f)] public float lightSize = 1.0f;
        [Range(0.1f, 2f)] public float softness = 1.0f;
        public enum Quality { Low, Medium, High }

        [Header("Shadow Bias")]
        [Range(0f, 2f)] public float depthBias  = 0.5f;
        [Range(0f, 2f)] public float normalBias = 0.4f;

        [Header("Blur")]
        public bool enableBlur = true;
        [Range(0f, 5f)] public float blurScale = 1.0f;

        [Header("Debug")]
        public bool showShadowMap = true;

        internal static readonly int ShadowCacheTexID = Shader.PropertyToID("_PCSS_ShadowCacheTex");
        internal static readonly int LightDirectionID  = Shader.PropertyToID("_LightDirection");
        internal static readonly int DepthBiasID       = Shader.PropertyToID("_ShadowDepthBias");
        internal static readonly int NormalBiasID      = Shader.PropertyToID("_ShadowNormalBias");
    }

    // ════════════════════════════════════════════════════════════
    //  CustomShadowCasterPass — 级联 Atlas 渲染
    // ════════════════════════════════════════════════════════════
    class CustomShadowCasterPass : ScriptableRenderPass
    {
        Settings m_S;
        Material m_Mat;

        RenderTexture m_ShadowRT;
        RTHandle      m_ShadowHandle;
        int           m_AtlasRes, m_TileRes;

        // 级联数据（供 PCSSPass 读取）
        public Matrix4x4[] CascadeViewProj;
        public Vector4     CascadeSplits;
        public Vector4     CascadeHalfWidths;
        public Vector4     CascadeZDistances;
        public Vector4[]   CascadeOffsets;
        public int         CascadeCount;
        public RTHandle    shadowHandle => m_ShadowHandle;
        public RenderTexture shadowRT => m_ShadowRT;

        class RasterPassData
        {
            public Matrix4x4[] cascadeView;
            public Matrix4x4[] cascadeProj;
            public Matrix4x4   camView, camProj;
            public RendererListHandle[] rendererLists;
            public int tileRes;
        }

        public CustomShadowCasterPass(Settings s, Material mat)
        {
            m_S = s; m_Mat = mat;
            renderPassEvent = RenderPassEvent.AfterRenderingShadows;
            profilingSampler = new ProfilingSampler("PCSS Caster");
            CascadeViewProj = new Matrix4x4[8];
            CascadeOffsets  = new Vector4[8];
        }

        void EnsureRT(int atlasRes, int tileRes)
        {
            if (m_ShadowRT != null && m_AtlasRes == atlasRes && m_TileRes == tileRes)
                return;

            m_ShadowRT?.Release();
            m_ShadowHandle?.Release();

            m_ShadowRT = new RenderTexture(atlasRes, atlasRes, 0, RenderTextureFormat.RFloat);
            m_ShadowRT.filterMode = FilterMode.Point;
            m_ShadowRT.wrapMode = TextureWrapMode.Clamp;
            m_ShadowRT.Create();
            m_ShadowHandle = RTHandles.Alloc(m_ShadowRT);
            m_AtlasRes = atlasRes;
            m_TileRes  = tileRes;
        }

        // ════════════════════════════════════════════════════════════
        //  球体包围盒 + 光源正交投影（接受级联 near/far）
        // ════════════════════════════════════════════════════════════
        void ComputeShadowProjection(
            Camera cam, Light light, float cascadeNear, float cascadeFar,
            int tileRes, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out Vector3 camPos)
        {
            Transform t = cam.transform;
            Vector3 fwd = t.forward;
            float fov = cam.fieldOfView * Mathf.Deg2Rad;
            float aspect = cam.aspect;

            // ── 固定包围盒半径（Unity 做法：由 cascade 距离 + FOV 决定，不依赖当帧 frustum 角点）──
            // 旋转时 frustum 角点变化 → halfW/H 变化 → 投影 scale 飘移 → 抖动。
            // 改用 cascadeFar 处的视锥对角线作为固定半径，仅在穿越 split/FOV 变更时才变。
            float halfFovV = Mathf.Tan(fov * 0.5f);
            float halfFovH = halfFovV * aspect;
            float frustumDiag = cascadeFar * Mathf.Sqrt(halfFovV * halfFovV + halfFovH * halfFovH);
            float halfExtent = frustumDiag + 1f;

            // ── 角点仍用于确定光空间中心和深度范围 ──
            float camNear = cam.nearClipPlane;
            float nearH = halfFovV * camNear;
            float nearW = nearH * aspect;
            Vector3 rgt = t.right, up = t.up;
            Vector3 dBL = fwd * camNear + rgt * (-nearW) + up * (-nearH);
            Vector3 dTR = fwd * camNear + rgt * ( nearW) + up * ( nearH);
            Vector3 dBR = fwd * camNear + rgt * ( nearW) + up * (-nearH);
            Vector3 dTL = fwd * camNear + rgt * (-nearW) + up * ( nearH);
            float sN = cascadeNear / camNear, sF = cascadeFar / camNear;
            Vector3[] corners = {
                t.position + dBL * sN, t.position + dTR * sN,
                t.position + dBR * sN, t.position + dTL * sN,
                t.position + dBL * sF, t.position + dTR * sF,
                t.position + dBR * sF, t.position + dTL * sF
            };

            Vector3 lightDir = light.transform.forward;
            Vector3 lightRgt = Vector3.Cross(lightDir, Vector3.up).normalized;
            if (lightRgt.sqrMagnitude < 0.001f)
                lightRgt = Vector3.Cross(lightDir, Vector3.forward).normalized;
            Vector3 lightUp = Vector3.Cross(lightRgt, lightDir).normalized;

            float camR = Vector3.Dot(t.position, lightRgt);
            float camU = Vector3.Dot(t.position, lightUp);
            float minD = float.MaxValue, maxD = float.MinValue;
            foreach (var c in corners)
            {
                float d = Vector3.Dot(c, lightDir);
                minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
            }

            float midD     = (minD + maxD) * 0.5f;  // frustom 几何中心（含所有可见物体）
            float backDist = cascadeFar * 8f;
            float zNear    = 0.1f;
            float zFar     = backDist * 2f;
            float halfW    = halfExtent;
            float halfH    = halfExtent;
            // ── 量化整个正交投影边界（texel-aligned ortho bounds）──
            // Floor 左边界、Ceil 右边界 → 向外扩 → 确保原范围被包含。
            Vector3 vX = -lightRgt, vY = lightUp, vZ = -lightDir;
            float worldPerTexel = halfW * 2f / tileRes;
            // 光空间 R 轴
            float bl = camR - halfW, br = camR + halfW;
            bl = Mathf.Floor(bl / worldPerTexel) * worldPerTexel;
            br = Mathf.Ceil (br / worldPerTexel) * worldPerTexel;
            float snapR = (bl + br) * 0.5f, snapHW = Mathf.Max((br - bl) * 0.5f, halfW);
            // 光空间 U 轴
            float bb = camU - halfH, bt = camU + halfH;
            bb = Mathf.Floor(bb / worldPerTexel) * worldPerTexel;
            bt = Mathf.Ceil (bt / worldPerTexel) * worldPerTexel;
            float snapU = (bb + bt) * 0.5f, snapHH = Mathf.Max((bt - bb) * 0.5f, halfH);
            Vector3 center = lightRgt * snapR + lightUp * snapU + lightDir * midD;

            Vector3 shadowCamPos = center - lightDir * backDist;
            camPos = shadowCamPos;
            viewMatrix = new Matrix4x4(
                new Vector4(vX.x, vY.x, vZ.x, 0),
                new Vector4(vX.y, vY.y, vZ.y, 0),
                new Vector4(vX.z, vY.z, vZ.z, 0),
                new Vector4(-Vector3.Dot(vX, shadowCamPos), -Vector3.Dot(vY, shadowCamPos), -Vector3.Dot(vZ, shadowCamPos), 1));
            projMatrix = Matrix4x4.Ortho(-snapHW, snapHW, -snapHH, snapHH, zNear, zFar);
        }

        // ── PSSM Split ──
        float[] ComputePSSMSplits(float nearP, float farP, int count, float lambda)
        {
            var splits = new float[count];
            for (int i = 0; i < count; i++)
            {
                float p = (i + 1f) / count;
                float logSplit = nearP * Mathf.Pow(farP / nearP, p);
                float uniSplit = nearP + (farP - nearP) * p;
                splits[i] = lambda * logSplit + (1f - lambda) * uniSplit;
            }
            return splits;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            if (m_Mat == null) return;

            Light mainLight = RenderSettings.sun;
            if (mainLight == null) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            Camera cam = cameraData.camera;

            int cascadeCount = m_S.cascadeCount;
            int tileRes = m_S.shadowMapResolution / 2; // 2×2 atlas 布局
            int atlasRes = tileRes * 2;
            EnsureRT(atlasRes, tileRes);

            float shadowDist = m_S.shadowDistance;

            // ── PSSM split ──
            float[] splits = ComputePSSMSplits(cam.nearClipPlane, shadowDist, cascadeCount, m_S.pssmLambda);
            CascadeCount = cascadeCount;

            // ── 每级联计算投影，同时找最宽级联做 culling ──
            var cascadeView  = new Matrix4x4[cascadeCount];
            var cascadeProj  = new Matrix4x4[cascadeCount];
            var cascadeCamPos = new Vector3[cascadeCount];
            var cascadeHalfW  = new float[cascadeCount];
            var cascadeZDist  = new float[cascadeCount];
            float maxHalfW = 0f, maxHalfH = 0f;
            int widestCascade = 0;

            for (int ci = 0; ci < cascadeCount; ci++)
            {
                float cn = ci == 0 ? cam.nearClipPlane : splits[ci - 1];
                float cf = splits[ci];

                ComputeShadowProjection(cam, mainLight, cn, cf, tileRes,
                    out cascadeView[ci], out cascadeProj[ci], out cascadeCamPos[ci]);

                // 记录最宽级联（用于全量 culling）
                float extR = Mathf.Abs(1f / cascadeProj[ci].m00);
                float extU = Mathf.Abs(1f / cascadeProj[ci].m11);
                if (extR > maxHalfW || extU > maxHalfH)
                {
                    maxHalfW = Mathf.Max(maxHalfW, extR);
                    maxHalfH = Mathf.Max(maxHalfH, extU);
                    widestCascade = ci;
                }
            }

            // ── 存储级联数据 ──
            // 参考 Unity ShadowUtils.GetShadowTransform：
            // 1) reversed-Z 平台反转投影矩阵 z 分量
            // 2) 把 [-1,1]→[0,1] 的 scale-bias 烘焙进矩阵 → shader 只需除 w
            var scaleBias = Matrix4x4.identity;
            scaleBias.m00 = 0.5f; scaleBias.m11 = 0.5f; scaleBias.m22 = 0.5f;
            scaleBias.m03 = 0.5f; scaleBias.m13 = 0.5f; scaleBias.m23 = 0.5f;

            CascadeViewProj = new Matrix4x4[cascadeCount];
            CascadeOffsets  = new Vector4[cascadeCount];
            for (int ci = 0; ci < cascadeCount; ci++)
            {
                var proj = cascadeProj[ci];
                if (SystemInfo.usesReversedZBuffer)
                {
                    proj.m20 = -proj.m20; proj.m21 = -proj.m21;
                    proj.m22 = -proj.m22; proj.m23 = -proj.m23;
                }
                CascadeViewProj[ci] = scaleBias * (proj * cascadeView[ci]);
                int col = ci % 2, row = ci / 2;
                CascadeOffsets[ci] = new Vector4(
                    col * 0.5f, row * 0.5f, 0.5f, 0f);
            }
            // halfW 用固定参照（参考文章 _CascadeShadowSplitSpheres[i].w）
            // zDist 从投影矩阵提取（与旋转基本无关）
            float fovHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float diagFactor = Mathf.Sqrt(fovHalf * fovHalf * (1f + cam.aspect * cam.aspect));
            for (int ci = 0; ci < cascadeCount; ci++)
            {
                cascadeHalfW[ci] = splits[ci] * diagFactor + 1f;
                cascadeZDist[ci] = -2f / cascadeProj[ci].m22;
            }
            CascadeHalfWidths = new Vector4(
                cascadeHalfW.Length > 0 ? cascadeHalfW[0] : 0,
                cascadeHalfW.Length > 1 ? cascadeHalfW[1] : 0,
                cascadeHalfW.Length > 2 ? cascadeHalfW[2] : 0,
                cascadeHalfW.Length > 3 ? cascadeHalfW[3] : 0);
            CascadeZDistances = new Vector4(
                cascadeZDist.Length > 0 ? cascadeZDist[0] : 0,
                cascadeZDist.Length > 1 ? cascadeZDist[1] : 0,
                cascadeZDist.Length > 2 ? cascadeZDist[2] : 0,
                cascadeZDist.Length > 3 ? cascadeZDist[3] : 0);

            CascadeSplits = new Vector4(
                cascadeCount > 0 ? splits[0] : 0f,
                cascadeCount > 1 ? splits[1] : 0f,
                cascadeCount > 2 ? splits[2] : 0f,
                cascadeCount > 3 ? splits[3] : 0f);

            // ── 设置阴影偏移 ──
            m_Mat.SetVector(Settings.LightDirectionID, mainLight.transform.forward);
            m_Mat.SetFloat(Settings.DepthBiasID, m_S.depthBias);
            m_Mat.SetFloat(Settings.NormalBiasID, m_S.normalBias);

            TextureHandle shadowTH = graph.ImportTexture(m_ShadowHandle);

            RenderTextureDescriptor depthDesc = new RenderTextureDescriptor(
                atlasRes, atlasRes, RenderTextureFormat.Depth, 16, 0);
            TextureHandle depthTH = UniversalRenderer.CreateRenderGraphTexture(
                graph, depthDesc, "_PCSS_ShadowDepth", false);

            // ── 光源视角 Culling（用最宽级联覆盖全场景） ──
            CullContextData cullCtx = frameData.Get<CullContextData>();
            cam.TryGetCullingParameters(false, out var cullParams);
            cullParams.cullingMatrix = cascadeProj[widestCascade] * cascadeView[widestCascade];
            cullParams.isOrthographic = true;
            cullParams.origin = cascadeCamPos[widestCascade];
            var lightPlanes = GeometryUtility.CalculateFrustumPlanes(cullParams.cullingMatrix);
            int planeCount = Mathf.Min(lightPlanes.Length, ScriptableCullingParameters.maximumCullingPlaneCount);
            for (int i = 0; i < planeCount; i++)
                cullParams.SetCullingPlane(i, lightPlanes[i]);
            cullParams.cullingPlaneCount = planeCount;
            var lightCull = cullCtx.Cull(ref cullParams);

            var sorting = new SortingSettings(cam);
            var drawSettings = new DrawingSettings(new ShaderTagId("ShadowCaster"), sorting);
            drawSettings.overrideMaterial = m_Mat;
            drawSettings.overrideMaterialPassIndex = 0;
            var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
            var rlList = new RendererListHandle[cascadeCount];
            for (int ci = 0; ci < cascadeCount; ci++)
            {
                var rlp = new RendererListParams(lightCull, drawSettings, filterSettings);
                rlList[ci] = graph.CreateRendererList(rlp);
            }

            Matrix4x4 camView = cam.worldToCameraMatrix;
            Matrix4x4 camProj = cam.projectionMatrix;

            using (var builder = graph.AddRasterRenderPass<RasterPassData>(
                "PCSS Cascade", out var pd, profilingSampler))
            {
                pd.cascadeView = cascadeView;
                pd.cascadeProj = cascadeProj;
                pd.camView     = camView;
                pd.camProj     = camProj;
                pd.rendererLists = rlList;
                pd.tileRes     = tileRes;

                builder.SetRenderAttachment(shadowTH, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(depthTH, AccessFlags.Write);
                foreach (var rh in rlList)
                    builder.UseRendererList(rh);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(shadowTH, Settings.ShadowCacheTexID);

                builder.SetRenderFunc((RasterPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(RTClearFlags.Color | RTClearFlags.Depth,
                        Color.black, 1f, 0);

                    int count = data.cascadeView.Length;
                    for (int ci = 0; ci < count; ci++)
                    {
                        int col = ci % 2, row = ci / 2;
                        int vpX = col * data.tileRes;
                        int vpY = row * data.tileRes;
                        ctx.cmd.SetViewport(new Rect(vpX, vpY, data.tileRes, data.tileRes));
                        ctx.cmd.SetViewProjectionMatrices(data.cascadeView[ci], data.cascadeProj[ci]);
                        ctx.cmd.DrawRendererList(data.rendererLists[ci]);
                    }

                    ctx.cmd.SetViewProjectionMatrices(data.camView, data.camProj);
                });
            }
        }

        public void Release()
        {
            m_ShadowHandle?.Release();
            m_ShadowRT?.Release();
            m_ShadowRT = null; m_ShadowHandle = null;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  PCSSPass — 屏幕空间阴影比较
    // ════════════════════════════════════════════════════════════
    class PCSSPass : ScriptableRenderPass
    {
        Settings m_S;
        Material m_BlurMat;
        ComputeShader m_CS;
        int m_CSKernel;
        CustomShadowCasterPass m_Caster;

        RenderTexture m_SoftShadowRT;
        RTHandle      m_SoftShadowHandle;
        RenderTexture m_BlurTempRT;
        RTHandle      m_BlurTempHandle;
        int           m_RTWidth, m_RTHeight;

        class PassData
        {
            public ComputeShader cs;
            public int csKernel;
            public Material blurMat;
            public RenderTexture softShadowRT;
            public RenderTexture shadowCacheRT;
            public TextureHandle source, softShadowTH, blurTempTH;
            public Matrix4x4[] cascadeVP;
            public Vector4 splits;
            public Vector4[] cascadeOffsets;
            public int cascadeCount;
            public Vector4 cascadeHalfWidths;
            public Vector4 cascadeZDistances;
            public Settings.Quality quality;
            public float lightSize, softness;
            public Vector4 lightDirection;
            public Vector4 screenSize;
            public Vector4 frustumRay0, frustumRay1, frustumRay2, frustumRay3;
            public Vector4 zBufferParams;
            public Vector4 worldSpaceCameraPos;
            public bool enableBlur;
            public float blurScale;
            public bool showShadowMap;
        }

        public PCSSPass(Settings s, Material blurMat, ComputeShader cs, CustomShadowCasterPass caster)
        {
            m_S = s; m_BlurMat = blurMat; m_CS = cs; m_Caster = caster;
            if (m_CS != null) m_CSKernel = m_CS.FindKernel("PCSS_Main");
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            profilingSampler = new ProfilingSampler("PCSS Screen Shadow");
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        void EnsureRTs(int width, int height)
        {
            if (m_SoftShadowRT != null && m_RTWidth == width && m_RTHeight == height)
                return;

            m_SoftShadowRT?.Release(); m_SoftShadowHandle?.Release();
            m_BlurTempRT?.Release();   m_BlurTempHandle?.Release();

            m_SoftShadowRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
            m_SoftShadowRT.enableRandomWrite = true;
            m_SoftShadowRT.filterMode = FilterMode.Point;
            m_SoftShadowRT.Create();
            m_SoftShadowHandle = RTHandles.Alloc(m_SoftShadowRT);

            m_BlurTempRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
            m_BlurTempRT.enableRandomWrite = true;
            m_BlurTempRT.filterMode = FilterMode.Point;
            m_BlurTempRT.Create();
            m_BlurTempHandle = RTHandles.Alloc(m_BlurTempRT);

            m_RTWidth = width; m_RTHeight = height;
        }

        /// 用 ViewportToWorldPoint 预计算 4 条远平面角射线，避免 compute shader 传矩阵
        static void ComputeFrustumRays(Camera cam, out Vector4 r0, out Vector4 r1, out Vector4 r2, out Vector4 r3)
        {
            Vector3 p = cam.transform.position;
            float far = cam.farClipPlane;
            r0 = cam.ViewportToWorldPoint(new Vector3(0, 0, far)) - p;
            r1 = cam.ViewportToWorldPoint(new Vector3(1, 0, far)) - p;
            r2 = cam.ViewportToWorldPoint(new Vector3(0, 1, far)) - p;
            r3 = cam.ViewportToWorldPoint(new Vector3(1, 1, far)) - p;
        }

        public void Release()
        {
            m_SoftShadowHandle?.Release(); m_SoftShadowRT?.Release();
            m_BlurTempHandle?.Release();   m_BlurTempRT?.Release();
            m_SoftShadowRT = null; m_SoftShadowHandle = null;
            m_BlurTempRT = null; m_BlurTempHandle = null;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            if (m_CS == null || m_BlurMat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData   cameraData   = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            int width  = cameraData.cameraTargetDescriptor.width;
            int height = cameraData.cameraTargetDescriptor.height;
            EnsureRTs(width, height);

            TextureHandle softShadowTH = graph.ImportTexture(m_SoftShadowHandle);
            TextureHandle blurTempTH   = graph.ImportTexture(m_BlurTempHandle);
            TextureHandle shadowTH     = graph.ImportTexture(m_Caster.shadowHandle);

            using (var builder = graph.AddUnsafePass<PassData>("PCSS", out var pd, profilingSampler))
            {
                pd.cs              = m_CS;
                pd.csKernel        = m_CSKernel;
                pd.blurMat         = m_BlurMat;
                pd.softShadowRT    = m_SoftShadowRT;
                pd.shadowCacheRT   = m_Caster.shadowRT;
                pd.source          = source;
                pd.softShadowTH    = softShadowTH;
                pd.blurTempTH      = blurTempTH;
                pd.cascadeVP       = m_Caster.CascadeViewProj;
                pd.splits          = m_Caster.CascadeSplits;
                pd.cascadeOffsets  = m_Caster.CascadeOffsets;
                pd.cascadeCount    = m_Caster.CascadeCount;
                pd.cascadeHalfWidths  = m_Caster.CascadeHalfWidths;
                pd.cascadeZDistances  = m_Caster.CascadeZDistances;
                pd.quality         = m_S.quality;
                pd.lightSize       = m_S.lightSize;
                pd.softness        = m_S.softness;
                pd.screenSize = new Vector4(width, height, 1f / width, 1f / height);
                ComputeFrustumRays(cameraData.camera, out pd.frustumRay0, out pd.frustumRay1, out pd.frustumRay2, out pd.frustumRay3);
                pd.zBufferParams = Shader.GetGlobalVector("_ZBufferParams");
                Vector3 camPos = cameraData.camera.transform.position;
                pd.worldSpaceCameraPos = new Vector4(camPos.x, camPos.y, camPos.z, 0);
                Vector3 lightDir = -RenderSettings.sun.transform.forward;
                pd.lightDirection = new Vector4(lightDir.x, lightDir.y, lightDir.z, 0);
                pd.enableBlur   = m_S.enableBlur;
                pd.blurScale    = m_S.blurScale;
                pd.showShadowMap = m_S.showShadowMap;

                builder.UseTexture(source,        AccessFlags.ReadWrite);
                builder.UseTexture(softShadowTH,  AccessFlags.ReadWrite);
                builder.UseTexture(blurTempTH,    AccessFlags.ReadWrite);
                builder.UseTexture(shadowTH,      AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                    // ── Compute Shader Dispatch ──
                    int kernel = data.csKernel;

                    cmd.SetComputeTextureParam(data.cs, kernel, "_PCSS_SoftShadow", data.softShadowRT);
                    cmd.SetComputeTextureParam(data.cs, kernel, "_PCSS_ShadowCacheTex", data.shadowCacheRT);
                    cmd.SetComputeVectorParam(data.cs, "_ScreenSize", data.screenSize);
                    cmd.SetComputeVectorParam(data.cs, "_FrustumRay0", data.frustumRay0);
                    cmd.SetComputeVectorParam(data.cs, "_FrustumRay1", data.frustumRay1);
                    cmd.SetComputeVectorParam(data.cs, "_FrustumRay2", data.frustumRay2);
                    cmd.SetComputeVectorParam(data.cs, "_FrustumRay3", data.frustumRay3);
                    cmd.SetComputeVectorParam(data.cs, "_ZBufferParams", data.zBufferParams);
                    cmd.SetComputeVectorParam(data.cs, "_WorldSpaceCameraPos", data.worldSpaceCameraPos);
                    cmd.SetComputeMatrixArrayParam(data.cs, "_CascadeLightVP", data.cascadeVP);
                    cmd.SetComputeVectorArrayParam(data.cs, "_CascadeAtlasOffset", data.cascadeOffsets);
                    cmd.SetComputeIntParam(data.cs, "_CascadeCount", data.cascadeCount);
                    cmd.SetComputeVectorParam(data.cs, "_CascadeSplits", data.splits);
                    cmd.SetComputeVectorParam(data.cs, "_CascadeHalfWidth", data.cascadeHalfWidths);
                    cmd.SetComputeVectorParam(data.cs, "_CascadeZDistance", data.cascadeZDistances);
                    // 质量档位关键词
                    data.cs.DisableKeyword("PCSS_LOW");
                    data.cs.DisableKeyword("PCSS_MEDIUM");
                    if      (data.quality == Settings.Quality.Low)    data.cs.EnableKeyword("PCSS_LOW");
                    else if (data.quality == Settings.Quality.Medium) data.cs.EnableKeyword("PCSS_MEDIUM");

                    cmd.SetComputeFloatParam(data.cs, "_PCSS_LightSize", data.lightSize);
                    cmd.SetComputeFloatParam(data.cs, "_PCSS_Softness", data.softness);
                    cmd.SetComputeVectorParam(data.cs, "_LightDirection", data.lightDirection);

                    int tgX = (width + 7) / 8;
                    int tgY = (height + 7) / 8;
                    cmd.DispatchCompute(data.cs, kernel, tgX, tgY, 1);

                    // ── 双边保边模糊（Blitter） ──
                    if (data.enableBlur)
                    {
                        data.blurMat.SetFloat("_BlurScale", data.blurScale);
                        Blitter.BlitCameraTexture(cmd, data.softShadowTH, data.blurTempTH, data.blurMat, 0);
                        Blitter.BlitCameraTexture(cmd, data.blurTempTH, data.softShadowTH, data.blurMat, 1);
                    }

                    // ── 叠加到屏幕 ──
                    if (data.showShadowMap)
                        Blitter.BlitCameraTexture(cmd, data.softShadowTH, data.source);
                });
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Feature 入口
    // ════════════════════════════════════════════════════════════

    public Settings settings = new();
    CustomShadowCasterPass m_SceneCaster, m_GameCaster;
    PCSSPass               m_ScenePCSS,   m_GamePCSS;

    public override void Create()
    {
        m_SceneCaster = null; m_GameCaster = null;
        m_ScenePCSS   = null; m_GamePCSS   = null;

        if (settings.shadowCasterShader != null)
        {
            var casterMat = CoreUtils.CreateEngineMaterial(settings.shadowCasterShader);
            m_SceneCaster = new CustomShadowCasterPass(settings, casterMat);
            m_GameCaster  = new CustomShadowCasterPass(settings, casterMat);
        }

        if (settings.pcssShader != null && m_SceneCaster != null)
        {
            var pcssMat = CoreUtils.CreateEngineMaterial(settings.pcssShader);
            m_ScenePCSS = new PCSSPass(settings, pcssMat, settings.pcssComputeShader, m_SceneCaster);
            m_GamePCSS  = new PCSSPass(settings, pcssMat, settings.pcssComputeShader, m_GameCaster);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CustomShadowCasterPass caster;
        PCSSPass               pcss;

        if (renderingData.cameraData.cameraType == CameraType.SceneView)
        { caster = m_SceneCaster; pcss = m_ScenePCSS; }
        else if (renderingData.cameraData.cameraType == CameraType.Game)
        { caster = m_GameCaster;  pcss = m_GamePCSS; }
        else return;

        if (caster == null) return;

        renderer.EnqueuePass(caster);

        if (pcss != null)
            renderer.EnqueuePass(pcss);
    }

    protected override void Dispose(bool disposing)
    {
        m_ScenePCSS?.Release(); m_GamePCSS?.Release();
        m_SceneCaster?.Release(); m_GameCaster?.Release();
        m_SceneCaster = null; m_GameCaster = null;
        m_ScenePCSS = null; m_GamePCSS = null;
    }
}
