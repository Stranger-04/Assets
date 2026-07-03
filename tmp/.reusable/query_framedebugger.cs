using UnityEngine;
using UnityEditor;

/// <summary>Query Frame Debugger — list draw calls, shaders, and render states from the last captured frame.</summary>
public class Script
{
    static System.Type FDU;
    static System.Type FDData;

    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Frame Debugger Query ===\n");

        FDU = System.Type.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility, UnityEditor.CoreModule");
        FDData = System.Type.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData, UnityEditor.CoreModule");

        if (FDU == null) return "Error: FrameDebuggerUtility type not found.";

        // Enable Frame Debugger (idempotent)
        FDU.GetMethod("SetEnabled", new[] { typeof(bool), typeof(int) }).Invoke(null, new object[] { true, 0 });

        // Set a high event limit
        var limitProp = FDU.GetProperty("limit");
        if (limitProp != null) limitProp.SetValue(null, 500);

        int count = (int)FDU.GetProperty("count").GetValue(null);
        sb.AppendLine($"Status: ENABLED | Draw Calls: {count}\n");

        if (count == 0)
        {
            sb.AppendLine("No draw call data captured yet.");
            sb.AppendLine("→ Render a frame (move Scene/Game view), then re-run this script.");
            return sb.ToString();
        }

        var getEventData = FDU.GetMethod("GetFrameEventData", new[] { typeof(int), FDData });
        var getEventObj = FDU.GetMethod("GetFrameEventObject");
        var getEventName = FDU.GetMethod("GetFrameEventInfoName");

        // ---- Collect data ----
        var items = new System.Collections.Generic.List<FdItem>();
        var shaderCounts = new System.Collections.Generic.Dictionary<string, (int calls, int verts)>();

        for (int i = 0; i < count && i < 300; i++)
        {
            var data = System.Activator.CreateInstance(FDData);
            getEventData.Invoke(null, new[] { (object)i, data });
            var go = getEventObj.Invoke(null, new object[] { i }) as GameObject;

            var item = new FdItem
            {
                index = i,
                goName = go != null ? go.name : "-",
                shaderName = FStr(data, "m_RealShaderName") ?? FStr(data, "m_OriginalShaderName"),
                passName = FStr(data, "m_PassName"),
                passLightMode = FStr(data, "m_PassLightMode"),
                vertexCount = FInt(data, "m_VertexCount"),
                keywords = FStr(data, "shaderKeywords"),
            };

            items.Add(item);

            string key = item.shaderName ?? "(Unknown)";
            if (shaderCounts.ContainsKey(key))
            {
                var v = shaderCounts[key];
                shaderCounts[key] = (v.calls + 1, v.verts + item.vertexCount);
            }
            else
                shaderCounts[key] = (1, item.vertexCount);
        }

        // ---- Shader summary ----
        sb.AppendLine("Shader Summary:");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"{"Shader",-35} {"Calls",-8} {"Vertices",-12}");
        sb.AppendLine(new string('-', 60));
        foreach (var kv in shaderCounts)
            sb.AppendLine($"{T(kv.Key, 35),-35} {kv.Value.calls,-8} {kv.Value.verts,-12}");

        // ---- Draw call list ----
        sb.AppendLine($"\nDraw Call List:");
        sb.AppendLine(new string('-', 112));
        sb.AppendLine($"{"#",-5} {"GameObject",-28} {"Shader",-24} {"Pass",-18} {"LightMode",-14} {"Verts",-8} {"Keywords",0}");
        sb.AppendLine(new string('-', 112));

        foreach (var item in items)
        {
            sb.AppendLine($"{item.index,-5} {T(item.goName, 28),-28} {T(S(item.shaderName), 24),-24} {T(item.passName, 18),-18} {T(item.passLightMode, 14),-14} {item.vertexCount,-8} {T(item.keywords, 999)}");
        }

        // ---- Render target info for first event ----
        if (count > 0)
        {
            var data = System.Activator.CreateInstance(FDData);
            getEventData.Invoke(null, new[] { 0, data });
            sb.AppendLine($"\nRender Target (event 0): {FStr(data, "m_RenderTargetName")}");
            sb.AppendLine($"  Size: {FInt(data, "m_RenderTargetWidth")}x{FInt(data, "m_RenderTargetHeight")}  BackBuffer: {FBool(data, "m_RenderTargetIsBackBuffer")}");
        }

        return sb.ToString();
    }

    class FdItem
    {
        public int index;
        public string goName, shaderName, passName, passLightMode, keywords;
        public int vertexCount;
    }

    static string FStr(object obj, string field) => obj?.GetType().GetField(field)?.GetValue(obj) as string;
    static int FInt(object obj, string field) => (int)(obj?.GetType().GetField(field)?.GetValue(obj) ?? 0);
    static bool FBool(object obj, string field) => (bool)(obj?.GetType().GetField(field)?.GetValue(obj) ?? false);

    static string T(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "-";
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
    static string S(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "-";
        int idx = fullName.LastIndexOf('/');
        return idx >= 0 ? fullName.Substring(idx + 1) : fullName;
    }
}
