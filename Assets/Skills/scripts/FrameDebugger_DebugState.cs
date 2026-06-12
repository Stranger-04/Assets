using UnityEditor;
using System.Reflection;
using System.Text;
using UnityEngine;

public class FrameDebuggerDebugState
{
    static object SafeCall(System.Reflection.MethodInfo m, object target, params object[] args)
    {
        try { return m.Invoke(target, args); }
        catch (System.Exception e) { return "ERROR: " + e.InnerException?.Message; }
    }

    public static object Main()
    {
        var sb = new StringBuilder();
        var editorAsm = typeof(EditorWindow).Assembly;
        var fduType = editorAsm.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility");
        var fdwType = editorAsm.GetType("UnityEditor.FrameDebuggerWindow");

        var windows = Resources.FindObjectsOfTypeAll(fdwType);
        if (windows.Length == 0) EditorWindow.GetWindow(fdwType);
        windows = Resources.FindObjectsOfTypeAll(fdwType);
        var window = windows[0] as EditorWindow;

        sb.AppendLine("Window found: " + (window != null));

        // Check basic state
        sb.AppendLine("FrameDebugger.enabled: " + FrameDebugger.enabled);
        sb.AppendLine("count: " + SafeCall(fduType.GetProperty("count").GetGetMethod(true), null));

        // Try calling methods one by one
        var disableMethod = fdwType.GetMethod("DisableFrameDebugger", BindingFlags.NonPublic | BindingFlags.Instance);
        var enableMethod = fdwType.GetMethod("EnableFrameDebugger", BindingFlags.NonPublic | BindingFlags.Instance);
        var toggleMethod = fdwType.GetMethod("ToggleFrameDebuggerEnabled", BindingFlags.NonPublic | BindingFlags.Instance);

        sb.AppendLine("\nCalling EnableFrameDebugger: " + SafeCall(enableMethod, window));

        // Wait and check
        System.Threading.Thread.Sleep(200);
        sb.AppendLine("After enable, FrameDebugger.enabled: " + FrameDebugger.enabled);
        sb.AppendLine("count: " + SafeCall(fduType.GetProperty("count").GetGetMethod(true), null));

        // Force the Game view to repaint
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        System.Threading.Thread.Sleep(500);
        sb.AppendLine("After repaint, FrameDebugger.enabled: " + FrameDebugger.enabled);
        sb.AppendLine("count: " + SafeCall(fduType.GetProperty("count").GetGetMethod(true), null));

        // Try toggle
        sb.AppendLine("\nCalling ToggleFrameDebuggerEnabled: " + SafeCall(toggleMethod, window));
        System.Threading.Thread.Sleep(200);
        sb.AppendLine("After toggle, FrameDebugger.enabled: " + FrameDebugger.enabled);
        sb.AppendLine("count: " + SafeCall(fduType.GetProperty("count").GetGetMethod(true), null));

        // Toggle back
        sb.AppendLine("\nCalling ToggleFrameDebuggerEnabled again: " + SafeCall(toggleMethod, window));
        System.Threading.Thread.Sleep(200);
        sb.AppendLine("After toggle back, FrameDebugger.enabled: " + FrameDebugger.enabled);
        sb.AppendLine("count: " + SafeCall(fduType.GetProperty("count").GetGetMethod(true), null));

        return sb.ToString();
    }
}
