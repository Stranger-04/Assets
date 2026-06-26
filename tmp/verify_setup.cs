using System.Linq;
using Mine.Picker;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var rd = pipeline.rendererDataList[0];
        var pf = rd.rendererFeatures.OfType<PickerFeature>().FirstOrDefault();

        var parent = GameObject.Find("PickerTest");
        var count = parent != null ? parent.GetComponentsInChildren<Renderer>().Length : 0;
        var rb = GameObject.Find("PickerReadback")?.GetComponent<PickerReadback>();

        return $"PickerFeature in renderer: {(pf != null ? "YES" : "NO - needs adding!")}\n"
             + $"PickerTest children: {count}\n"
             + $"PickerReadback: {(rb != null ? "YES" : "NO")}";
    }
}
