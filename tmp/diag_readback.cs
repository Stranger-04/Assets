using Mine.Chosen;
using UnityEngine;
using UnityEngine.Rendering;

public class Script
{
    public static object Main()
    {
        var go = GameObject.Find("PickerReadback");
        var rb = go?.GetComponent<PickerReadback>();

        // 用反射获取私有字段
        var t = typeof(PickerReadback);
        var fRT  = t.GetField("m_ObjIDRT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fPass = t.GetField("m_OutlinePass", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fInit = t.GetField("m_Initialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fPend = t.GetField("m_ReadbackPending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var rt   = fRT?.GetValue(rb) as RenderTexture;
        var pass = fPass?.GetValue(rb) as OutlinePass;
        var init = (bool)(fInit?.GetValue(rb) ?? false);
        var pend = (bool)(fPend?.GetValue(rb) ?? false);

        return $"PickerReadback:\n"
             + $"  GO: {(go != null ? "found" : "MISSING")}\n"
             + $"  enabled: {(rb != null && rb.enabled)}\n"
             + $"  m_Initialized: {init}\n"
             + $"  m_ReadbackPending: {pend}\n"
             + $"  m_ObjIDRT: {(rt != null ? $"{rt.width}x{rt.height} {rt.format}" : "NULL")}\n"
             + $"  m_OutlinePass: {(pass != null ? "set" : "NULL")}\n"
             + $"\nStatic refs:\n"
             + $"  PickerFeature.RegisteredPass: {PickerFeature.RegisteredPass != null}\n"
             + $"  OutlineFeature.RegisteredPass: {OutlineFeature.RegisteredPass != null}\n"
             + $"\nMouse: {Input.mousePosition}\n"
             + $"RT bounds: {(rt != null ? $"{rt.width}x{rt.height}" : "N/A")}\n"
             + $"Mouse inside RT: {(rt != null && Input.mousePosition.x >= 0 && Input.mousePosition.x < rt.width && Input.mousePosition.y >= 0 && Input.mousePosition.y < rt.height)}\n";
    }
}
