using Mine.Chosen;
using UnityEngine;

public class Script
{
    public static object Main()
    {
        var go = GameObject.Find("PickerReadback");
        if (go == null) return "PickerReadback GO NOT FOUND in scene!";

        var rb = go.GetComponent<PickerReadback>();
        if (rb == null) return "PickerReadback component NOT FOUND on GO!";

        return $"PickerReadback: GO={go.name}, enabled={rb.enabled}, isActiveAndEnabled={rb.isActiveAndEnabled}\n"
             + $"Static PickerPass: {PickerFeature.RegisteredPass != null}\n"
             + $"Static OutlinePass: {OutlineFeature.RegisteredPass != null}";
    }
}
