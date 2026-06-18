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
        // 1. 关闭 Debug 视图（让场景正常渲染）
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var rendererData = pipeline.rendererDataList[0];
        var pickerFeature = rendererData.rendererFeatures
            .OfType<PickerFeature>()
            .FirstOrDefault();

        if (pickerFeature != null)
        {
            var field = typeof(PickerFeature).GetField("m_DebugView",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(pickerFeature, PickerPass.DebugView.Off);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }

        // 2. 创建 PickerReadback GameObject
        var cam = Camera.main;
        if (cam == null) return "ERROR: No main camera";

        var go = GameObject.Find("PickerReadback");
        if (go != null) Object.DestroyImmediate(go);

        go = new GameObject("PickerReadback");
        var readback = go.AddComponent<PickerReadback>();

        return "Setup complete:\n"
             + "  - DebugView: OFF (normal scene rendering)\n"
             + "  - PickerReadback GameObject created\n"
             + "  - 4 colored cubes should be visible\n"
             + "  - Click a cube to see its ObjectID in Console";
    }
}
