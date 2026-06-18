using System;
using System.Linq;
using System.Reflection;
using UnityEngine.Rendering;

public class Script
{
    public static object Main()
    {
        var lines = "AsyncGPUReadback.Request overloads:\n";
        foreach (var m in typeof(AsyncGPUReadback).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "Request" && m.GetParameters().Length >= 7))
        {
            lines += $"  {m.GetParameters().Length} params: ";
            lines += string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            lines += "\n";
        }
        return lines;
    }
}
