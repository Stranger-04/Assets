using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Reflection;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        string path = "Assets/Settings/PC_Renderer.asset";

        var rendererData = AssetDatabase.LoadAssetAtPath(path, typeof(ScriptableRendererData));
        var so = new SerializedObject(rendererData);
        var featuresProp = so.FindProperty("m_RendererFeatures");

        for (int i = 0; i < featuresProp.arraySize; i++)
        {
            var elem = featuresProp.GetArrayElementAtIndex(i);
            var obj = elem.objectReferenceValue; // The ScriptableRendererFeature
            if (obj == null) continue;

            string fname = obj.GetType().Name;

            // Disable ScreenSpaceShadows permanently
            if (fname == "ScreenSpaceShadows")
            {
                var objSO = new SerializedObject(obj);
                var activeProp = objSO.FindProperty("m_Active");
                if (activeProp != null)
                {
                    sb.AppendLine($"  [{i}] ScreenSpaceShadows: {activeProp.boolValue} → FALSE");
                    activeProp.boolValue = false;
                    objSO.ApplyModifiedProperties();
                }
                else sb.AppendLine($"  [{i}] ScreenSpaceShadows: m_Active not found");
            }
            sb.AppendLine($"  [{i}] {fname}");
        }

        // Save
        AssetDatabase.SaveAssetIfDirty(rendererData);
        AssetDatabase.SaveAssets();

        // Verify
        sb.AppendLine("\nAfter save:");
        for (int i = 0; i < featuresProp.arraySize; i++)
        {
            var obj = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue;
            if (obj == null) continue;
            var objSO = new SerializedObject(obj);
            var ap = objSO.FindProperty("m_Active");
            sb.AppendLine($"  [{i}] {obj.GetType().Name} m_Active={ap?.boolValue}");
        }

        // 加载 shader 和配置 PCSSFeature
        var pcssShader = AssetDatabase.LoadAssetAtPath(
            "Assets/Mine/Shaders/PCSS/PCSS.shader", typeof(Shader)) as Shader;
        sb.AppendLine($"\nShader: {(pcssShader != null ? pcssShader.name : "NULL")}");

        var ff = typeof(ScriptableRendererData).GetField("m_RendererFeatures",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var features = ff.GetValue(rendererData) as System.Collections.IList;
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i]?.GetType().Name == "PCSSFeature")
            {
                var f = features[i];
                var sf = f.GetType().GetField("settings", BindingFlags.Public | BindingFlags.Instance);
                var s = sf.GetValue(f);
                var shf = s.GetType().GetField("pcssShader", BindingFlags.Public | BindingFlags.Instance);
                shf.SetValue(s, pcssShader);

                // Clear material
                var mf = f.GetType().GetField("m_Material", BindingFlags.NonPublic | BindingFlags.Instance);
                mf.SetValue(f, null);

                // Re-Create
                var cm = f.GetType().BaseType.GetMethod("Create", BindingFlags.Public | BindingFlags.Instance);
                cm.Invoke(f, null);

                var mat = mf.GetValue(f) as Material;
                sb.AppendLine($"PCSSFeature re-created: mat={(mat != null ? mat.shader.name : "NULL")}");
            }
        }

        EditorUtility.SetDirty(rendererData);
        return sb.ToString();
    }
}
