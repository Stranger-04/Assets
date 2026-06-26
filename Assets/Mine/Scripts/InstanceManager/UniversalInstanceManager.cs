using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generic GPU instance rendering manager.
/// Handles buffer lifecycle, indirect draw dispatch, and editor integration.
/// Requires an IUniversalInstanceSimulator component on the same GameObject to provide
/// simulation-specific logic (compute shader binding, data initialization, per-frame update).
/// </summary>
[ExecuteAlways]
public class UniversalInstanceManager : MonoBehaviour
{
    [Header("Rendering")]
    public Mesh instanceMesh;
    public Material instanceMaterial;
    public int instanceCount = 1024;

    [Header("Culling")]
    public float boundsSize = 100f;

    [ReadOnly]
    [SerializeField]
    private bool initialized = false;

    private IUniversalInstanceSimulator simulation;
    private ComputeBuffer argsBuffer;
    private Bounds instanceBounds;

    #region Lifecycle

    private void Awake()
    {
        FindSimulation();
        if (instanceMesh != null && instanceCount > 0)
        {
            ReleaseBuffers();
            InitInstances();
        }
    }

    private void OnEnable()
    {
        FindSimulation();
        if (instanceMesh != null && instanceCount > 0)
        {
            ReleaseBuffers();
            InitInstances();
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    #endregion

    #region Initialization

    public void FindSimulation()
    {
        if (simulation == null)
            simulation = GetComponent<IUniversalInstanceSimulator>();
    }

    public void InitInstances()
    {
        if (initialized) return;
        if (instanceMesh == null)
        {
            Debug.LogError("[UniversalInstanceManager] Instance Mesh is null.");
            return;
        }
        if (instanceMaterial == null)
        {
            Debug.LogError("[UniversalInstanceManager] Instance Material is null.");
            return;
        }
        if (simulation == null)
        {
            Debug.LogError("[UniversalInstanceManager] No IUniversalInstanceSimulator component found on this GameObject.");
            return;
        }

        instanceBounds = new Bounds(transform.position, Vector3.one * boundsSize);

        // Create indirect draw args buffer
        uint[] args = new uint[5];
        args[0] = instanceMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = instanceMesh.GetIndexStart(0);
        args[3] = instanceMesh.GetBaseVertex(0);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // Delegate simulation-specific setup
        simulation.Initialize(instanceCount);
        simulation.BindMaterial(instanceMaterial);

        initialized = true;
        Debug.Log($"[UniversalInstanceManager] Initialized {instanceCount} instances.");
    }

    public void ReleaseBuffers()
    {
        simulation?.Release();

        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }

        initialized = false;
        Debug.Log("[UniversalInstanceManager] Buffers Released.");
    }

    #endregion

    #region Update

    private void Update()
    {
        if (!initialized) return;
        if (simulation == null) return;

        simulation.Dispatch(Time.deltaTime);

        // Sync visible count if simulation provides a culling buffer
        ComputeBuffer visibleBuffer = simulation.VisibleCountBuffer;
        if (visibleBuffer != null)
        {
            ComputeBuffer.CopyCount(visibleBuffer, argsBuffer, 4);
        }
        else
        {
            // Without culling, render all instances
            uint[] args = new uint[5];
            args[0] = instanceMesh.GetIndexCount(0);
            args[1] = (uint)instanceCount;
            args[2] = instanceMesh.GetIndexStart(0);
            args[3] = instanceMesh.GetBaseVertex(0);
            argsBuffer.SetData(args);
        }

        Graphics.DrawMeshInstancedIndirect(
            instanceMesh,
            0,
            instanceMaterial,
            instanceBounds,
            argsBuffer
        );
    }

    #endregion

    #region Editor

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
                manager.ReleaseBuffers();
                manager.FindSimulation();
                manager.InitInstances();
            }

            if (GUILayout.Button("Release Buffers"))
            {
                manager.ReleaseBuffers();
            }
        }
    }
#endif

    #endregion
}
