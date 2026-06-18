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

        // Check all builder interfaces
        var builderTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => {
                try { return a.GetTypes(); } catch { return new Type[0]; }
            })
            .Where(t => (t.Name.Contains("RenderGraphBuilder") || t.Name.Contains("UnsafeRender") || t.Name == "BaseRenderGraphBuilder") && t.IsPublic)
            .ToList();

        foreach (var bt in builderTypes)
        {
            result += $"\n=== {bt.FullName} (interface={bt.IsInterface})===\n";
            foreach (var m in bt.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName))
            {
                result += $"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})\n";
            }
        }

        // Check Methods that concrete RenderGraph returns for pass builders
        var rgType = typeof(RenderGraph);
        result += "\n=== All RenderGraph methods with 'Unsafe' or 'Raster' in name ===\n";
        foreach (var m in rgType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Unsafe") || m.Name.Contains("Raster") || m.Name.Contains("RenderPass")))
        {
            result += $"  {m.ReturnType.Name} {m.Name}(...)\n";
        }

        // Check RendererListDesc constructors
        result += "\n=== RendererListDesc constructors ===\n";
        var rldType = Type.GetType("UnityEngine.Rendering.RendererUtils.RendererListDesc, UnityEngine.CoreModule");
        if (rldType != null)
        {
            foreach (var ctor in rldType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                result += $"  ctor({string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})\n";
            }
        }

        return result;
    }
}
