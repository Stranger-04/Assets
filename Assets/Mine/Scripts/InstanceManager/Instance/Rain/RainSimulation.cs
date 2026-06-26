using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Rain particle simulation — particles fall from a rotated emission plane that follows
/// the GameObject's transform. Force fields editable as a variable-size list.
/// </summary>
public class RainSimulation : MonoBehaviour, IUniversalInstanceSimulator
{
    [Header("Compute")]
    public ComputeShader rainShader;

    [Header("Physics")]
    public Vector3[] forceFields = new Vector3[]
    {
        new Vector3(0, -9.81f, 0),
        new Vector3(2, 0, 0),
    };

    [Header("Area (transform local space)")]
    [Tooltip("x=X radius, y=fall distance along -UP, z=Z radius")]
    public Vector3 spawnArea = new Vector3(30, 55, 30);

    private const int MaxForceFields = 16;

    [StructLayout(LayoutKind.Sequential)]
    private struct RainParticle
    {
        public Vector3 position;
        public Vector3 velocity;

        public static int Stride() => sizeof(float) * 6;
    }

    private ComputeBuffer rainBuffer;
    private int instanceCount;
    private int updateKernel;

    private static readonly int RainBufferId       = Shader.PropertyToID("_RainBuffer");
    private static readonly int InstanceCountId    = Shader.PropertyToID("_InstanceCount");
    private static readonly int DeltaTimeId        = Shader.PropertyToID("_DeltaTime");
    private static readonly int ForceFieldsId      = Shader.PropertyToID("_ForceFields");
    private static readonly int ForceFieldCountId  = Shader.PropertyToID("_ForceFieldCount");
    private static readonly int SpawnCenterId      = Shader.PropertyToID("_SpawnCenter");
    private static readonly int SpawnRightId       = Shader.PropertyToID("_SpawnRight");
    private static readonly int SpawnUpId          = Shader.PropertyToID("_SpawnUp");
    private static readonly int SpawnForwardId     = Shader.PropertyToID("_SpawnForward");
    private static readonly int SpawnAreaId        = Shader.PropertyToID("_SpawnArea");

    public ComputeBuffer VisibleCountBuffer => null;

    // ════════════════════════════════════════════════════════════
    //  Initialize
    // ════════════════════════════════════════════════════════════
    public void Initialize(int count)
    {
        instanceCount = count;
        updateKernel  = rainShader.FindKernel("CS_RainUpdate");

        Transform t = transform;

        RainParticle[] particles = new RainParticle[count];
        for (int i = 0; i < count; i++)
        {
            float rx = (Random.value * 2f - 1f) * spawnArea.x * 0.5f;
            float rz = (Random.value * 2f - 1f) * spawnArea.z * 0.5f;
            // Pre-distribute along the fall column for natural initial fill
            float ry = Random.Range(-spawnArea.y, 0f);

            particles[i].position = t.position
                + t.right   * rx
                + t.up      * ry
                + t.forward * rz;

            Vector3 initVel = Vector3.zero;
            foreach (var f in forceFields) initVel += f;
            particles[i].velocity = new Vector3(initVel.x, 0, initVel.z);
        }

        rainBuffer = new ComputeBuffer(count, RainParticle.Stride());
        rainBuffer.SetData(particles);

        rainShader.SetBuffer(updateKernel, RainBufferId, rainBuffer);
        rainShader.SetInt(InstanceCountId, count);

        UploadForceFields();
    }

    // ════════════════════════════════════════════════════════════
    //  Dispatch
    // ════════════════════════════════════════════════════════════
    public void Dispatch(float deltaTime)
    {
        rainShader.SetFloat(DeltaTimeId, deltaTime);

        Transform t = transform;
        rainShader.SetVector(SpawnCenterId,  t.position);
        rainShader.SetVector(SpawnRightId,   t.right);
        rainShader.SetVector(SpawnUpId,      t.up);
        rainShader.SetVector(SpawnForwardId, t.forward);
        rainShader.SetVector(SpawnAreaId,    spawnArea);

        UploadForceFields();

        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64f);
        rainShader.Dispatch(updateKernel, threadGroups, 1, 1);
    }

    public void BindMaterial(Material material)
    {
        material.SetBuffer(RainBufferId, rainBuffer);
    }

    public void Release()
    {
        if (rainBuffer != null) { rainBuffer.Release(); rainBuffer = null; }
    }

    private void UploadForceFields()
    {
        var fields = new Vector4[MaxForceFields];
        int count = Mathf.Min(forceFields.Length, MaxForceFields);
        for (int i = 0; i < count; i++)
            fields[i] = new Vector4(forceFields[i].x, forceFields[i].y, forceFields[i].z, 0);
        rainShader.SetVectorArray(ForceFieldsId, fields);
        rainShader.SetInt(ForceFieldCountId, count);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos — emission plane + fall volume
    // ════════════════════════════════════════════════════════════
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform t = transform;
        Vector3 c = t.position;
        Vector3 r = t.right   * spawnArea.x * 0.5f;
        Vector3 f = t.forward * spawnArea.z * 0.5f;
        Vector3 d = t.up      * spawnArea.y;  // fall distance (full)

        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);

        // Emission plane wire rectangle
        Gizmos.DrawLine(c - r - f, c + r - f);
        Gizmos.DrawLine(c + r - f, c + r + f);
        Gizmos.DrawLine(c + r + f, c - r + f);
        Gizmos.DrawLine(c - r + f, c - r - f);

        // Fall volume — 4 vertical edges
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
        Vector3 b = c - d;
        Gizmos.DrawLine(c - r - f, b - r - f);
        Gizmos.DrawLine(c + r - f, b + r - f);
        Gizmos.DrawLine(c + r + f, b + r + f);
        Gizmos.DrawLine(c - r + f, b - r + f);

        // Bottom wire rectangle
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawLine(b - r - f, b + r - f);
        Gizmos.DrawLine(b + r - f, b + r + f);
        Gizmos.DrawLine(b + r + f, b - r + f);
        Gizmos.DrawLine(b - r + f, b - r - f);

        // Fall direction arrow
        Gizmos.color = new Color(0.3f, 1f, 1f, 0.9f);
        Gizmos.DrawLine(c, b);
        // Arrowhead
        Vector3 ah = b + t.up * 0.5f;
        Gizmos.DrawLine(b, ah + t.right * 0.3f + t.forward * 0.3f);
        Gizmos.DrawLine(b, ah - t.right * 0.3f + t.forward * 0.3f);
        Gizmos.DrawLine(b, ah + t.right * 0.3f - t.forward * 0.3f);
        Gizmos.DrawLine(b, ah - t.right * 0.3f - t.forward * 0.3f);
    }
#endif
}
