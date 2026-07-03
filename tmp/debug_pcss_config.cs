using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return "No URP asset";

        // 遍历所有 RendererData 查找 PC_Renderer
        for (int i = 0; i < pipeline.m_RendererDataList.Length; i++)
        {
            var rd = pipeline.m_RendererDataList[i];
            if (rd == null) continue;

            sb.AppendLine($"Renderer[{i}]: {rd.name}");

            foreach (var feature in rd.rendererFeatures)
            {
                if (feature == null) { sb.AppendLine("  [null feature]"); continue; }
                var fn = feature.GetType().Name;
                sb.AppendLine($"  {fn} active={feature.isActive}");

                if (fn == "PCSSFeature")
                {
                    var fields = feature.GetType().GetFields(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance);

                    foreach (var f in fields)
                    {
                        var val = f.GetValue(feature);
                        if (val == null) sb.AppendLine($"    {f.Name}: NULL");
                        else if (f.Name == "settings")
                        {
                            var sf = val.GetType().GetFields(
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.Instance);
                            foreach (var s in sf)
                                sb.AppendLine($"    settings.{s.Name} = {s.GetValue(val) ?? "NULL"}");
                        }
                        else
                            sb.AppendLine($"    {f.Name} = {val}");
                    }
                }
            }
        }
        return sb.ToString();
    }
}
