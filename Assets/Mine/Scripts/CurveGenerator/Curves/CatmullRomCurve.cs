using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Catmull-Rom spline: passes through all control points, C1 continuous.
/// </summary>
public static class CatmullRomCurve
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
            int p0, p1, p2, p3;
            if (closed)
            {
                p0 = (seg - 1 + pts.Length) % pts.Length;
                p1 = seg;
                p2 = (seg + 1) % pts.Length;
                p3 = (seg + 2) % pts.Length;
            }
            else
            {
                p0 = Mathf.Max(seg - 1, 0);
                p1 = seg;
                p2 = seg + 1;
                p3 = Mathf.Min(seg + 2, pts.Length - 1);
            }

            for (int i = 0; i < samplesPerSeg; i++)
            {
                float t = (float)i / samplesPerSeg;
                Vector3 pos  = Position(pts[p0], pts[p1], pts[p2], pts[p3], t);
                Vector3 tang = Tangent(pts[p0], pts[p1], pts[p2], pts[p3], t);

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

    // ── Math ────────────────────────────────────────────────

    public static Vector3 Position(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            (2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public static Vector3 Tangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return (0.5f * (
            (-p0 + p2)
            + (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t
            + (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2)).normalized;
    }
}
