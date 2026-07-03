using UnityEngine;
public class Script {
    public static object Main() {
        var t = System.Type.GetType("UnityEditorInternal.FrameDebuggerUtility, UnityEditor");
        var disable = t.GetMethod("SetEnabled", new[] { typeof(bool) });
        disable.Invoke(null, new object[] { false });
        disable.Invoke(null, new object[] { true });
        return "FrameDebugger toggled OFF→ON";
    }
}
