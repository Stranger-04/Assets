using Mine.Chosen;
using UnityEngine;

public class Script
{
    public static object Main()
    {
        var cam = Camera.main;
        var go = GameObject.Find("PickerReadback");
        var rb = go?.GetComponent<PickerReadback>();

        return $"Camera: {cam?.name}, {cam?.pixelWidth}x{cam?.pixelHeight}\n"
             + $"Readback GO: {(go != null ? "found" : "MISSING")}\n"
             + $"Readback comp: {(rb != null ? "found" : "MISSING")}\n"
             + $"Static PickerPass: {PickerFeature.RegisteredPass != null}\n"
             + $"Static OutlinePass: {OutlineFeature.RegisteredPass != null}\n"
             + $"ObjID RT in pass: {PickerFeature.RegisteredPass?.ObjIDRenderTexture != null}\n"
             + $"ObjID Handle valid: {PickerFeature.RegisteredPass?.ObjIDHandle.IsValid()}\n";
    }
}
