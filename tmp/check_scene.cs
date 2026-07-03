using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var rd = pipeline?.scriptableRenderer;
        var rdData = pipeline != null ? pipeline.scriptableRendererData : null;

        sb.AppendLine($"Renderer type: {rd?.GetType().Name ?? "null"}");
        sb.AppendLine($"RendererData type: {rdData?.GetType().Name ?? "null"}");
        sb.AppendLine($"RendererData name: {rdData?.name ?? "null"}");

        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var go in roots)
            DescribeObject(sb, go, 0);

        var sun = RenderSettings.sun;
        if (sun != null)
            sb.AppendLine($"\nMainLight: {sun.name} shadows={sun.shadows} strength={sun.shadowStrength}");

        return sb.ToString();
    }

    static void DescribeObject(System.Text.StringBuilder sb, GameObject go, int depth)
    {
        var pfx = new string(' ', depth * 2);
        var r = go.GetComponent<Renderer>();
        if (r != null) {
            var m = r.sharedMaterial;
            sb.AppendLine($"{pfx}{go.name} [Rend={r.GetType().Name}] [Mat={m?.name}] [Shader={m?.shader?.name}] [shadowCastingMode={r.shadowCastingMode}]");
        } else {
            var names = new List<string>();
            foreach (var c in go.GetComponents<Component>())
                if (c != null && c is not Transform) names.Add(c.GetType().Name);
            if (names.Count > 0) sb.AppendLine($"{pfx}{go.name} [{string.Join(", ", names)}]");
        }
        foreach (Transform c in go.transform)
            DescribeObject(sb, c.gameObject, depth + 1);
    }
}
