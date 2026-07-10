using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Boid simulation — torus-distributed particles orbiting a target with
/// group-center-aware blending. Implements IUniversalInstanceSimulator for
/// use with UniversalInstanceManager.
/// </summary>
public class BoidSimulation : MonoBehaviour, IUniversalInstanceSimulator
{
    [Header("Compute")]
    public ComputeShader computeShader;

    [Header("Spawning")]
    public float spawnRadius = 10f;
    public Vector3 spawnVelocity = Vector3.zero;

    [Header("Behaviour")]
    public float maxCenterSpeed = 5f;
    public float aveInstanceSpeed = 2f;
    [Range(0.001f, 1f)] public float posDelayFactor = 0.005f;
    [Range(0.001f, 1f)] public float radiusFixFactor = 1f;

    [Header("Targets")]
    public Transform positionTarget;
    public Transform collisionTarget;
    public float collisionRadius = 1f;

    [Header("Mesh")]
    public Vector3 basePosition = Vector3.zero;
    public Quaternion baseRotation = Quaternion.identity;
    public float baseScale = 1f;

    [Header("Clip")]
    [Range(1f, 1000f)] public float depthClipThreshold = 50f;

    // ════════════════════════════════════════════════════════════
    //  GPU data structs — strides must match compute shader layouts
    // ════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Sequential)]
    private struct MeshProperties
    {
        public Matrix4x4 matrix;
        public static int Stride() => sizeof(float) * 16;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceProperties
    {
        public Vector3 positionOG;
        public Vector3 positionWS;
        public Vector3 velocity;
        public float anime;
        public static int Stride() => sizeof(float) * 10;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GroupProperties
    {
        public Vector3 center;
        public Vector3 velocity;
        public float state;
        public static int Stride() => sizeof(float) * 7;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClipProperties
    {
        public float depthClipThreshold;
        public static int Stride() => sizeof(float);
    }

    // ════════════════════════════════════════════════════════════
    //  Compute buffers
    // ════════════════════════════════════════════════════════════

    private ComputeBuffer meshBuffer;
    private ComputeBuffer instanceBuffer;
    private ComputeBuffer groupBuffer;
    private ComputeBuffer clipBuffer;
    private ComputeBuffer centerBuffer;

    private int instanceCount;
    private int partialCount;
    private int instanceKernel;
    private int centerKernel;
    private int groupKernel;

    // ════════════════════════════════════════════════════════════
    //  Shader property IDs
    // ════════════════════════════════════════════════════════════

    private static readonly int DeltaTimeID          = Shader.PropertyToID("_DeltaTime");
    private static readonly int PositionTargetID     = Shader.PropertyToID("_PositionTarget");
    private static readonly int CollisionTargetID    = Shader.PropertyToID("_CollisionTarget");
    private static readonly int CollisionRadiusID    = Shader.PropertyToID("_CollisionRadius");
    private static readonly int VPID                 = Shader.PropertyToID("_VP");
    private static readonly int InstanceCountID      = Shader.PropertyToID("_InstanceCount");
    private static readonly int PartialCountID       = Shader.PropertyToID("_PartialCount");
    private static readonly int MaxCenterSpeedID     = Shader.PropertyToID("_MaxCenterSpeed");
    private static readonly int AveInstanceSpeedID   = Shader.PropertyToID("_AveInstanceSpeed");
    private static readonly int DepthClipThresholdID = Shader.PropertyToID("_DepthClipThreshold");
    private static readonly int PosDelayFactorID     = Shader.PropertyToID("_PosDelayFactor");
    private static readonly int RadiusFixFactorID    = Shader.PropertyToID("_RadiusFixFactor");
    private static readonly int InnerRadiusID        = Shader.PropertyToID("_InnerRadius");
    private static readonly int SpawnRadiusID        = Shader.PropertyToID("_SpawnRadius");
    private static readonly int MeshBufferID         = Shader.PropertyToID("_MeshBuffer");
    private static readonly int InstanceBufferID     = Shader.PropertyToID("_InstanceBuffer");
    private static readonly int GroupBufferID        = Shader.PropertyToID("_GroupBuffer");
    private static readonly int ClipBufferID         = Shader.PropertyToID("_ClipBuffer");
    private static readonly int CenterBufferID       = Shader.PropertyToID("_CenterBuffer");

    // ════════════════════════════════════════════════════════════
    //  IUniversalInstanceSimulator
    // ════════════════════════════════════════════════════════════

    public ComputeBuffer VisibleCountBuffer => clipBuffer;

    public void Initialize(int count)
    {
        instanceCount = count;
        partialCount = Mathf.CeilToInt((float)instanceCount / 64f);

        instanceKernel = computeShader.FindKernel("CS_InstanceUpdate");
        centerKernel   = computeShader.FindKernel("CS_CenterUpdate");
        groupKernel    = computeShader.FindKernel("CS_GroupUpdate");

        CreateBuffers();
        BindKernels();
        SetStaticParams();
        RunInitialDispatch();
    }

    public void Dispatch(float deltaTime)
    {
        if (computeShader == null) return;

        // ── Per-frame dynamic params ─────────────────────────────
        computeShader.SetFloat(DeltaTimeID, deltaTime);

        if (positionTarget != null)
            computeShader.SetVector(PositionTargetID, positionTarget.position);

        if (collisionTarget != null)
        {
            computeShader.SetVector(CollisionTargetID, collisionTarget.position);
            computeShader.SetFloat(CollisionRadiusID, collisionRadius);
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Matrix4x4 vp = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix;
            computeShader.SetMatrix(VPID, vp);
        }

        // ── Dispatch chain ───────────────────────────────────────
        clipBuffer.SetCounterValue(0);

        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64f);
        computeShader.Dispatch(instanceKernel, threadGroups, 1, 1);
        computeShader.Dispatch(centerKernel, threadGroups, 1, 1);
        computeShader.Dispatch(groupKernel, 1, 1, 1);

        // ── Debug readback ───────────────────────────────────────
        if (Time.frameCount % 30 == 0)
        {
            var debugInstance = new InstanceProperties[1];
            var debugGroup = new GroupProperties[1];
            instanceBuffer.GetData(debugInstance, 0, 0, 1);
            groupBuffer.GetData(debugGroup, 0, 0, 1);
            Debug.Log($"pos: {debugInstance[0].positionWS}");
            Debug.Log($"center: {debugGroup[0].center}");
        }
    }

    public void BindMaterial(Material material)
    {
        material.SetBuffer(MeshBufferID, meshBuffer);
        material.SetBuffer(InstanceBufferID, instanceBuffer);
        material.SetBuffer(ClipBufferID, clipBuffer);
    }

    public void Release()
    {
        if (meshBuffer != null)     { meshBuffer.Release();     meshBuffer = null; }
        if (instanceBuffer != null) { instanceBuffer.Release(); instanceBuffer = null; }
        if (groupBuffer != null)    { groupBuffer.Release();    groupBuffer = null; }
        if (clipBuffer != null)     { clipBuffer.Release();     clipBuffer = null; }
        if (centerBuffer != null)   { centerBuffer.Release();   centerBuffer = null; }
    }

    // ════════════════════════════════════════════════════════════
    //  Buffer creation + CPU-side data generation
    // ════════════════════════════════════════════════════════════

    private void CreateBuffers()
    {
        // ── Mesh buffer (single element) ─────────────────────────
        var meshData = new MeshProperties[1];
        meshData[0].matrix = Matrix4x4.TRS(basePosition, baseRotation, Vector3.one * baseScale);
        meshBuffer = new ComputeBuffer(1, MeshProperties.Stride());
        meshBuffer.SetData(meshData);

        // ── Instance buffer ──────────────────────────────────────
        var instanceData = new InstanceProperties[instanceCount];
        float innerRadius = spawnRadius * 0.5f;
        float R = (spawnRadius + innerRadius) * 0.5f;
        float r = (spawnRadius - innerRadius) * 0.5f;

        Vector3 targetPos = positionTarget != null ? positionTarget.position : Vector3.zero;

        for (int i = 0; i < instanceCount; i++)
        {
            float u = Random.value * Mathf.PI * 2f;
            float v = Random.value * Mathf.PI * 2f;
            float t = Mathf.Sqrt(Random.value) * r;

            float cu = Mathf.Cos(u);
            float su = Mathf.Sin(u);
            float cv = Mathf.Cos(v);
            float sv = Mathf.Sin(v);

            Vector3 radial = new Vector3(cu, 0f, su);
            Vector3 center = radial * R;
            Vector3 offset = radial * t * cv + new Vector3(0f, t * sv, 0f);

            instanceData[i].positionOG = center + offset;
            instanceData[i].positionWS = instanceData[i].positionOG + targetPos;
            instanceData[i].velocity   = spawnVelocity + Random.insideUnitSphere;
            instanceData[i].anime      = Mathf.Atan2(instanceData[i].positionOG.z, instanceData[i].positionOG.x);
        }

        instanceBuffer = new ComputeBuffer(instanceCount, InstanceProperties.Stride());
        instanceBuffer.SetData(instanceData);

        // ── Group buffer (single element) ────────────────────────
        var groupData = new GroupProperties[1];
        groupData[0].center = transform.position;
        groupBuffer = new ComputeBuffer(1, GroupProperties.Stride());
        groupBuffer.SetData(groupData);

        // ── Clip buffer (append) ─────────────────────────────────
        var clipData = new ClipProperties[instanceCount];
        for (int i = 0; i < instanceCount; i++)
            clipData[i].depthClipThreshold = depthClipThreshold;

        clipBuffer = new ComputeBuffer(instanceCount, ClipProperties.Stride(), ComputeBufferType.Append);
        clipBuffer.SetCounterValue(0);
        clipBuffer.SetData(clipData);

        // ── Center reduction buffer ──────────────────────────────
        centerBuffer = new ComputeBuffer(partialCount, sizeof(float) * 3);
    }

    // ════════════════════════════════════════════════════════════
    //  Kernel binding + static params
    // ════════════════════════════════════════════════════════════

    private void BindKernels()
    {
        computeShader.SetBuffer(instanceKernel, InstanceBufferID, instanceBuffer);
        computeShader.SetBuffer(instanceKernel, GroupBufferID,    groupBuffer);
        computeShader.SetBuffer(instanceKernel, ClipBufferID,     clipBuffer);

        computeShader.SetBuffer(groupKernel, GroupBufferID,  groupBuffer);
        computeShader.SetBuffer(groupKernel, CenterBufferID, centerBuffer);

        computeShader.SetBuffer(centerKernel, InstanceBufferID, instanceBuffer);
        computeShader.SetBuffer(centerKernel, CenterBufferID,   centerBuffer);
    }

    private void SetStaticParams()
    {
        float innerRadius = spawnRadius * 0.5f;

        computeShader.SetInt(InstanceCountID, instanceCount);
        computeShader.SetInt(PartialCountID, partialCount);
        computeShader.SetFloat(MaxCenterSpeedID, maxCenterSpeed);
        computeShader.SetFloat(AveInstanceSpeedID, aveInstanceSpeed);
        computeShader.SetFloat(DepthClipThresholdID, depthClipThreshold);
        computeShader.SetFloat(PosDelayFactorID, posDelayFactor);
        computeShader.SetFloat(RadiusFixFactorID, radiusFixFactor);
        computeShader.SetFloat(InnerRadiusID, innerRadius);
        computeShader.SetFloat(SpawnRadiusID, spawnRadius);
    }

    private void RunInitialDispatch()
    {
        clipBuffer.SetCounterValue(0);

        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64f);
        computeShader.Dispatch(instanceKernel, threadGroups, 1, 1);
        computeShader.Dispatch(centerKernel, threadGroups, 1, 1);
        computeShader.Dispatch(groupKernel, 1, 1, 1);
    }
}
