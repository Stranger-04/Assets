using UnityEngine;
using UnityEditor;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();

        // Enable Frame Debugger via menu
        var fdType = System.Type.GetType("UnityEditorInternal.FrameDebuggerUtility, UnityEditor");
        if (fdType == null) { return "FrameDebuggerUtility type not found"; }

        var isLocalEnabled = fdType.GetMethod("IsLocalEnabled");
        var setEnabled = fdType.GetMethod("SetEnabled", new[] { typeof(bool), typeof(int) });

        bool wasEnabled = (bool)isLocalEnabled.Invoke(null, null);

        if (!wasEnabled)
        {
            setEnabled.Invoke(null, new object[] { true, 0 });
            sb.AppendLine($"Frame Debugger: {wasEnabled} → enabled");
            sb.AppendLine("Need to render a frame first. Run again after a frame renders.");
            return sb.ToString();
        }

        // Get frame events
        var getEvents = fdType.GetMethod("GetFrameEvents");
        var events = getEvents.Invoke(null, null) as System.Array;
        int total = events?.Length ?? 0;
        sb.AppendLine($"Frame Debugger: ENABLED, {total} draw calls");

        // Search for "PCSS" in shader names
        int pcssMatches = 0;
        for (int i = 0; i < total && i < 300; i++)
        {
            var ev = events.GetValue(i);
            var shaderNameProp = ev.GetType().GetProperty("shaderName");
            var passNameProp = ev.GetType().GetProperty("shaderPassName");
            string sName = shaderNameProp?.GetValue(ev) as string ?? "";
            string pName = passNameProp?.GetValue(ev) as string ?? "";
            if (sName.Contains("PCSS") || sName.Contains("ScreenSpace"))
            {
                pcssMatches++;
                var goProp = ev.GetType().GetProperty("gameObjectName");
                string goName = goProp?.GetValue(ev) as string ?? "";
                sb.AppendLine($"  [{i}] {goName,-25} shader={sName,-40} pass={pName}");
            }
        }

        sb.AppendLine($"\nPCSS-related draw calls found: {pcssMatches}");

        // Search for our sampling profiler name
        sb.AppendLine("\nSearching for 'PCSS' in events...");
        for (int i = 0; i < total && i < 300; i++)
        {
            var ev = events.GetValue(i);
            var goProp = ev.GetType().GetProperty("gameObjectName");
            string goName = goProp?.GetValue(ev) as string ?? "";
            var passProp = ev.GetType().GetProperty("shaderPassName");
            string pName = passProp?.GetValue(ev) as string ?? "";
            if (goName.Contains("PCSS") || pName.Contains("PCSS"))
            {
                sb.AppendLine($"  [{i}] go={goName} pass={pName}");
                pcssMatches++;
            }
        }

        return sb.ToString();
    }
}
