using System.Linq;
using Mine.Picker;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var path = "Assets/Settings/PC_Renderer.asset";
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (rd == null) return "ERROR";

        // 移除旧 feature
        rd.rendererFeatures.RemoveAll(f => f == null || f is PickerFeature || f is OutlineFeature);

        // 重建
        var pf = ScriptableObject.CreateInstance<PickerFeature>();
        pf.name = "Picker";
        AssetDatabase.AddObjectToAsset(pf, path);
        rd.rendererFeatures.Add(pf);

        var of = ScriptableObject.CreateInstance<OutlineFeature>();
        of.name = "Outline";
        AssetDatabase.AddObjectToAsset(of, path);
        rd.rendererFeatures.Add(of);

        EditorUtility.SetDirty(rd);
        AssetDatabase.SaveAssets();
        return $"Features rebuilt. Count: {rd.rendererFeatures.Count}";
    }
}
