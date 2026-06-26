using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

// [ExecuteAlways]
public class BoidsManager : MonoBehaviour
{
    public ComputeShader computeShader;

    [Header("Instance Settings")]
    public Mesh instanceMesh;
    public Material instanceMaterial;
    public int instanceCount = 1024;
    public float spawnRadius = 10f;
    public Vector3 spawnVelocity = Vector3.zero;
    public float maxCenterSpeed = 5f;
    public float aveInstanceSpeed = 2f;
    [Range(0.001f, 1f)] public float posDelayFactor = 0.005f;
    [Range(0.001f, 1f)] public float radiusFixFactor = 1f;

    [Header("Group Settings")]
    public Transform positionTarget;
    public Transform collisionTarget;
    public float collisionRadius = 1f;

    [Header("Mesh Settings")]
    public Vector3 basePosition = Vector3.zero;
    public Quaternion baseRotation = Quaternion.identity;
    public float baseScale = 1f;

    [Header("Clip Settings")]
    [Range(1f, 1000f)] public float depthClipThreshold = 50f;
    public float boundsSize = 100f;
    private Bounds instanceBounds;

    [ReadOnly]
    [SerializeField]
    public bool initialized = false;

    private static readonly int DeltaTimeID           = Shader.PropertyToID("_DeltaTime");
    private static readonly int PositionTargetID      = Shader.PropertyToID("_PositionTarget");
    private static readonly int CollisionTargetID     = Shader.PropertyToID("_CollisionTarget");
    private static readonly int CollisionRadiusID     = Shader.PropertyToID("_CollisionRadius");
    private static readonly int VPID                  = Shader.PropertyToID("_VP");
    private static readonly int InstanceCountID       = Shader.PropertyToID("_InstanceCount");
    private static readonly int PartialCountID        = Shader.PropertyToID("_PartialCount");

    private static readonly int MaxCenterSpeedID      = Shader.PropertyToID("_MaxCenterSpeed");
    private static readonly int AveInstanceSpeedID    = Shader.PropertyToID("_AveInstanceSpeed");
    private static readonly int DepthClipThresholdID  = Shader.PropertyToID("_DepthClipThreshold");
    private static readonly int PosDelayFactorID      = Shader.PropertyToID("_PosDelayFactor");
    private static readonly int RadiusFixFactorID     = Shader.PropertyToID("_RadiusFixFactor");
    private static readonly int InnerRadiusID         = Shader.PropertyToID("_InnerRadius");
    private static readonly int SpawnRadiusID         = Shader.PropertyToID("_SpawnRadius");

    private static readonly int MeshBufferID          = Shader.PropertyToID("_MeshBuffer");
    private static readonly int InstanceBufferID      = Shader.PropertyToID("_InstanceBuffer");
    private static readonly int GroupBufferID         = Shader.PropertyToID("_GroupBuffer");
    private static readonly int ClipBufferID          = Shader.PropertyToID("_ClipBuffer");
    private static readonly int CenterBufferID        = Shader.PropertyToID("_CenterBuffer");

    private ComputeBuffer MeshBuffer;
    private ComputeBuffer InstanceBuffer;
    private ComputeBuffer GroupBuffer;
    private ComputeBuffer ClipBuffer;
    private ComputeBuffer ArgsBuffer;
    private ComputeBuffer CenterBuffer;

    private int partialCount;

    struct MeshProperties
    {
        public Matrix4x4 matrix;

        public static int Size()
        {
            return sizeof(float) * 16;
        }
    }

    struct InstanceProperties
    {
        public Vector3 positionOG;
        public Vector3 positionWS;
        public Vector3 velocity;
        public float anime;

        public static int Size()
        {
            return sizeof(float) * (3 + 3 + 3 + 1);
        }
    }

    struct GroupProperties
    {
        public Vector3 center;

        public static int Size()
        {
            return sizeof(float) * 3;
        }
    }

    struct ClipProperties
    {
        public float depthClipThreshold;

        public static int Size()
        {
            return sizeof(float) * 1;
        }
    }

    void Awake()
    {
        if (instanceMesh != null && instanceCount > 0)
        {
            ReleaseBuffers();
            InitInstances();
        }
    }

