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
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return "Error: Not URP";

        var field = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);
        int defaultIndex = field != null ? (int)field.GetValue(pipeline) : 0;

        var listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var renderers = listField?.GetValue(pipeline) as ScriptableRendererData[];
        if (renderers == null || renderers.Length == 0 || defaultIndex >= renderers.Length)
            return "Error: No renderer";

        var rendererData = renderers[defaultIndex];
        sb.AppendLine("Renderer: " + rendererData.name);

        var casterShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Mine/Shaders/PCSS/CustomShadowCaster.shader");
        var pcssShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Mine/Shaders/PCSS/PCSS.shader");
        if (casterShader == null) return "Error: CustomShadowCaster.shader not found";
        if (pcssShader == null) return "Error: PCSS.shader not found";

        PCSSFeature existing = null;
        foreach (var f in rendererData.rendererFeatures)
        {
            if (f is PCSSFeature pcss) { existing = pcss; break; }
        }

        if (existing != null)
        {
            existing.settings.shadowCasterShader = casterShader;
            existing.settings.pcssShader = pcssShader;
            existing.Create();
            sb.AppendLine("Updated PCSSFeature (both shaders)");
        }
        else
        {
            var feature = ScriptableObject.CreateInstance<PCSSFeature>();
            feature.settings.shadowCasterShader = casterShader;
            feature.settings.pcssShader = pcssShader;
            feature.Create();
            rendererData.rendererFeatures.Add(feature);
            sb.AppendLine("Added PCSSFeature (both shaders)");
        }

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        sb.AppendLine("Done.");
        return sb.ToString();
    }
}
