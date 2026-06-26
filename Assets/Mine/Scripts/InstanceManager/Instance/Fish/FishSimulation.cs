using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Fish particle simulation — particles are distributed along and inside a
/// CurveAsset tube, advancing purely by path sampling. No emission plane, no forces.
/// </summary>
public class FishSimulation : MonoBehaviour, IUniversalInstanceSimulator
{
    [Header("Compute")]
    public ComputeShader fishShader;

    [Header("Path")]
    public CurveAsset curveAsset;
    public float curveSpeed = 8f;
    public float tubeRadius = 2f;

    [Header("Behaviour")]
    [Range(0f, 1f)] public float speedVariation = 0.5f;
    [Range(0f, 1f)] public float positionSmooth = 0.3f;

    private const int MaxCurveSamples = 1024;

    [StructLayout(LayoutKind.Sequential)]
    private struct Particle
    {
        public Vector3 position;
        public float curveT;
        public float radialAngle;
        public float radialDist;

        public static int Stride() => sizeof(float) * 6;
    }

    private ComputeBuffer particleBuffer;
    private ComputeBuffer curveBuffer;
    private int instanceCount;
    private int updateKernel;
    private int curveSampleCount;

    private static readonly int ParticleBufferId   = Shader.PropertyToID("_ParticleBuffer");
    private static readonly int CurveBufferId      = Shader.PropertyToID("_CurveBuffer");
    private static readonly int CurveSampleCountId = Shader.PropertyToID("_CurveSampleCount");
    private static readonly int InstanceCountId    = Shader.PropertyToID("_InstanceCount");
    private static readonly int DeltaTimeId        = Shader.PropertyToID("_DeltaTime");
    private static readonly int CurveSpeedId       = Shader.PropertyToID("_CurveSpeed");
    private static readonly int TubeRadiusId       = Shader.PropertyToID("_TubeRadius");
    private static readonly int TotalLengthId      = Shader.PropertyToID("_TotalLength");
    private static readonly int CurveLoopId        = Shader.PropertyToID("_CurveLoop");
    private static readonly int SpeedVariationId   = Shader.PropertyToID("_SpeedVariation");
    private static readonly int PositionSmoothId   = Shader.PropertyToID("_PositionSmooth");

    public ComputeBuffer VisibleCountBuffer => null;

    // ════════════════════════════════════════════════════════════
    //  Debug — read back particle positions from GPU
    // ════════════════════════════════════════════════════════════
    public string DebugReadback(int maxParticles)
    {
        if (particleBuffer == null || curveAsset == null) return "buffer or curve null";

        int n = Mathf.Min(maxParticles, instanceCount);
        var data = new Particle[n];
        particleBuffer.GetData(data, 0, 0, n);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- {n} particles (curve samples={curveSampleCount}, totalLength={curveAsset.totalLength:F1}) ---");

        int badCount = 0;
        for (int i = 0; i < n; i++)
        {
            float bestDist = float.MaxValue;
            float bestT = 0;
            var positions = curveAsset.positions;
            for (int j = 0; j < positions.Length; j++)
            {
                float d = Vector3.Distance(data[i].position, positions[j]);
                if (d < bestDist) { bestDist = d; bestT = (float)j / positions.Length; }
            }

            if (bestDist > tubeRadius * 2f)
            {
                badCount++;
                if (badCount <= 5)
                    sb.AppendLine($"  BAD  [{i}] pos={data[i].position:F2} curveT={data[i].curveT:F3} nearestDist={bestDist:F2} nearestT={bestT:F3}");
            }
            else if (i < 3)
            {
                sb.AppendLine($"  OK   [{i}] pos={data[i].position:F2} curveT={data[i].curveT:F3} nearestDist={bestDist:F2}");
            }
        }

        sb.AppendLine($"Result: {n - badCount} OK / {badCount} BAD (dist > {tubeRadius * 2f})");
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════
    //  Initialize
    // ════════════════════════════════════════════════════════════
    public void Initialize(int count)
    {
        instanceCount = count;
        updateKernel  = fishShader.FindKernel("CS_FishUpdate");
        UploadCurveData();
        SpawnParticles();
    }

    // ════════════════════════════════════════════════════════════
    //  Dispatch
    // ════════════════════════════════════════════════════════════
    public void Dispatch(float deltaTime)
    {
        if (curveAsset == null) return;

        fishShader.SetFloat(DeltaTimeId, deltaTime);
        fishShader.SetFloat(CurveSpeedId, curveSpeed);
        fishShader.SetFloat(TubeRadiusId, tubeRadius);
        fishShader.SetFloat(TotalLengthId, curveAsset.totalLength);
        fishShader.SetInt(InstanceCountId, instanceCount);
        fishShader.SetInt(CurveSampleCountId, curveSampleCount);
        fishShader.SetBool(CurveLoopId, curveAsset.loop);
        fishShader.SetFloat(SpeedVariationId, speedVariation);
        fishShader.SetFloat(PositionSmoothId, positionSmooth);

        int threadGroups = Mathf.CeilToInt((float)instanceCount / 64f);
        fishShader.Dispatch(updateKernel, threadGroups, 1, 1);
    }

    public void BindMaterial(Material material)
    {
        material.SetBuffer(ParticleBufferId, particleBuffer);
    }

    public void Release()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (curveBuffer != null) { curveBuffer.Release(); curveBuffer = null; }
    }

