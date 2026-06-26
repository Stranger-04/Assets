using UnityEngine;

/// <summary>
/// Curve baking dispatcher. Delegates to individual curve type implementations
/// in <see cref="CatmullRomCurve"/> / <see cref="BezierCurve"/>.
/// To add a new curve type: create a new file in Scripts/Curves/ with a
/// <c>public static CurveBake.Result Bake(Vector3[], int, bool)</c> method,
/// then register it in the switch below.
/// </summary>
public static class CurveBake
{
    public struct Result
    {
        public Vector3[] positions;
        public Vector3[] tangents;
        public float[]   arcLengths;
        public Vector3[] normals;       // curvature normal (points toward bend center)
        public float[]   curvatures;     // scalar curvature κ = 1/radius
        public float     totalLength;
        public int       sampleCount => positions?.Length ?? 0;
    }

    // ── Public ──────────────────────────────────────────────

    /// <summary>Bake control points into uniformly sampled curve data.</summary>
    public static Result Bake(Vector3[] controlPoints, CurveAsset.CurveType type,
                              CurveAsset.CurveDimension dimension, int sampleCount, bool loop)
    {
        Vector3[] pts = ProjectPositions(controlPoints, dimension);
        return type switch
        {
            CurveAsset.CurveType.CatmullRom => CatmullRomCurve.Bake(pts, sampleCount, loop),
            CurveAsset.CurveType.Bezier     => BezierCurve.Bake(pts, sampleCount, loop),
            _ => default,
        };
    }

    /// <summary>Project 3D positions onto a 2D plane (average depth).</summary>
    public static Vector3[] ProjectPositions(Vector3[] pts, CurveAsset.CurveDimension dim)
    {
        if (pts == null || pts.Length == 0) return pts;
        if (dim == CurveAsset.CurveDimension.XYZ) return (Vector3[])pts.Clone();

        float avgX = 0, avgY = 0, avgZ = 0;
        foreach (var p in pts) { avgX += p.x; avgY += p.y; avgZ += p.z; }
        avgX /= pts.Length; avgY /= pts.Length; avgZ /= pts.Length;

        var projected = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            projected[i] = dim switch
            {
                CurveAsset.CurveDimension.XY => new Vector3(pts[i].x, pts[i].y, avgZ),
                CurveAsset.CurveDimension.XZ => new Vector3(pts[i].x, avgY, pts[i].z),
                CurveAsset.CurveDimension.YZ => new Vector3(avgX, pts[i].y, pts[i].z),
                _ => pts[i],
            };
        }
        return projected;
    }
}
