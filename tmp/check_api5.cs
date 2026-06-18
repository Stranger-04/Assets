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

        var rgType = typeof(RenderGraph);

        // AddRenderPass overloads - full signature
        result += "=== AddRenderPass ===\n";
        foreach (var m in rgType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "AddRenderPass"))
        {
            result += $"  Returns: {m.ReturnType.FullName}\n";
            result += $"  Generic args: {string.Join(", ", m.GetGenericArguments().Select(t => t.Name))}\n";
            result += $"  Params: {string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}\n\n";
        }

        // RenderGraphBuilder SetRenderFunc - what type does it take?
        var rgbType = typeof(RenderGraphBuilder);
        result += "=== RenderGraphBuilder.SetRenderFunc ===\n";
        foreach (var m in rgbType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "SetRenderFunc"))
        {
            result += $"  {m.ReturnType.Name} {m.Name}(...)\n";
            foreach (var p in m.GetParameters())
            {
                result += $"    Param: {p.ParameterType.FullName} {p.Name}\n";
            }
        }

        // DepthAccess
        result += "\n=== DepthAccess enum ===\n";
        var daType = typeof(DepthAccess);
        foreach (var v in Enum.GetValues(daType))
            result += $"  {v}\n";

        return result;
    }
}
