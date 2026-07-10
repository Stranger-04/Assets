using UnityEngine;
using UnityEditor;

public class Script
{
    public static object Main()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Mine/Shaders/PCSS/PCSSDebugSurface.shader");
        if (shader == null) return "Shader not found";

        var mat = new Material(shader);
        var plane = GameObject.Find("ShadowTestPlane");
        if (plane == null) return "Plane not found";

        plane.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // 移除相机的 showShadow 干扰
        var light = GameObject.FindObjectOfType<Light>();
        return $"Material assigned to ShadowTestPlane. Light: {light?.name}, type: {light?.type}";
    }
}