    void OnEnable()
    {
        if (instanceMesh != null && instanceCount > 0)
        {
            ReleaseBuffers();
            InitInstances();
        }
    }

    void Start()
    {
        if (instanceMesh != null && instanceCount > 0)
        {
            ReleaseBuffers();
            InitInstances();
        }
    }

    void OnDisable()
    {
        ReleaseBuffers();
    }

    void OnDisabled()
    {
        ReleaseBuffers();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    public void ReleaseBuffers()
    {
        if (MeshBuffer != null)
        {
            MeshBuffer.Release();
            MeshBuffer = null;
        }

        if (InstanceBuffer != null)
        {
            InstanceBuffer.Release();
            InstanceBuffer = null;
        }

        if (GroupBuffer != null)
        {
            GroupBuffer.Release();
            GroupBuffer = null;
        }

        if (ClipBuffer != null)
        {
            ClipBuffer.Release();
            ClipBuffer = null;
        }

        if (CenterBuffer != null)
        {
            CenterBuffer.Release();
            CenterBuffer = null;
        }

        if (ArgsBuffer != null)
        {
            ArgsBuffer.Release();
            ArgsBuffer = null;
        }

        initialized = false;
        Debug.Log("Buffers Released");
    }

    void Update()
    {
        if (!initialized) return;

        computeShader.SetFloat(DeltaTimeID, Time.deltaTime);
        computeShader.SetVector(PositionTargetID, positionTarget.position);
        computeShader.SetVector(CollisionTargetID, collisionTarget.position);
        computeShader.SetFloat(CollisionRadiusID, collisionRadius);
        Matrix4x4 vp = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
        computeShader.SetMatrix(VPID, vp);

        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64);
        int instanceKernel = computeShader.FindKernel("CS_InstanceUpdate");
        int centerKernel = computeShader.FindKernel("CS_CenterUpdate");
        int groupKernel  = computeShader.FindKernel("CS_GroupUpdate");

        ClipBuffer.SetCounterValue(0);
        computeShader.Dispatch(instanceKernel, threadGroups, 1, 1);
        computeShader.Dispatch(centerKernel, threadGroups, 1, 1);
        computeShader.Dispatch(groupKernel, 1, 1, 1);

        ComputeBuffer.CopyCount(ClipBuffer, ArgsBuffer, 4);

        if (Time.frameCount % 30 == 0)
        {
            var debugArgs1 = new InstanceProperties[1];
            var debugArgs2 = new GroupProperties[1];
            InstanceBuffer.GetData(debugArgs1, 0, 0, 1);
            GroupBuffer.GetData(debugArgs2, 0, 0, 1);
            Debug.Log("pos" + debugArgs1[0].positionWS);
            Debug.Log("center: " + debugArgs2[0].center);
        }

        Graphics.DrawMeshInstancedIndirect
        (
            instanceMesh, 
            0, 
            instanceMaterial, 
            instanceBounds, 
            ArgsBuffer
        );
    }

