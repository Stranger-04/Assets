using UnityEngine;
using UnityEditor;
public class Script {
    public static object Main() {
        var shader = AssetDatabase.LoadAssetAtPath(
            "Assets/Mine/Shaders/PCSS/PCSS.shader", typeof(Shader)) as Shader;
        return $"Shader: {(shader != null ? shader.name + " (" + shader.passCount + " passes)" : "STILL NULL")}";
    }
}
