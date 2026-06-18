using System.Linq;
using Mine.Chosen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        // 确认 PickerFeature 已正确配置
        var rendererPath = "Assets/Settings/PC_Renderer.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null) return "ERROR: PC_Renderer.asset not found";

        var lines = new System.Collections.Generic.List<string>();

        PickerFeature pf = null;
        foreach (var f in rendererData.rendererFeatures)
        {
            if (f is PickerFeature p)
            {
                pf = p;
                // 通过反射读取 debugView
                var field = typeof(PickerFeature).GetField("m_DebugView",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var dv = field?.GetValue(pf)?.ToString() ?? "?";
                lines.Add($"PickerFeature found: debugView={dv}");
            }
            else if (f != null)
            {
                lines.Add($"  - {f.GetType().Name}");
            }
        }

        if (pf == null)
        {
            lines.Add("WARNING: PickerFeature NOT found in renderer!");
        }

        // 检查 Picker Shader 是否存在
        var shader = Shader.Find("Mine/Chosen/Picker");
        lines.Add($"Picker shader found: {shader != null}");

        // 检查是否有 Camera 标记为 MainCamera
        var cam = Camera.main;
        lines.Add($"Main camera: {(cam != null ? cam.name : "NONE")}");

        // 检查场景中 Opaque 物体数量
        var renderers = Object.FindObjectsOfType<Renderer>();
        var opaqueCount = renderers.Count(r => r.sharedMaterial != null &&
            r.sharedMaterial.renderQueue <= 2500);
        lines.Add($"Scene renderers: {renderers.Length} total, ~{opaqueCount} opaque");

        return string.Join("\n", lines);
    }
}
