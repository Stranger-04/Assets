using UnityEngine;

public class Script
{
    public static object Main()
    {
        var shader = Shader.Find("Mine/PCSS/DebugPlane");
        if (shader == null) return "Error: DebugPlane shader not found";

        var mat = new Material(shader);

        var existing = GameObject.Find("__PCSS_DebugPlane__");
        if (existing != null) Object.DestroyImmediate(existing);

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "__PCSS_DebugPlane__";
        plane.transform.position = new Vector3(0, 2, 3);
        plane.transform.localScale = new Vector3(3, 1, 3);
        plane.GetComponent<Renderer>().material = mat;

        return "DebugPlane created at (0, 2, 3), scale 3x3. Samples _PCSS_ShadowCacheTex directly.";
    }
}
