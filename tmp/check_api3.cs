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

        // IRasterRenderGraphBuilder
        var builderTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => {
                try { return a.GetTypes(); } catch { return new Type[0]; }
            })
            .Where(t => t.Name.Contains("RasterRender") && t.IsPublic)
            .ToList();

        foreach (var bt in builderTypes)
        {
            result += $"\n=== {bt.FullName} ===\n";
            result += $"IsInterface: {bt.IsInterface}\n";
            foreach (var m in bt.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Contains("SetRender") || m.Name.Contains("UseRenderer") || m.Name.Contains("AllowPass")))
            {
                result += $"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})\n";
            }
        }

        // Also check what ScriptableRenderPass.profilingSampler is
        result += "\n=== ScriptableRenderPass fields/properties ===\n";
        var srpType = typeof(UnityEngine.Rendering.Universal.ScriptableRenderPass);
        foreach (var m in srpType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name.Contains("rofil")))
        {
            result += $"  {m.MemberType} {m.Name}: {((m as FieldInfo)?.FieldType.Name ?? (m as PropertyInfo)?.PropertyType.Name ?? "?")}\n";
        }

        // Check AddUnsafePass
        result += "\n=== RenderGraph.AddUnsafePass overloads ===\n";
        var rgType = typeof(RenderGraph);
        foreach (var m in rgType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "AddUnsafePass"))
        {
            result += $"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})\n";
        }

        return result;
    }
}
