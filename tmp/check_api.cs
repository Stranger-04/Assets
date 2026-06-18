using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class Script
{
    public static object Main()
    {
        // Check which RendererList-related types exist
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => {
                try { return a.GetTypes(); } catch { return new Type[0]; }
            })
            .Where(t => t.Name.Contains("RendererList") && t.IsPublic)
            .Select(t => $"{t.Namespace}.{t.Name}")
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var result = "=== RendererList types ===\n";
        foreach (var t in types)
            result += t + "\n";

        // Check UnsafeGraphContext methods
        var ctxType = typeof(UnsafeGraphContext);
        result += "\n=== UnsafeGraphContext methods ===\n";
        foreach (var m in ctxType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Render") || m.Name.Contains("Texture"))
            .Select(m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})"))
        {
            result += $"  {m}\n";
        }

        // Check RenderGraph methods for renderer list creation
        var rgType = typeof(RenderGraph);
        result += "\n=== RenderGraph methods (RendererList / Blit) ===\n";
        foreach (var m in rgType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("RendererList") || m.Name.Contains("Blit"))
            .Select(m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})"))
        {
            result += $"  {m}\n";
        }

        return result;
    }
}
