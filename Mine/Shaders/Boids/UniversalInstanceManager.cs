using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class UniversalInstanceManager : MonoBehaviour
{
    public ComputeShader computeShader;

    [Header("Instance Settings")]
    public Mesh instanceMesh;
    public Material instanceMaterial;
    public int instanceCount = 1024;
    public float spawnRadius = 10f;
    public Vector3 spawnVelocity = Vector3.zero;
    public float maxSpeed = 5f;

    [Header("Collision Settings")]
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
    private ComputeBuffer MeshBuffer;
    private ComputeBuffer InstanceBuffer;
    private ComputeBuffer GroupBuffer;
    private ComputeBuffer ClipBuffer;
    private ComputeBuffer ArgsBuffer;

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
        public Vector3 position;
        public Vector3 velocity;
        public float animation;

        public static int Size()
        {
            return sizeof(float) * (3 + 3 + 1);
        }
    }

    struct GroupProperties
    {
        public Vector3 center;
        public Vector3 velocity;
        public float state;

        public static int Size()
        {
            return sizeof(float) * (3 + 3 +1);
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

    void ReleaseBuffers()
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

        int Groupkernel = computeShader.FindKernel("CS_GroupUpdate");
        computeShader.SetFloat("_DeltaTime", Time.deltaTime);
        computeShader.SetVector("_PositionTarget", transform.position);
        computeShader.Dispatch(Groupkernel, 1, 1, 1);

        int Instancekernel = computeShader.FindKernel("CS_InstanceUpdate");
        computeShader.SetFloat("_DeltaTime", Time.deltaTime);
        computeShader.SetVector("_CollisionTarget", collisionTarget.position);
        Matrix4x4 vp = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
        computeShader.SetMatrix("_VP", vp);

        ClipBuffer.SetCounterValue(0);
        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64);
        computeShader.Dispatch(Instancekernel, threadGroups, 1, 1);
        ComputeBuffer.CopyCount(ClipBuffer, ArgsBuffer, 4);

        Graphics.DrawMeshInstancedIndirect
        (
            instanceMesh, 
            0, 
            instanceMaterial, 
            instanceBounds, 
            ArgsBuffer
        );
    }

    void InitInstances()
    {
        if (initialized) return;
        instanceBounds = new Bounds(transform.position, Vector3.one * boundsSize);

        // Buffer Setup
        MeshProperties [] meshProperties = new MeshProperties[instanceCount];
        InstanceProperties [] instanceProperties = new InstanceProperties[instanceCount];
        GroupProperties [] groupProperties = new GroupProperties[1];
        ClipProperties [] clipProperties = new ClipProperties[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            meshProperties[i].matrix = Matrix4x4.TRS(basePosition, baseRotation, Vector3.one * baseScale * Random.Range(0.8f, 1.2f));
            instanceProperties[i].position = Random.insideUnitSphere * spawnRadius + transform.position;
            instanceProperties[i].velocity = spawnVelocity + Random.insideUnitSphere;
            instanceProperties[i].animation = Random.Range(0f, 1f);
            clipProperties[i].depthClipThreshold = depthClipThreshold;
        }

        MeshBuffer = new ComputeBuffer(instanceCount, MeshProperties.Size());
        MeshBuffer.SetData(meshProperties);

        InstanceBuffer = new ComputeBuffer(instanceCount, InstanceProperties.Size());
        InstanceBuffer.SetData(instanceProperties);

        GroupBuffer = new ComputeBuffer(1, GroupProperties.Size());

        ClipBuffer = new ComputeBuffer(instanceCount, ClipProperties.Size(), ComputeBufferType.Append);
        ClipBuffer.SetCounterValue(0);
        ClipBuffer.SetData(clipProperties);

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)instanceMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = (uint)instanceMesh.GetIndexStart(0);
        args[3] = (uint)instanceMesh.GetBaseVertex(0);
        ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        ArgsBuffer.SetData(args);
        // Compute Shader Setup
        int Groupkernel = computeShader.FindKernel("CS_GroupUpdate");
        computeShader.SetBuffer(Groupkernel, "_GroupBuffer", GroupBuffer);
        computeShader.Dispatch(Groupkernel, 1, 1, 1);

        int Instancekernel = computeShader.FindKernel("CS_InstanceUpdate");
        computeShader.SetInt("_InstanceCount", instanceCount);
        computeShader.SetBuffer(Instancekernel, "_InstanceBuffer", InstanceBuffer);
        computeShader.SetBuffer(Instancekernel, "_ClipBuffer", ClipBuffer);

        computeShader.SetFloat("_MaxSpeed", maxSpeed);
        computeShader.SetFloat("_CollisionRadius", collisionRadius);
        computeShader.SetFloat("_DepthClipThreshold", depthClipThreshold);

        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64);
        computeShader.Dispatch(Instancekernel, threadGroups, 1, 1);
        ComputeBuffer.CopyCount(ClipBuffer, ArgsBuffer, 4);

        // Material Setup
        instanceMaterial.SetBuffer("_MeshBuffer", MeshBuffer);
        instanceMaterial.SetBuffer("_InstanceBuffer", InstanceBuffer);
        instanceMaterial.SetBuffer("_ClipBuffer", ClipBuffer);

        Debug.Log("Instances Initialized: " + instanceCount);
        initialized = true;
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(UniversalInstanceManager))]
    public class UniversalInstanceManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            UniversalInstanceManager manager = (UniversalInstanceManager)target;
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