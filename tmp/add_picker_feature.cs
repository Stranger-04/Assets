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

        // 检查是否已存在
        foreach (var f in rendererData.rendererFeatures)
        {
            if (f is PickerFeature)
                return "PickerFeature already exists in PC_Renderer.";
        }

        // 创建并配置 PickerFeature
        var feature = ScriptableObject.CreateInstance<PickerFeature>();
        feature.name = "PickerFeature";

        // 添加后自动序列化到 renderer asset
        rendererData.rendererFeatures.Add(feature);

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "PickerFeature added to PC_Renderer. You can now test RT output.\n"
             + "To visualize: select PC_Renderer, set Debug View to ObjectID in Inspector.";
    }
}
