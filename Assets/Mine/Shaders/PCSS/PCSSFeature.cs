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

        [Header("Shadow Bias")]
        [Range(0f, 2f)] public float depthBias  = 0.5f;
        [Range(0f, 2f)] public float normalBias = 0.4f;

        [Header("Debug")]
        public bool showShadowMap = true;
        public bool debugFixedProjection = false;
        public float debugFixedHalfSize = 15f;

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
            float camNear = cam.nearClipPlane;
            float fov = cam.fieldOfView * Mathf.Deg2Rad;
            float aspect = cam.aspect;

            float nearH = Mathf.Tan(fov * 0.5f) * camNear;
            float nearW = nearH * aspect;

            Vector3 fwd = t.forward, rgt = t.right, up = t.up;

            // ── 级联远近平面角点 ──
            Vector3 dBL = fwd * camNear + rgt * (-nearW) + up * (-nearH);
            Vector3 dTR = fwd * camNear + rgt * ( nearW) + up * ( nearH);
            Vector3 dBR = fwd * camNear + rgt * ( nearW) + up * (-nearH);
            Vector3 dTL = fwd * camNear + rgt * (-nearW) + up * ( nearH);

            float scaleN = cascadeNear / camNear;
            float scaleF = cascadeFar  / camNear;
            Vector3 nearBL = t.position + dBL * scaleN;
            Vector3 nearTR = t.position + dTR * scaleN;
            Vector3 nearBR = t.position + dBR * scaleN;
            Vector3 nearTL = t.position + dTL * scaleN;
            Vector3 farBL  = t.position + dBL * scaleF;
            Vector3 farTR  = t.position + dTR * scaleF;
            Vector3 farBR  = t.position + dBR * scaleF;
            Vector3 farTL  = t.position + dTL * scaleF;

            Vector3[] corners = { nearBL, nearTR, nearBR, nearTL, farBL, farTR, farBR, farTL };

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

            float midD = (minD + maxD) * 0.5f;
            float rawHalfW = maxExtR + 1f;
            float rawHalfH = maxExtU + 1f;

            // ── 防抖：尺度 + 中心双重量化 ──
            float rawTexelR = 2.0f * rawHalfW / tileRes;
            float rawTexelU = 2.0f * rawHalfH / tileRes;
            float halfW = Mathf.Ceil(rawHalfW / rawTexelR) * rawTexelR;
            float halfH = Mathf.Ceil(rawHalfH / rawTexelU) * rawTexelU;
            float texelR = 2.0f * halfW / tileRes;
            float texelU = 2.0f * halfH / tileRes;
            float snapR = Mathf.Floor(camR / texelR) * texelR;
            float snapU = Mathf.Floor(camU / texelU) * texelU;
            Vector3 center = lightRgt * snapR + lightUp * snapU + lightDir * midD;

            float depthRange = Mathf.Max(maxD - minD, 1f);
            float backDist = depthRange * 8f;

            Vector3 shadowCamPos = center - lightDir * backDist;
            camPos = shadowCamPos;

            Vector3 vX = -lightRgt, vY = lightUp, vZ = -lightDir;
            viewMatrix = new Matrix4x4(
                new Vector4(vX.x, vY.x, vZ.x, 0),
                new Vector4(vX.y, vY.y, vZ.y, 0),
                new Vector4(vX.z, vY.z, vZ.z, 0),
                new Vector4(-Vector3.Dot(vX, shadowCamPos), -Vector3.Dot(vY, shadowCamPos), -Vector3.Dot(vZ, shadowCamPos), 1));

            float zNear = Mathf.Max(0.1f, backDist - depthRange * 8f);
            float zFar  = backDist + depthRange * 8f;
            projMatrix = Matrix4x4.Ortho(-halfW, halfW, -halfH, halfH, zNear, zFar);
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

            float shadowDist = 50f;
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null) shadowDist = urpAsset.shadowDistance;

            // ── PSSM split ──
            float[] splits = ComputePSSMSplits(cam.nearClipPlane, shadowDist, cascadeCount, m_S.pssmLambda);
            CascadeCount = cascadeCount;

            // ── 每级联计算投影，同时找最宽级联做 culling ──
            var cascadeView  = new Matrix4x4[cascadeCount];
            var cascadeProj  = new Matrix4x4[cascadeCount];
            var cascadeCamPos = new Vector3[cascadeCount];
            float maxHalfW = 0f, maxHalfH = 0f;
            int widestCascade = 0;

            for (int ci = 0; ci < cascadeCount; ci++)
            {
                float cn = ci == 0 ? cam.nearClipPlane : splits[ci - 1];
                float cf = splits[ci];

                if (m_S.debugFixedProjection)
                {
                    float hs = m_S.debugFixedHalfSize;
                    Vector3 ctr = cam.transform.position;
                    Vector3 lightDir = mainLight.transform.forward;
                    Vector3 lr = Vector3.Cross(lightDir, Vector3.up).normalized;
                    if (lr.sqrMagnitude < 0.001f) lr = Vector3.Cross(lightDir, Vector3.forward).normalized;
                    Vector3 lu = Vector3.Cross(lr, lightDir).normalized;
                    float bd = hs * 10f;
                    cascadeCamPos[ci] = ctr - lightDir * bd;
                    Vector3 vX = -lr, vY = lu, vZ = -lightDir;
                    cascadeView[ci] = new Matrix4x4(
                        new Vector4(vX.x, vY.x, vZ.x, 0), new Vector4(vX.y, vY.y, vZ.y, 0),
                        new Vector4(vX.z, vY.z, vZ.z, 0),
                        new Vector4(-Vector3.Dot(vX, cascadeCamPos[ci]), -Vector3.Dot(vY, cascadeCamPos[ci]), -Vector3.Dot(vZ, cascadeCamPos[ci]), 1));
                    cascadeProj[ci] = Matrix4x4.Ortho(-hs, hs, -hs, hs, 0.1f, 2f * bd);
                }
                else
                {
                    ComputeShadowProjection(cam, mainLight, cn, cf, tileRes,
                        out cascadeView[ci], out cascadeProj[ci], out cascadeCamPos[ci]);
                }

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
            CascadeViewProj = new Matrix4x4[cascadeCount];
            CascadeOffsets  = new Vector4[cascadeCount];
            for (int ci = 0; ci < cascadeCount; ci++)
            {
                CascadeViewProj[ci] = cascadeProj[ci] * cascadeView[ci];
                int col = ci % 2, row = ci / 2;
                CascadeOffsets[ci] = new Vector4(
                    col * 0.5f, row * 0.5f, 0.5f, 0f); // xy=atlas offset, z=tile scale
            }
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
            ConfigureInput(ScriptableRenderPassInput.Depth);
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