    public void InitInstances()
    {
        if (initialized) return;
        instanceBounds = new Bounds(transform.position, Vector3.one * boundsSize);

        // Buffer Setup
        MeshProperties [] meshProperties = new MeshProperties[1];
        InstanceProperties [] instanceProperties = new InstanceProperties[instanceCount];
        GroupProperties [] groupProperties = new GroupProperties[1];
        ClipProperties [] clipProperties = new ClipProperties[instanceCount];

        float innerRadius = spawnRadius * 0.5f;
        for (int i = 0; i < instanceCount; i++)
        {
            float R = (spawnRadius + innerRadius) * 0.5f;
            float r = (spawnRadius - innerRadius) * 0.5f;

            float u = Random.value * Mathf.PI * 2;
            float v = Random.value * Mathf.PI * 2;
            float t = Mathf.Sqrt(Random.value) * r;

            float cu = Mathf.Cos(u), su = Mathf.Sin(u);
            float cv = Mathf.Cos(v), sv = Mathf.Sin(v);

            Vector3 radial = new Vector3(cu, 0, su);
            Vector3 center = radial * R;
            Vector3 offset = radial * t * cv + new Vector3(0, t * sv, 0);

            instanceProperties[i].positionOG = center + offset;
            instanceProperties[i].positionWS = instanceProperties[i].positionOG + positionTarget.position;
            instanceProperties[i].velocity = spawnVelocity + Random.insideUnitSphere;
            instanceProperties[i].anime = Mathf.Atan2(instanceProperties[i].positionOG.z, instanceProperties[i].positionOG.x);
            clipProperties[i].depthClipThreshold = depthClipThreshold;
        }
        meshProperties[0].matrix = Matrix4x4.TRS(basePosition, baseRotation, Vector3.one * baseScale);
        groupProperties[0].center = transform.position;

        MeshBuffer = new ComputeBuffer(1, MeshProperties.Size());
        MeshBuffer.SetData(meshProperties);

        InstanceBuffer = new ComputeBuffer(instanceCount, InstanceProperties.Size());
        InstanceBuffer.SetData(instanceProperties);

        GroupBuffer = new ComputeBuffer(1, GroupProperties.Size());
        GroupBuffer.SetData(groupProperties);

        ClipBuffer = new ComputeBuffer(instanceCount, ClipProperties.Size(), ComputeBufferType.Append);
        ClipBuffer.SetCounterValue(0);
        ClipBuffer.SetData(clipProperties);

        partialCount = Mathf.CeilToInt((float)instanceCount / 64);
        CenterBuffer = new ComputeBuffer(partialCount, sizeof(float) * 3);

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)instanceMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = (uint)instanceMesh.GetIndexStart(0);
        args[3] = (uint)instanceMesh.GetBaseVertex(0);
        ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        ArgsBuffer.SetData(args);
        // Compute Shader Setup
        int Instancekernel  = computeShader.FindKernel("CS_InstanceUpdate");
        int Centerkernel    = computeShader.FindKernel("CS_CenterUpdate");
        int Groupkernel     = computeShader.FindKernel("CS_GroupUpdate");

        computeShader.SetInt(InstanceCountID, instanceCount);
        computeShader.SetInt(PartialCountID, partialCount);
        computeShader.SetBuffer(Instancekernel, InstanceBufferID, InstanceBuffer);
        computeShader.SetBuffer(Instancekernel, GroupBufferID, GroupBuffer);
        computeShader.SetBuffer(Instancekernel, ClipBufferID, ClipBuffer);
        computeShader.SetBuffer(Groupkernel, GroupBufferID, GroupBuffer);
        computeShader.SetBuffer(Groupkernel, CenterBufferID, CenterBuffer);
        computeShader.SetBuffer(Centerkernel, InstanceBufferID, InstanceBuffer);
        computeShader.SetBuffer(Centerkernel, CenterBufferID, CenterBuffer);

        computeShader.SetFloat(DeltaTimeID, Time.deltaTime);
        computeShader.SetFloat(MaxCenterSpeedID, maxCenterSpeed);
        computeShader.SetFloat(AveInstanceSpeedID, aveInstanceSpeed);
        computeShader.SetFloat(DepthClipThresholdID, depthClipThreshold);
        computeShader.SetFloat(PosDelayFactorID, posDelayFactor);
        computeShader.SetFloat(RadiusFixFactorID, radiusFixFactor);
        computeShader.SetFloat(InnerRadiusID, innerRadius);
        computeShader.SetFloat(SpawnRadiusID, spawnRadius);

        ClipBuffer.SetCounterValue(0);
        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64);
        computeShader.Dispatch(Instancekernel, threadGroups, 1, 1);
        computeShader.Dispatch(Centerkernel, threadGroups, 1, 1);
        computeShader.Dispatch(Groupkernel, 1, 1, 1);

        ComputeBuffer.CopyCount(ClipBuffer, ArgsBuffer, 4);

        // Material Setup
        instanceMaterial.SetBuffer(MeshBufferID, MeshBuffer);
        instanceMaterial.SetBuffer(InstanceBufferID, InstanceBuffer);
        instanceMaterial.SetBuffer(ClipBufferID, ClipBuffer);

        Debug.Log("Instances Initialized: " + instanceCount);
        initialized = true;
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(BoidsManager))]
    public class BoidsManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BoidsManager manager = (BoidsManager)target;
            if (GUILayout.Button("Initialize Instances"))
            {
                manager.InitInstances();
            }

            if (GUILayout.Button("Release Buffers"))
            {
                manager.ReleaseBuffers();
            }
        }
    }
    #endif
}