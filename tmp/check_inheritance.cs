using System;
using System.Linq;
using System.Reflection;
using UnityEngine.Rendering.RenderGraphModule;

public class Script
{
    public static object Main()
    {
        var result = "";

        // Check IRasterRenderGraphBuilder inheritance
        var irb = typeof(IRasterRenderGraphBuilder);
        result += $"=== {irb.Name} ===\n";
        result += $"Interfaces:\n";
        foreach (var i in irb.GetInterfaces())
            result += $"  {i.Name}\n";

        result += $"\nAll methods:\n";
        foreach (var m in irb.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            result += $"  {m.Name}\n";

        // Check IUnsafeRenderGraphBuilder
        var iub = typeof(IUnsafeRenderGraphBuilder);
        result += $"\n=== {iub.Name} ===\n";
        result += $"Interfaces:\n";
        foreach (var i in iub.GetInterfaces())
            result += $"  {i.Name}\n";

        result += $"\nAll methods:\n";
        foreach (var m in iub.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            result += $"  {m.Name}\n";

        return result;
    }
}
