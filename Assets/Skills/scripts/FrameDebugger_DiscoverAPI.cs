using UnityEditor;
using System.Reflection;
using System.Text;

public class FrameDebuggerDiscoverAPI
{
    public static object Main()
    {
        var sb = new StringBuilder();

        var fdu = typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility");
        if (fdu == null)
        {
            return "FrameDebuggerUtility not found";
        }

        sb.AppendLine("=== FrameDebuggerUtility ===\n");

        // Static methods
        foreach (var m in fdu.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var parms = string.Join(", ", System.Array.ConvertAll(m.GetParameters(),
                p => $"{p.ParameterType.Name} {p.Name}{(p.IsOptional ? " = " + (p.DefaultValue ?? "null") : "")}"));
            sb.AppendLine($"  [{(m.IsPublic ? "public" : "private")} static] {m.ReturnType.Name} {m.Name}({parms})");
        }

        // Instance methods
        foreach (var m in fdu.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (m.IsStatic) continue;
            var parms = string.Join(", ", System.Array.ConvertAll(m.GetParameters(),
                p => $"{p.ParameterType.Name} {p.Name}"));
            sb.AppendLine($"  [{(m.IsPublic ? "public" : "private")}] {m.ReturnType.Name} {m.Name}({parms})");
        }

        // Properties
        sb.AppendLine();
        foreach (var p in fdu.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
        {
            var get = p.CanRead ? "get;" : "";
            var set = p.CanWrite ? "set;" : "";
            sb.AppendLine($"  [{(p.GetMethod?.IsStatic ?? false ? "static " : "")}prop] {p.PropertyType.Name} {p.Name} {{ {get} {set} }}");
        }

        return sb.ToString();
    }
}
