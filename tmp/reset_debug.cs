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

        // PickerFeature → DebugView = Off
        var pf = rd.rendererFeatures.OfType<PickerFeature>().FirstOrDefault();
        if (pf != null)
        {
            typeof(PickerFeature).GetField("m_DebugView",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(pf, PickerPass.DebugView.Off);
        }

        // OutlineFeature → m_DebugShowMask = false
        var of = rd.rendererFeatures.OfType<OutlineFeature>().FirstOrDefault();
        if (of != null)
        {
            typeof(OutlineFeature).GetField("m_DebugShowMask",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(of, false);
        }

        EditorUtility.SetDirty(rd);
        AssetDatabase.SaveAssets();
        return "Debug views reset: Picker=Off, Outline debugShowMask=False";
    }
}
