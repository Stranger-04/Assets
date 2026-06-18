using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

public class Script
{
    public static object Main()
    {
        var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
        var lines = new List<string>();
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (rd != null)
            {
                lines.Add($"Renderer: {rd.name} at {path}");
                lines.Add($"  Features count: {rd.rendererFeatures.Count}");
                foreach (var f in rd.rendererFeatures)
                {
                    if (f != null) lines.Add($"  - {f.GetType().Name}");
                    else lines.Add("  - (null slot)");
                }
            }
        }
        return string.Join("\n", lines);
    }
}
