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
        var result = "";

        // RenderGraph.AddRasterRenderPass overloads
        var rgType = typeof(RenderGraph);
        result += "=== AddRasterRenderPass overloads ===\n";
        foreach (var m in rgType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "AddRasterRenderPass"))
        {
            result += $"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})\n";
        }

        // RasterRenderPassBuilder methods
        var builderTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => {
                try { return a.GetTypes(); } catch { return new Type[0]; }
            })
            .Where(t => t.Name == "RasterRenderPassBuilder" && t.IsPublic)
            .ToList();

        foreach (var bt in builderTypes)
        {
            result += $"\n=== {bt.FullName} methods ===\n";
            foreach (var m in bt.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Contains("SetRender") || m.Name.Contains("UseRenderer") || m.Name.Contains("AllowPass")))
            {
                result += $"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})\n";
            }
        }

        // ProfilingSampler parameter on ScriptableRenderPass
        result += "\n=== ScriptableRenderPass constructors ===\n";
        var srpType = typeof(UnityEngine.Rendering.Universal.ScriptableRenderPass);
        foreach (var ctor in srpType.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
        {
            // skip non-public
            if (!ctor.IsPublic && !ctor.IsFamily) continue;
            result += $"  ctor({string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})\n";
        }

        return result;
    }
}
