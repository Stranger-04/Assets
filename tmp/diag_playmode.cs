using System.Linq;
using Mine.Chosen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return "Pipeline null";

        var lines = "rendererDataList length: " + pipeline.rendererDataList.Length + "\n";

        for (int i = 0; i < pipeline.rendererDataList.Length; i++)
        {
            var rd = pipeline.rendererDataList[i];
            lines += $"\nRenderer[{i}]: {rd.name}\n";
            lines += $"  Features: {rd.rendererFeatures.Count}\n";
            foreach (var f in rd.rendererFeatures)
            {
                if (ReferenceEquals(f, null))
                    lines += "  - (real null)\n";
                else if (f == null)
                    lines += "  - (Unity null / missing script)\n";
                else
                    lines += $"  - {f.GetType().Name}\n";
            }
        }

        // Check static registrations
        lines += $"\nStatic PickerFeature.RegisteredPass: {PickerFeature.RegisteredPass}\n";
        lines += $"Static OutlineFeature.RegisteredPass: {OutlineFeature.RegisteredPass}\n";

        return lines;
    }
}
