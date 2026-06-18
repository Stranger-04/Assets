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
        if (rendererData == null) return "ERROR: Renderer not found";

        // 1. 清理所有 null / 损坏的 feature
        int removed = rendererData.rendererFeatures.RemoveAll(f => f == null);
        var lines = $"Removed {removed} null slots from PC_Renderer.\n";

        // 2. 确保 PickerFeature 存在（作为子 asset）
        var pickerFeature = rendererData.rendererFeatures.OfType<PickerFeature>().FirstOrDefault();
        if (pickerFeature == null)
        {
            pickerFeature = ScriptableObject.CreateInstance<PickerFeature>();
            pickerFeature.name = "PickerFeature";
            AssetDatabase.AddObjectToAsset(pickerFeature, rendererPath);
            rendererData.rendererFeatures.Add(pickerFeature);
            lines += "PickerFeature created.\n";
        }
        else
            lines += "PickerFeature exists.\n";

        // 3. 确保 OutlineFeature 存在（作为子 asset）
        var outlineFeature = rendererData.rendererFeatures.OfType<OutlineFeature>().FirstOrDefault();
        if (outlineFeature == null)
        {
            outlineFeature = ScriptableObject.CreateInstance<OutlineFeature>();
            outlineFeature.name = "OutlineFeature";
            AssetDatabase.AddObjectToAsset(outlineFeature, rendererPath);
            rendererData.rendererFeatures.Add(outlineFeature);
            lines += "OutlineFeature created.\n";
        }
        else
            lines += "OutlineFeature exists.\n";

        // 4. 检查每个 feature 是否为 "fake null"（Unity 的假 null）
        lines += "\nFeature list:\n";
        foreach (var f in rendererData.rendererFeatures)
        {
            bool isRealNull = ReferenceEquals(f, null);
            bool isUnityNull = f == null;
            string name = isRealNull ? "REAL-NULL" :
                          isUnityNull ? "UNITY-NULL(overloaded==)" :
                          f.GetType().Name;
            lines += $"  - {name} (refNull={isRealNull}, eqNull={isUnityNull})\n";
        }

        // 5. 强制保存
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();

        return lines;
    }
}
