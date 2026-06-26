using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Cubic Bezier spline with auto-computed handles (Catmull-Rom 1/6 factor).
/// Passes through all control points using central-difference tangent estimation.
/// </summary>
public static class BezierCurve
{
    public static CurveBake.Result Bake(Vector3[] pts, int samples, bool closed)
    {
        int segCount = closed ? pts.Length : pts.Length - 1;
        int samplesPerSeg = samples / segCount;

        var positions  = new List<Vector3>();
        var tangents   = new List<Vector3>();
        var arcLengths = new List<float>();
        var normals    = new List<Vector3>();
        var curvatures = new List<float>();

        float totalLength = 0f;
        Vector3 lastPos = Vector3.zero;
        bool first = true;

        for (int seg = 0; seg < segCount; seg++)
        {
            int i0 = seg;
            int i1 = closed ? (seg + 1) % pts.Length : seg + 1;

            // Auto-handles from central-difference tangents
            Vector3 B0 = pts[i0];
            Vector3 B3 = pts[i1];
            Vector3 t0 = TangentAt(pts, i0, closed);
            Vector3 t1 = TangentAt(pts, i1, closed);
            Vector3 B1 = B0 + t0 / 6f;
            Vector3 B2 = B3 - t1 / 6f;

            for (int i = 0; i < samplesPerSeg; i++)
            {
                float t = (float)i / samplesPerSeg;
                Vector3 pos  = Position(B0, B1, B2, B3, t);
                Vector3 tang = Tangent(B0, B1, B2, B3, t);

                if (!first) totalLength += Vector3.Distance(lastPos, pos);
                lastPos = pos;
                first = false;

                positions.Add(pos);
                tangents.Add(tang);
                arcLengths.Add(totalLength);
            }
        }

        // Final point
        if (!closed)
        {
            Vector3 last = pts[pts.Length - 1];
            positions.Add(last);
            tangents.Add(tangents.Count > 0 ? tangents[tangents.Count - 1] : Vector3.forward);
            if (positions.Count > 1)
                totalLength += Vector3.Distance(positions[positions.Count - 2], last);
            arcLengths.Add(totalLength);
        }

        // ── Curvature computation ─────────────────────────────
        int n = positions.Count;
        for (int i = 0; i < n; i++)
        {
            if (n < 3 || totalLength < 0.001f)
            {
                normals.Add(Vector3.right);
                curvatures.Add(0f);
                continue;
            }

            int i0, i1;
            if (closed)
            { i0 = (i - 1 + n) % n; i1 = (i + 1) % n; }
            else
            { i0 = Mathf.Max(0, i - 1); i1 = Mathf.Min(n - 1, i + 1); }

            Vector3 dT = tangents[i1] - tangents[i0];
            float ds = arcLengths[i1] - arcLengths[i0];
            float rawK = dT.magnitude / Mathf.Max(ds, 0.0001f);
            float curveK = Mathf.Min(rawK, 100f); // cap at radius ≈ 1cm
            Vector3 curveN = dT.sqrMagnitude > 0.0001f ? dT.normalized : Vector3.right;

            normals.Add(curveN);
            curvatures.Add(curveK);
        }

        return new CurveBake.Result
        {
            positions   = positions.ToArray(),
            tangents    = tangents.ToArray(),
            arcLengths  = arcLengths.ToArray(),
            normals     = normals.ToArray(),
            curvatures  = curvatures.ToArray(),
            totalLength = totalLength,
        };
    }

    // ── Auto-handle helpers ─────────────────────────────────

    private static Vector3 TangentAt(Vector3[] pts, int i, bool closed)
    {
        if (closed)
        {
            Vector3 prev = pts[(i - 1 + pts.Length) % pts.Length];
            Vector3 next = pts[(i + 1) % pts.Length];
            return next - prev;
        }
        if (i <= 0)               return pts[i + 1] - pts[i];
        if (i >= pts.Length - 1)  return pts[i] - pts[i - 1];
        return pts[i + 1] - pts[i - 1];
    }

    // ── Math ────────────────────────────────────────────────

    public static Vector3 Position(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        float uu = u * u, uuu = uu * u;
        float tt = t * t, ttt = tt * t;
        return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
    }

    public static Vector3 Tangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return (3f * u * u * (p1 - p0)
              + 6f * u * t * (p2 - p1)
              + 3f * t * t * (p3 - p2)).normalized;
    }
}
