using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== RT Bindings ===\n");

        string[] names = { "_ScreenSpaceShadowmapTexture", "_PCSS_SoftShadowRT",
            "_ScreenSpaceOcclusionTexture", "_SSAO_OcclusionTexture" };
        foreach (var n in names)
        {
            var t = Shader.GetGlobalTexture(n);
            if (t == null) sb.AppendLine($"{n}: NULL");
            else sb.AppendLine($"{n}: {t.width}x{t.height} [{t.graphicsFormat}] name='{t.name}'");
        }

        // Check feature RT
        var rd = Object.FindFirstObjectByType(typeof(ScriptableRendererData)) as ScriptableRendererData;
        if (rd != null)
        {
            var ff = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            var features = ff.GetValue(rd) as System.Collections.IList;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i]?.GetType().Name == "PCSSFeature")
                {
                    var f = features[i];
                    var pf = f.GetType().GetField("m_PCSSPass", BindingFlags.NonPublic | BindingFlags.Instance);
                    var pass = pf.GetValue(f);
                    var rtf = pass.GetType().GetField("m_RT", BindingFlags.NonPublic | BindingFlags.Instance);
                    var rt = rtf?.GetValue(pass) as RTHandle;
                    if (rt?.rt != null)
                        sb.AppendLine($"\nPCSS internal RT: {rt.rt.width}x{rt.rt.height} [{rt.rt.graphicsFormat}] name='{rt.name}'");
                    else sb.AppendLine("\nPCSS internal RT: NULL");
                }
            }
        }

        sb.AppendLine($"\ns_DebugPass: {PCSSFeature.s_DebugPass}");
        return sb.ToString();
    }
}
