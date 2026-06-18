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

        rendererData.rendererFeatures.RemoveAll(f => f == null);

        var existing = rendererData.rendererFeatures.OfType<OutlineFeature>().FirstOrDefault();
        if (existing == null)
        {
            var feature = ScriptableObject.CreateInstance<OutlineFeature>();
            feature.name = "OutlineFeature";
            AssetDatabase.AddObjectToAsset(feature, rendererPath);
            rendererData.rendererFeatures.Add(feature);
        }

        // 强制设置 DebugShowMask = false
        var field = typeof(OutlineFeature).GetField("m_DebugShowMask",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(existing, false);
        }

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var dv = field?.GetValue(existing);
        return $"OutlineFeature ready. debugShowMask={dv}";
    }
}
