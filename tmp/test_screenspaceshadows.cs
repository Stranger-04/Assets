using UnityEngine;
using UnityEditor;
public class Script {
    public static object Main() {
        var rd = AssetDatabase.LoadAssetAtPath("Assets/Settings/PC_Renderer.asset", typeof(ScriptableObject));
        var so = new SerializedObject(rd);
        var fp = so.FindProperty("m_RendererFeatures");
        for (int i = 0; i < fp.arraySize; i++) {
            var o = fp.GetArrayElementAtIndex(i).objectReferenceValue;
            if (o == null) continue;
            var os = new SerializedObject(o);
            var ap = os.FindProperty("m_Active");
            var n = o.GetType().Name;
            if (n == "ScreenSpaceShadows") { ap.boolValue = false; os.ApplyModifiedProperties(); }
            if (n == "PCSSFeature") { ap.boolValue = true; os.ApplyModifiedProperties(); }
        }
        AssetDatabase.SaveAssetIfDirty(rd);
        return "DONE: SSS=OFF, PCSS=ON";
    }
}
