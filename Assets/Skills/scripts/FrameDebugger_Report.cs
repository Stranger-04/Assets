/// <summary>
/// FrameDebugger_Report - 通过 unityctl script execute 运行
/// 用法: unityctl script execute Assets/Skills/scripts/FrameDebugger_Report.cs
/// 前提: 需要先打开 Frame Debugger 窗口 (Window > Analysis > Frame Debugger)
/// </summary>
using UnityEditor;
using System.Reflection;
using System.Text;
using UnityEngine;

public class FrameDebuggerReport
{
    public static object Main()
    {
        var sb = new StringBuilder();
        var editorAsm = typeof(EditorWindow).Assembly;
        var fduType = editorAsm.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility");
        var fdwType = editorAsm.GetType("UnityEditor.FrameDebuggerWindow");

        // Get window
        var windows = Resources.FindObjectsOfTypeAll(fdwType);
        if (windows.Length == 0) EditorWindow.GetWindow(fdwType);
        windows = Resources.FindObjectsOfTypeAll(fdwType);
        var window = windows[0] as EditorWindow;

        var enableMethod = fdwType.GetMethod("EnableFrameDebugger", BindingFlags.NonPublic | BindingFlags.Instance);

        // If not enabled, enable it
        if (!FrameDebugger.enabled)
        {
            enableMethod.Invoke(window, null);
            System.Threading.Thread.Sleep(200);
        }

        int count = (int)fduType.GetProperty("count").GetValue(null);

        // If count is 0, we need a frame to be rendered
        if (count == 0)
        {
            // Force render a frame
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
            System.Threading.Thread.Sleep(500);

            count = (int)fduType.GetProperty("count").GetValue(null);
        }

        sb.AppendLine($"=== Frame Debugger 渲染管线 ({count} events) ===\n");

        if (count == 0)
        {
            sb.AppendLine("No events. Frame Debugger window may need manual 'Enable' click.");
            sb.AppendLine("Please click 'Enable' in Window > Analysis > Frame Debugger, then re-run.");
            return sb.ToString();
        }

        // Read all events
        int limit = Mathf.Min(count, 80);
        for (int i = 0; i < limit; i++)
        {
            var getInfoNameMethod = fduType.GetMethod("GetFrameEventInfoName");
            string name = (string)getInfoNameMethod.Invoke(null, new object[] { i });
            sb.AppendFormat("{0,3}. {1}\n", i, name);
        }

        if (count > limit)
            sb.AppendLine($"\n... 省略 {count - limit} 个事件");

        // Summary
        int drawCalls = 0;
        for (int i = 0; i < count; i++)
        {
            var getInfoNameMethod = fduType.GetMethod("GetFrameEventInfoName");
            string name = (string)getInfoNameMethod.Invoke(null, new object[] { i });
            if (name.Contains("Draw")) drawCalls++;
        }

        sb.AppendLine($"\n总事件: {count}  |  Draw Calls: {drawCalls}");

        return sb.ToString();
    }
}
