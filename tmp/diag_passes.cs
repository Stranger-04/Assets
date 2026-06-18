using System.Linq;
using Mine.Chosen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var lines = "";

        // 1. 检查测试Cube是否存在
        var parent = GameObject.Find("PickerTest");
        var cubes = parent != null ? parent.GetComponentsInChildren<Renderer>() : new Renderer[0];
        lines += $"PickerTest GO: {(parent != null ? "found" : "MISSING")}\n";
        lines += $"Cubes with PickerMRT pass: {cubes.Length}\n";
        foreach (var c in cubes)
        {
            var mat = c.sharedMaterial;
            var hasProp = mat != null && mat.HasProperty("_ObjectID");
            var id = hasProp ? mat.GetInt("_ObjectID") : -1;
            var passes = mat != null ? mat.passCount : 0;
            lines += $"  - {c.name}: ObjectID={id}, passes={passes}, shader={mat?.shader?.name}\n";
        }

        // 2. 检查PickerPass的渲染条件
        var pickerPass = PickerFeature.RegisteredPass;
        if (pickerPass != null)
        {
            lines += $"\nPickerPass debugView: {pickerPass.debugView}\n";
        }

        // 3. 检查OutlinePass
        var outlinePass = OutlineFeature.RegisteredPass;
        if (outlinePass != null)
        {
            lines += $"OutlinePass selectedID: {outlinePass.selectedObjectID}\n";
            lines += $"OutlinePass debugShowMask: {outlinePass.debugShowMask}\n";
        }

        // 4. Shader是否存在
        lines += $"\nPicker shader: {Shader.Find("Mine/Chosen/Picker") != null}\n";
        lines += $"OutlineMask shader: {Shader.Find("Mine/Chosen/OutlineMask") != null}\n";
        lines += $"OutlineComposite shader: {Shader.Find("Mine/Chosen/OutlineComposite") != null}\n";

        return lines;
    }
}
