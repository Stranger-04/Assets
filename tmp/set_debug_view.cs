using System.Reflection;
using Mine.Chosen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var rendererPath = "Assets/Settings/PC_Renderer.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null)
            return "ERROR: PC_Renderer.asset not found";

        PickerFeature found = null;
        foreach (var f in rendererData.rendererFeatures)
        {
            if (f is PickerFeature pf)
            {
                found = pf;
                break;
            }
        }

        if (found == null)
            return "ERROR: PickerFeature not found in PC_Renderer";

        // 使用反射设置私有序列化字段 m_DebugView
        var field = typeof(PickerFeature).GetField("m_DebugView",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(found, PickerPass.DebugView.ObjectID);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            return $"DebugView set to: {field.GetValue(found)}";
        }

        return "ERROR: Could not find m_DebugView field.";
    }
}
