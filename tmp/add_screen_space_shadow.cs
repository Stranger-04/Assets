using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using System.Linq;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
            "Assets/Settings/PC_Renderer.asset");
        if (rendererData == null)
            return "ERROR: PC_Renderer.asset not found";

        var so = new SerializedObject(rendererData);
        var featuresProp = so.FindProperty("m_RendererFeatures");

        // Check existing
        for (int i = 0; i < featuresProp.arraySize; i++)
        {
            var elem = featuresProp.GetArrayElementAtIndex(i);
            var obj = elem.objectReferenceValue;
            if (obj != null && obj.GetType().Name == "ScreenSpaceShadows")
            {
                sb.AppendLine("ScreenSpaceShadows already present.");
                return sb.ToString();
            }
        }

        // Create via reflection (internal class in URP assembly)
        var urpAssembly = typeof(UniversalRendererData).Assembly;
        var type = urpAssembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceShadows");
        if (type == null)
        {
            sb.AppendLine("Type not found. Available types containing 'ScreenSpace':");
            foreach (var t in urpAssembly.GetTypes())
                if (t.Name.Contains("ScreenSpace"))
                    sb.AppendLine($"  {t.FullName}");
            return sb.ToString();
        }

        var instance = ScriptableObject.CreateInstance(type);
        instance.name = "ScreenSpaceShadows";

        // Add to asset so it's saved with the renderer
        AssetDatabase.AddObjectToAsset(instance, rendererData);
        AssetDatabase.SaveAssetIfDirty(rendererData);

        // Now add to features array
        featuresProp.arraySize++;
        var lastIdx = featuresProp.arraySize - 1;
        var lastElem = featuresProp.GetArrayElementAtIndex(lastIdx);
        lastElem.objectReferenceValue = instance;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        sb.AppendLine("ScreenSpaceShadows added to PC_Renderer successfully!");
        return sb.ToString();
    }
}
