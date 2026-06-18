using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var t = typeof(UniversalRenderPipelineAsset);
        var lines = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.ToLower().Contains("renderer") || p.Name.ToLower().Contains("script"))
            .Select(p => $"{p.PropertyType.Name} {p.Name}")
            .ToList();
        return "URP Asset properties:\n" + string.Join("\n", lines);
    }
}
