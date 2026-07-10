using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Reflection;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();

        // 1. Check RenderSettings.sun
        var sun = RenderSettings.sun;
        sb.AppendLine("RenderSettings.sun: " + (sun != null ? sun.name : "NULL !!!"));
        if (sun != null)
            sb.AppendLine("  Light type: " + sun.type + ", forward: " + sun.transform.forward);

        // 2. Check if any directional light exists
        var allLights = Object.FindObjectsOfType<Light>();
        int dirCount = 0;
        foreach (var l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                dirCount++;
                sb.AppendLine("Directional Light: " + l.name + " (sun=" + (l == sun) + ")");
            }
        }
        sb.AppendLine("Total dir lights: " + dirCount);

        // 3. Check PCSSFeature settings
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            var listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var renderers = listField?.GetValue(pipeline) as ScriptableRendererData[];
            var idxField = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex",
                BindingFlags.NonPublic | BindingFlags.Instance);
            int idx = idxField != null ? (int)idxField.GetValue(pipeline) : 0;

            if (renderers != null && idx < renderers.Length && renderers[idx] != null)
            {
                var rd = renderers[idx];
                sb.AppendLine("\nRenderer: " + rd.name);
                foreach (var f in rd.rendererFeatures)
                {
                    if (f is PCSSFeature pcss)
                    {
                        sb.AppendLine("  PCSSFeature found");
                        sb.AppendLine("    shadowCasterShader: " + (pcss.settings.shadowCasterShader != null ? "SET" : "NULL !!!"));
                        sb.AppendLine("    pcssShader: " + (pcss.settings.pcssShader != null ? "SET" : "NULL !!!"));
                        sb.AppendLine("    showShadowMap: " + pcss.settings.showShadowMap);
                    }
                }
            }
        }

        return sb.ToString();
    }
}
