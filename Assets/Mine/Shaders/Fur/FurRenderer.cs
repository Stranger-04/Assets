using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class FurRenderer : MonoBehaviour
{
    public enum FurMode { Shell, Fin }
    public FurMode mode = FurMode.Shell;

    [Header("Material")]
    public Material material;

    [Header("Shell Parameters")]
    [Range(1, 42)] public int shellLayerCount = 16;
    public float shellLayerStep = 0.001f;

    [Header("Fin Parameters")]
    public int finCount = 10000;
    public float finWidth = 0.005f;
    public float finLength = 0.1f;
    public float bendStrength = 2.0f;

    [Header("Shared Fur")]
    [Range(0.0f, 1.0f)] public float alphaCutout = 0.3f;
    [Range(0.0f, 10.0f)] public float furScale = 1.0f;
    [Range(0.0f, 1.0f)] public float occlusion = 0.5f;

    [Header("Motion")]
    public Vector3 baseMove = new Vector3(0.0f, -0.0f, 0.0f);
    [Range(0.0f, 3.0f)] public float baseMoveExponent = 3.0f;
    public Vector3 gravityDirection = Vector3.down;
    [Range(0.0f, 5.0f)] public float gravityStrength = 1.0f;

    [Header("Inertia")]
    [Range(0.0f, 1.0f)] public float inertia = 0.5f;

    // ---- hair point data (matches HairPoint struct in Fin.shader) ----
    private struct HairPoint
    {
        public Vector3 positionOS;
        public Vector3 normalOS;
        public Vector2 uv;
        public float seed;
    }

    private Mesh _sourceMesh;
    private Mesh _finMesh;
    private ComputeBuffer _pointBuffer;
    private MaterialPropertyBlock _props;
    private Matrix4x4[] _identityMatrices;          // reusable, sized to finCount
    private List<Matrix4x4> _shellMatrices;         // rebuilt each frame
    private Vector3 _laggedPosition;

    // ============================================================
    void OnEnable()
    {
        var mf = GetComponent<MeshFilter>();
        _sourceMesh = mf != null ? mf.sharedMesh : null;

        _props = new MaterialPropertyBlock();
        _laggedPosition = transform.position;

        _finMesh = CreateFinMesh();

        if (mode == FurMode.Fin)
            RebuildPoints();
    }

    void OnDisable()
    {
        _pointBuffer?.Release();
        _pointBuffer = null;
    }

    void OnValidate()
    {
        if (Application.IsPlaying(this)) return;
        if (isActiveAndEnabled)
        {
            OnDisable();
            OnEnable();
        }
    }

    // ============================================================
    //  Public: force resample hair points
    // ============================================================
    public void RebuildPoints()
    {
        _pointBuffer?.Release();
        if (_sourceMesh == null) return;

        int count = Mathf.Max(finCount, 1);
        HairPoint[] points = SampleMeshSurface(_sourceMesh, count);
        _pointBuffer = new ComputeBuffer(count, sizeof(float) * (3 + 3 + 2 + 1));
        _pointBuffer.SetData(points);

        if (_identityMatrices == null || _identityMatrices.Length != count)
        {
            _identityMatrices = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
                _identityMatrices[i] = Matrix4x4.identity;
        }
    }

    // ============================================================
    void LateUpdate()
    {
        if (material == null) return;

        // --- shared: inertia ---
        Vector3 currentPos = transform.position;
        _laggedPosition = Vector3.Lerp(_laggedPosition, currentPos, inertia);
        Vector3 lagOffset = _laggedPosition - currentPos;

        // --- shared: material properties ---
        _props.SetFloat("_AlphaCutout", alphaCutout);
        _props.SetFloat("_FurScale", furScale);
        _props.SetFloat("_Occlusion", occlusion);
        _props.SetVector("_BaseMove",
            new Vector4(baseMove.x, baseMove.y, baseMove.z, baseMoveExponent));
        _props.SetVector("_Gravity",
            new Vector4(gravityDirection.x, gravityDirection.y, gravityDirection.z, gravityStrength));
        _props.SetVector("_LagOffset", lagOffset);

        // ===== SHELL MODE =====
        if (mode == FurMode.Shell)
        {
            if (_sourceMesh == null) return;

            int count = Mathf.Max(shellLayerCount, 1);
            _props.SetFloat("_LayerCount", count);
            _props.SetFloat("_LayerStep", shellLayerStep);

            // build matrices — each instance gets the same real transform
            if (_shellMatrices == null) _shellMatrices = new List<Matrix4x4>();
            _shellMatrices.Clear();
            Matrix4x4 m = transform.localToWorldMatrix;
            for (int i = 0; i < count; i++)
                _shellMatrices.Add(m);

            Graphics.DrawMeshInstanced(_sourceMesh, 0, material, _shellMatrices, _props);
        }
        // ===== FIN MODE =====
        else
        {
            if (_finMesh == null || _pointBuffer == null) return;

            _props.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
            _props.SetMatrix("_WorldToObject", transform.worldToLocalMatrix);
            _props.SetBuffer("_HairPoints", _pointBuffer);
            _props.SetFloat("_FinWidth", finWidth);
            _props.SetFloat("_FinLength", finLength);
            _props.SetFloat("_BendStrength", bendStrength);

            int total = _identityMatrices.Length;
            for (int start = 0; start < total; start += 1023)
            {
                int batchCount = System.Math.Min(1023, total - start);
                _props.SetInt("_BaseInstance", start);
                Graphics.DrawMeshInstanced(
                    _finMesh, 0, material, _identityMatrices, batchCount, _props);
            }
        }
    }

    // ============================================================
    //  Fin mesh: a thin quad (blade of grass)
    // ============================================================
    private static Mesh CreateFinMesh()
    {
        var mesh = new Mesh();
        mesh.name = "FinQuad";
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0.0f, 0.0f),
            new Vector3( 0.5f, 0.0f, 0.0f),
            new Vector3(-0.5f, 1.0f, 0.0f),
            new Vector3( 0.5f, 1.0f, 0.0f),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 1.0f),
        };
        mesh.triangles = new int[] { 0, 1, 2, 1, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // ============================================================
    //  Mesh surface sampling via barycentric distribution
    // ============================================================
    private static HairPoint[] SampleMeshSurface(Mesh mesh, int count)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] tris = mesh.triangles;
        int triCount = tris.Length / 3;

        float[] triAreas = new float[triCount];
        double totalArea = 0.0;
        for (int i = 0; i < triCount; i++)
        {
            Vector3 v0 = verts[tris[i * 3]];
            Vector3 v1 = verts[tris[i * 3 + 1]];
            Vector3 v2 = verts[tris[i * 3 + 2]];
            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            triAreas[i] = area;
            totalArea += area;
        }

        double invTotal = 1.0 / totalArea;
        for (int i = 0; i < triCount; i++)
        {
            triAreas[i] = (float)(triAreas[i] * invTotal);
            if (i > 0) triAreas[i] += triAreas[i - 1];
        }
        triAreas[triCount - 1] = 1.0f;

        var rng = new System.Random(42);
        var points = new HairPoint[count];
        for (int i = 0; i < count; i++)
        {
            double r = rng.NextDouble();
            int ti = 0;
            for (int j = 0; j < triCount; j++)
            {
                if (r <= triAreas[j]) { ti = j; break; }
            }

            int i0 = tris[ti * 3];
            int i1 = tris[ti * 3 + 1];
            int i2 = tris[ti * 3 + 2];

            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();
            if (u + v > 1.0f) { u = 1.0f - u; v = 1.0f - v; }
            float w = 1.0f - u - v;

            points[i] = new HairPoint
            {
                positionOS = verts[i0] * w + verts[i1] * u + verts[i2] * v,
                normalOS = (norms[i0] * w + norms[i1] * u + norms[i2] * v).normalized,
                uv = uvs[i0] * w + uvs[i1] * u + uvs[i2] * v,
                seed = (float)rng.NextDouble(),
            };
        }
        return points;
    }
}
