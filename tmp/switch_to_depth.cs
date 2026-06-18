using System.Linq;
using System.Reflection;
using Mine.Chosen;
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
        if (pf == null) return "no feature";

        var field = typeof(PickerFeature).GetField("m_DebugView",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(pf, PickerPass.DebugView.Depth);
        EditorUtility.SetDirty(rd);
        AssetDatabase.SaveAssets();
        return "DebugView = Depth — clear grayscale depth map";
    }
}
