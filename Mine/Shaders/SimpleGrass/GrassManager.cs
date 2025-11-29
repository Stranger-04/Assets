using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GrassManager : MonoBehaviour
{
    public ComputeShader grassComputeShader;
    // Use database instead of direct generator runtime list
    public GrassDatabase grassDatabase;

    [Header("GrassLand Settings")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public int grassCount = 1024;
    public float areaSize = 10f;

    [Header("Wind Settings")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0f);
    public float windFrequency = 1f;
    public float windStrength = 1f;
    public float windScale = 1f;
    public Texture2D windNoiseTexture;

    [Header("Interaction Settings")]
    public RenderTexture interactionTexture;
    public Camera interactionCamera;

    [Header("Grass Settings")]
    public Vector3 BasePosition = Vector3.zero;
    public Quaternion BaseRotation = Quaternion.identity;
    public float BaseScale = 1f;

    private Bounds grassBounds;

    [ReadOnly]
    [SerializeField]
    public bool initialized = false;

    private ComputeBuffer MeshBuffer;
    private ComputeBuffer GrassBuffer;
    private ComputeBuffer ArgsBuffer;
    private ComputeBuffer ClipBuffer;
    struct MeshProperties
    {
        public Matrix4x4 matrix;

        public static int size()
        {
            return sizeof(float) * 16;
        }
    }

    struct GrassProperties
    {
        public Vector3 offset;
        public Vector3 normal;
        public float height;

        public static int size()
        {
            return sizeof(float) * 7;
        }
    }

    struct ClipProperties
    {
        public uint clipIndex;

        public static int size()
        {
            return sizeof(uint);
        }
    }
    
    void Awake()
    {
        if (grassDatabase != null && grassDatabase.Count > 0)
        {
            ReleaseBuffers();
            InitGrass();
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

    void Update()
    {
        if (!initialized) return;

        int kernel = grassComputeShader.FindKernel("CSMain");
        // Dynamic Parameters
        grassComputeShader.SetVector("_WindDirection", windDirection);
        grassComputeShader.SetFloat("_WindFrequency", windFrequency);
        grassComputeShader.SetFloat("_WindStrength", windStrength);
        grassComputeShader.SetFloat("_WindScale", windScale);

        grassComputeShader.SetTexture(kernel, "_InteractionTexture", interactionTexture);
        grassComputeShader.SetVector("_InteractionCenter", interactionCamera.transform.position);

        grassComputeShader.SetFloat("_Time", Time.time);

        int threadGroups = Mathf.CeilToInt(grassCount / 64f);
        grassComputeShader.Dispatch(kernel, threadGroups, 1, 1);

        Graphics.DrawMeshInstancedIndirect
        (
            grassMesh, 
            0, 
            grassMaterial, 
            grassBounds, 
            ArgsBuffer
        );
        
    }

    void InitGrass()
    {
        if (initialized) return;
        grassBounds = new Bounds(Vector3.zero, new Vector3(areaSize, 10f, areaSize));

        if (grassDatabase == null || grassDatabase.Count == 0)
        {
            Debug.Log("Grass database empty or missing");
            return;
        }

        grassCount = grassDatabase.Count;
        // Initialize Mesh Properties
        // Used to store position, rotation, scale of each grass instance
        MeshProperties [] meshproperties = new MeshProperties[grassCount];
        GrassProperties [] grassproperties = new GrassProperties[grassCount];
        ClipProperties [] clipproperties = new ClipProperties[grassCount];
        
        for (int i = 0; i < grassCount; i++)
        {
            // Build matrix from database stored world position & rotation
            Quaternion rotation = grassDatabase.GetRotation(i) * BaseRotation;
            Vector3 position = grassDatabase.GetPosition(i) + BasePosition;
            float scale = Random.Range(0.8f, 1.2f) * BaseScale;
            meshproperties[i].matrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);

            grassproperties[i].offset = Vector3.zero;
            grassproperties[i].normal = grassDatabase.GetNormal(i);
            grassproperties[i].height = grassDatabase.GetHeight(i);
        }
        

        MeshBuffer = new ComputeBuffer(grassCount, MeshProperties.size());
        MeshBuffer.SetData(meshproperties);

        GrassBuffer = new ComputeBuffer(grassCount, GrassProperties.size());
        GrassBuffer.SetData(grassproperties);

        ClipBuffer = new ComputeBuffer(grassCount, ClipProperties.size(), ComputeBufferType.Append);
        ClipBuffer.SetCounterValue(0);

        // Initialize Args Buffer
        // Used for DrawMeshInstancedIndirect
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)grassMesh.GetIndexCount(0);
        args[1] = (uint)grassCount;
        args[2] = (uint)grassMesh.GetIndexStart(0);
        args[3] = (uint)grassMesh.GetBaseVertex(0);
        ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        ArgsBuffer.SetData(args);

        int kernel = grassComputeShader.FindKernel("CSMain");

        // Static Parameters
        // Compute Shader
        
        grassComputeShader.SetInt("_GrassCount", grassCount);
        grassComputeShader.SetTexture(kernel, "_WindNoiseTexture", windNoiseTexture);
        grassComputeShader.SetBuffer(kernel, "_MeshProperties", MeshBuffer);
        grassComputeShader.SetBuffer(kernel, "_GrassProperties", GrassBuffer);
        grassComputeShader.SetBuffer(kernel, "_ClipProperties", ClipBuffer);

        Matrix4x4 worldToUV = Matrix4x4.identity;
        worldToUV.m00 = 1f / areaSize;
        worldToUV.m03 = 0.5f;
        worldToUV.m22 = 1f / areaSize;
        worldToUV.m23 = 0.5f;

        grassComputeShader.SetTexture(kernel, "_InteractionTexture", interactionTexture);
        grassComputeShader.SetMatrix("_InteractionMatrix", worldToUV);

        int threadGroups = Mathf.CeilToInt(grassCount / 64f);
        grassComputeShader.Dispatch(kernel, threadGroups, 1, 1);
        ComputeBuffer.CopyCount(ClipBuffer, ArgsBuffer, 4);

        // Material

        grassMaterial.SetBuffer("_MeshProperties", MeshBuffer);
        grassMaterial.SetBuffer("_GrassProperties", GrassBuffer);
        grassMaterial.SetBuffer("_ClipProperties", ClipBuffer);

        Debug.Log("Grass initialized");
        initialized = true;
    }

    void ReleaseBuffers()
    {   
        if (MeshBuffer != null)
        {
        MeshBuffer.Release();
        MeshBuffer = null;
        }

        if (ArgsBuffer != null)
        {
        ArgsBuffer.Release();
        ArgsBuffer = null;
        }

        if (GrassBuffer != null)
        {
        GrassBuffer.Release();
        GrassBuffer = null;
        }

        if (ClipBuffer != null)
        {
        ClipBuffer.Release();
        ClipBuffer = null;
        }

        initialized = false;
        Debug.Log("Buffer released");
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(GrassManager))]
    public class GrassManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GrassManager grassManager = (GrassManager)target;
            if (GUILayout.Button("Initialize Grass"))
            {
                grassManager.InitGrass();
            }
            if (GUILayout.Button("Release Buffers"))
            {
                grassManager.ReleaseBuffers();
            }
        }
    }
    #endif
}