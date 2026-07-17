using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PCSSFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Resources")]
        public Shader shadowCasterShader;
        public Shader pcssShader;

        [Header("Shadow Map")]
        [Range(256, 4096)] public int shadowMapResolution = 2048;

        [Header("Cascades")]
        [Range(1, 8)] public int cascadeCount = 4;
        [Range(0f, 1f)] public float pssmLambda = 0.75f;
        [Range(10f, 200f)] public float shadowDistance = 50f;

        [Header("PCSS")]
        [Range(8, 64)] public int blockerSamples = 32;
        [Range(4, 64)] public int pcfSamples = 32;
        [Range(0.1f, 5f)] public float lightSize = 1.0f;
        [Range(0.1f, 2f)] public float softness = 1.0f;

        [Header("Shadow Bias")]
        [Range(0f, 2f)] public float depthBias  = 0.5f;
        [Range(0f, 2f)] public float normalBias = 0.4f;

        [Header("Debug")]
        public bool showShadowMap = true;
        [Range(0, 5)] public int debugMode = 0;

        internal static readonly int ShadowCacheTexID = Shader.PropertyToID("_PCSS_ShadowCacheTex");
        internal static readonly int LightViewID       = Shader.PropertyToID("_PCSS_LightView");
        internal static readonly int LightProjID       = Shader.PropertyToID("_PCSS_LightProj");
        internal static readonly int LightDirectionID  = Shader.PropertyToID("_LightDirection");
        internal static readonly int DepthBiasID       = Shader.PropertyToID("_ShadowDepthBias");
        internal static readonly int NormalBiasID      = Shader.PropertyToID("_ShadowNormalBias");
        internal static readonly int CascadeCountID    = Shader.PropertyToID("_CascadeCount");
        internal static readonly int CascadeSplitsID   = Shader.PropertyToID("_CascadeSplits");
        internal static readonly int CascadeLightVPID  = Shader.PropertyToID("_CascadeLightVP");
        internal static readonly int CascadeOffsetID   = Shader.PropertyToID("_CascadeAtlasOffset");
        internal static readonly int CascadeHalfWID    = Shader.PropertyToID("_CascadeHalfWidth");
        internal static readonly int CascadeZDistID    = Shader.PropertyToID("_CascadeZDistance");
        internal static readonly int BlockerSamplesID  = Shader.PropertyToID("_PCSS_BlockerSamples");
        internal static readonly int PCFSamplesID      = Shader.PropertyToID("_PCSS_PCFSamples");
        internal static readonly int LightSizeID       = Shader.PropertyToID("_PCSS_LightSize");
        internal static readonly int SoftnessID        = Shader.PropertyToID("_PCSS_Softness");
        internal static readonly int DebugModeID       = Shader.PropertyToID("_PCSS_DebugMode");
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

        public void SetCullingData(CullingResults cull, Camera cam) { }

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
            float maxExtR = 0f, maxExtU = 0f;
            foreach (var c in corners)
            {
                float dr = Mathf.Abs(Vector3.Dot(c, lightRgt) - camR);
                float du = Mathf.Abs(Vector3.Dot(c, lightUp) - camU);
                float d  = Vector3.Dot(c, lightDir);
                maxExtR = Mathf.Max(maxExtR, dr);
                maxExtU = Mathf.Max(maxExtU, du);
                minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
            }

            float midD     = Vector3.Dot(t.position, lightDir);
            float backDist = cascadeFar * 8f;
            float zNear    = 0.1f;
            float zFar     = backDist * 2f;
            float halfW    = halfExtent;
            float halfH    = halfExtent;
            Vector3 rawCenter = lightRgt * camR + lightUp * camU + lightDir * midD;

            // ── 第 1 步：构造未 snapping 的 view/proj ──
            Vector3 rawCamPos = rawCenter - lightDir * backDist;
            Vector3 vX = -lightRgt, vY = lightUp, vZ = -lightDir;
            Matrix4x4 rawView = new Matrix4x4(
                new Vector4(vX.x, vY.x, vZ.x, 0),
                new Vector4(vX.y, vY.y, vZ.y, 0),
                new Vector4(vX.z, vY.z, vZ.z, 0),
                new Vector4(-Vector3.Dot(vX, rawCamPos), -Vector3.Dot(vY, rawCamPos), -Vector3.Dot(vZ, rawCamPos), 1));
            Matrix4x4 rawProj = Matrix4x4.Ortho(-halfW, halfW, -halfH, halfH, zNear, zFar);

            // ── 第 2 步：Shadow Map UV 空间 snapping ──
            // 参考 Common Techniques to Improve Shadow Depth Maps：
            // 在 shadow map 像素空间直接量化参考点，再反算世界空间偏移量。
            // 这保证参考点始终映射到同一 shadow map 像素——独立于所有坐标系的伸缩。
            Matrix4x4 rawVP = rawProj * rawView;
            Vector3 refWS = rawCenter;
            Vector4 refCS = rawVP * new Vector4(refWS.x, refWS.y, refWS.z, 1);
            float invW = 1f / refCS.w;
            float ndcX = refCS.x * invW;
            float ndcY = refCS.y * invW;
            // NDC [-1,1] → 像素坐标 [0,tileRes] → 量化 → 回到 NDC
            float pixelX = (ndcX * 0.5f + 0.5f) * tileRes;
            float pixelY = (ndcY * 0.5f + 0.5f) * tileRes;
            pixelX = Mathf.Round(pixelX);
            pixelY = Mathf.Round(pixelY);
            float snapNdcX = (pixelX / tileRes - 0.5f) * 2f;
            float snapNdcY = (pixelY / tileRes - 0.5f) * 2f;
            // NDC 偏移 → 世界空间偏移
            float offsetR = (snapNdcX - ndcX) * halfW;
            float offsetU = (snapNdcY - ndcY) * halfH;
            Vector3 center = rawCenter + lightRgt * offsetR + lightUp * offsetU;

            // ── 第 3 步：用 snapped 中心重建 view ──
            Vector3 shadowCamPos = center - lightDir * backDist;
            camPos = shadowCamPos;
            viewMatrix = new Matrix4x4(
                new Vector4(vX.x, vY.x, vZ.x, 0),
                new Vector4(vX.y, vY.y, vZ.y, 0),
                new Vector4(vX.z, vY.z, vZ.z, 0),
                new Vector4(-Vector3.Dot(vX, shadowCamPos), -Vector3.Dot(vY, shadowCamPos), -Vector3.Dot(vZ, shadowCamPos), 1));
            projMatrix = rawProj;
        }

        static int s_DbgFrame = 0;

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
                float hw = (cascadeProj[ci].m00 > 0.0001f) ? 2f / cascadeProj[ci].m00 : 0f;
                float hh = (cascadeProj[ci].m11 > 0.0001f) ? 2f / cascadeProj[ci].m11 : 0f;
                // hw ≈ 2/(2/(r-l)) → not directly extractable. Use a simpler heuristic:
                // half-extent ≈ Mathf.Abs(1f / cascadeProj[ci].m00) — this gives r-l/2 for Ortho
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

            if (++s_DbgFrame % 30 == 0)
                Debug.Log($"[PCSS] C0 halfW={cascadeHalfW[0]:F3} zDist={cascadeZDist[0]:F1} " +
                          $"camR={Vector3.Dot(cam.transform.position, mainLight.transform.right):F3} " +
                          $"midD={Vector3.Dot(cam.transform.position, mainLight.transform.forward):F3}");

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
            var drawSettings = new DrawingSettings(new ShaderTagId("UniversalForward"), sorting);
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
        Material m_Mat;
        CustomShadowCasterPass m_Caster;

        class PassData
        {
            public Material material;
            public TextureHandle source, dest;
            public Matrix4x4[] cascadeVP;
            public Vector4 splits, offsets0, offsets1, offsets2, offsets3;
            public int cascadeCount;
        }

        public PCSSPass(Settings s, Material mat, CustomShadowCasterPass caster)
        {
            m_S = s; m_Mat = mat; m_Caster = caster;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            profilingSampler = new ProfilingSampler("PCSS Screen Shadow");
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            if (m_Mat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData   cameraData   = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            desc.msaaSamples = 1;

            TextureHandle dest = UniversalRenderer.CreateRenderGraphTexture(
                graph, desc, "_PCSS_SoftShadow", false);
            TextureHandle shadowTH = graph.ImportTexture(m_Caster.shadowHandle);

            using (var builder = graph.AddUnsafePass<PassData>("PCSS", out var pd, profilingSampler))
            {
                pd.material  = m_Mat;
                pd.source    = source;
                pd.dest      = dest;
                pd.cascadeVP   = m_Caster.CascadeViewProj;
                pd.splits      = m_Caster.CascadeSplits;
                pd.cascadeCount = m_Caster.CascadeCount;

                var off = m_Caster.CascadeOffsets;
                pd.offsets0 = off.Length > 0 ? off[0] : Vector4.zero;
                pd.offsets1 = off.Length > 1 ? off[1] : Vector4.zero;
                pd.offsets2 = off.Length > 2 ? off[2] : Vector4.zero;
                pd.offsets3 = off.Length > 3 ? off[3] : Vector4.zero;

                builder.UseTexture(source,   AccessFlags.ReadWrite);
                builder.UseTexture(dest,     AccessFlags.Write);
                builder.UseTexture(shadowTH, AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                    data.material.SetTexture(Settings.ShadowCacheTexID, m_Caster.shadowRT);
                    data.material.SetInt(Settings.CascadeCountID, data.cascadeCount);
                    data.material.SetVector(Settings.CascadeSplitsID, data.splits);
                    data.material.SetVector(Settings.CascadeHalfWID, m_Caster.CascadeHalfWidths);
                    data.material.SetVector(Settings.CascadeZDistID, m_Caster.CascadeZDistances);
                    data.material.SetInt(Settings.BlockerSamplesID, m_S.blockerSamples);
                    data.material.SetInt(Settings.PCFSamplesID, m_S.pcfSamples);
                    data.material.SetFloat(Settings.LightSizeID, m_S.lightSize);
                    data.material.SetFloat(Settings.SoftnessID, m_S.softness);
                    data.material.SetInt(Settings.DebugModeID, m_S.debugMode);
                    data.material.SetVector(Settings.LightDirectionID, -RenderSettings.sun.transform.forward);
                    cmd.SetGlobalMatrixArray(Settings.CascadeLightVPID, data.cascadeVP);
                    // Pass atlas offsets as a combined vector array
                    var offArr = new Vector4[] { data.offsets0, data.offsets1, data.offsets2, data.offsets3 };
                    cmd.SetGlobalVectorArray(Settings.CascadeOffsetID, offArr);

                    Blitter.BlitCameraTexture(cmd, data.source, data.dest, data.material, 0);

                    if (m_S.showShadowMap)
                        Blitter.BlitCameraTexture(cmd, data.dest, data.source);
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
            m_ScenePCSS = new PCSSPass(settings, pcssMat, m_SceneCaster);
            m_GamePCSS  = new PCSSPass(settings, pcssMat, m_GameCaster);
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

        caster.SetCullingData(renderingData.cullResults, renderingData.cameraData.camera);
        renderer.EnqueuePass(caster);

        if (pcss != null)
            renderer.EnqueuePass(pcss);
    }

    protected override void Dispose(bool disposing)
    {
        m_SceneCaster?.Release(); m_GameCaster?.Release();
        m_SceneCaster = null; m_GameCaster = null;
        m_ScenePCSS = null; m_GamePCSS = null;
    }
}
