using UnityEngine;
using UnityEditor;
public class Script {
    public static object Main() {
        AssetDatabase.ImportAsset("Assets/Mine/Shaders/PCSS/PCSS.shader",
            ImportAssetOptions.ForceUpdate);
        var s = AssetDatabase.LoadAssetAtPath("Assets/Mine/Shaders/PCSS/PCSS.shader", typeof(Shader)) as Shader;
        return $"Shader: {(s!=null ? s.name + " passes=" + s.passCount + " supported=" + s.isSupported : "BROKEN")}";
    }
}