    // ════════════════════════════════════════════════════════════
    //  Curve upload
    // ════════════════════════════════════════════════════════════
    private void UploadCurveData()
    {
        if (curveAsset == null || curveAsset.positions == null) return;

        curveSampleCount = Mathf.Min(curveAsset.sampleCount, MaxCurveSamples);
        var data = new Vector4[curveSampleCount];
        for (int i = 0; i < curveSampleCount; i++)
        {
            Vector3 p = curveAsset.positions[i];
            data[i] = new Vector4(p.x, p.y, p.z, (float)i / Mathf.Max(1, curveSampleCount - 1));
        }

        if (curveBuffer != null) curveBuffer.Release();
        curveBuffer = new ComputeBuffer(curveSampleCount, sizeof(float) * 4);
        curveBuffer.SetData(data);

        fishShader.SetBuffer(updateKernel, CurveBufferId, curveBuffer);
    }

    // ════════════════════════════════════════════════════════════
    //  Spawn — particles distributed along the curve, inside tube
    // ════════════════════════════════════════════════════════════
    private void SpawnParticles()
    {
        Particle[] particles = new Particle[instanceCount];
        bool hasCurve = curveAsset != null && curveAsset.positions != null && curveAsset.positions.Length >= 2;

        for (int i = 0; i < instanceCount; i++)
        {
            float ct = (float)i / instanceCount;

            if (hasCurve)
            {
                curveAsset.Sample(ct, out Vector3 curvePos, out Vector3 tangent);

                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.999f) up = Vector3.right;
                Vector3 right = Vector3.Cross(tangent, up).normalized;
                Vector3 curveUp = Vector3.Cross(right, tangent).normalized;

                float angle = Random.value * Mathf.PI * 2f;
                float dist  = Mathf.Sqrt(Random.value) * tubeRadius;

                particles[i].position    = curvePos + right * Mathf.Cos(angle) * dist + curveUp * Mathf.Sin(angle) * dist;
                particles[i].curveT      = ct;
            }
            else
            {
                particles[i].position    = Vector3.zero;
                particles[i].curveT      = 0f;
            }

            particles[i].radialAngle = Random.value * Mathf.PI * 2f;
            particles[i].radialDist  = Mathf.Sqrt(Random.value) * tubeRadius;
        }

        if (particleBuffer != null) particleBuffer.Release();
        particleBuffer = new ComputeBuffer(instanceCount, Particle.Stride());
        particleBuffer.SetData(particles);

        fishShader.SetBuffer(updateKernel, ParticleBufferId, particleBuffer);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos — curve path + tube radius
    // ════════════════════════════════════════════════════════════
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (curveAsset == null || curveAsset.positions == null || curveAsset.positions.Length < 2) return;

        Vector3[] pts = curveAsset.positions;
        Vector3[] tng = curveAsset.tangents;
        if (tng == null || tng.Length != pts.Length) return;

        // ── Curve path ─────────────────────────────────────────
        Gizmos.color = new Color(0, 1, 1, 0.9f);
        for (int i = 0; i < pts.Length - 1; i++)
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        if (curveAsset.loop)
            Gizmos.DrawLine(pts[pts.Length - 1], pts[0]);

        // ── Tube rings ─────────────────────────────────────────
        int ringCount = 32;
        int ringSegments = 16;
        float ringStep = 1f / ringCount;
        Color ringColor = new Color(1, 1, 0, 0.25f);
        for (int r = 0; r <= ringCount; r++)
        {
            float t = r * ringStep;
            int idx = Mathf.Min((int)(t * (pts.Length - 1)), pts.Length - 1);

            Vector3 tangent = tng[idx];
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.999f) up = Vector3.right;
            Vector3 right = Vector3.Cross(tangent, up).normalized;
            Vector3 curveUp = Vector3.Cross(right, tangent).normalized;

            Gizmos.color = ringColor;
            for (int s = 0; s < ringSegments; s++)
            {
                float a0 = (float)s / ringSegments * Mathf.PI * 2f;
                float a1 = (float)(s + 1) / ringSegments * Mathf.PI * 2f;
                Vector3 p0 = pts[idx] + right * Mathf.Cos(a0) * tubeRadius + curveUp * Mathf.Sin(a0) * tubeRadius;
                Vector3 p1 = pts[idx] + right * Mathf.Cos(a1) * tubeRadius + curveUp * Mathf.Sin(a1) * tubeRadius;
                Gizmos.DrawLine(p0, p1);
            }
        }

        // ── Direction arrow ────────────────────────────────────
        Vector3 start = pts[0];
        Vector3 startT = tng[0];
        Vector3 arrowBase = start + startT * 0.5f;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, arrowBase);
        Vector3 arrowUp = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(startT, arrowUp)) > 0.999f) arrowUp = Vector3.right;
        Vector3 arrowR = Vector3.Cross(startT, arrowUp).normalized;
        arrowUp = Vector3.Cross(arrowR, startT).normalized;
        Gizmos.DrawLine(arrowBase, arrowBase + (-startT * 0.2f + arrowR * 0.15f) * 0.5f);
        Gizmos.DrawLine(arrowBase, arrowBase + (-startT * 0.2f - arrowR * 0.15f) * 0.5f);
        Gizmos.DrawLine(arrowBase, arrowBase + (-startT * 0.2f + arrowUp * 0.15f) * 0.5f);
        Gizmos.DrawLine(arrowBase, arrowBase + (-startT * 0.2f - arrowUp * 0.15f) * 0.5f);
    }
#endif
}
