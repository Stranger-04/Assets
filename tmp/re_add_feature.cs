using System.Linq;
using System.Reflection;
using Mine.Chosen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var rendererPath = "Assets/Settings/PC_Renderer.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null) return "ERROR: PC_Renderer.asset not found";

        // 移除所有 null slot（损坏的引用）
        rendererData.rendererFeatures.RemoveAll(f => f == null);

        // 检查 PickerFeature 是否已正确存在
        var existing = rendererData.rendererFeatures.OfType<PickerFeature>().FirstOrDefault();
        if (existing != null)
        {
            var field = typeof(PickerFeature).GetField("m_DebugView",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(existing, PickerPass.DebugView.Off);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            return $"PickerFeature already present (debug=OFF). Features: {rendererData.rendererFeatures.Count}";
        }

        // 创建并正确注册为 sub-asset
        var feature = ScriptableObject.CreateInstance<PickerFeature>();
        feature.name = "PickerFeature";

        // 关键：注册到 renderer asset 中，生成稳定的 fileID
        AssetDatabase.AddObjectToAsset(feature, rendererPath);
        rendererData.rendererFeatures.Add(feature);

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"PickerFeature added as sub-asset. Features: {rendererData.rendererFeatures.Count}\n"
             + string.Join("\n", rendererData.rendererFeatures
                 .Select(f => f != null ? $"  - {f.GetType().Name} [OK]" : "  - (null) [MISSING]"));
    }
}
