using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Reflection;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== PCSS Pass Verification ===\n");

        var rendererData = AssetDatabase.LoadAssetAtPath(
            "Assets/Settings/PC_Renderer.asset", typeof(ScriptableRendererData)) as ScriptableRendererData;

        var ff = typeof(ScriptableRendererData).GetField("m_RendererFeatures",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var features = ff.GetValue(rendererData) as System.Collections.IList;

        for (int i = 0; i < features.Count; i++)
        {
            var f = features[i];
            if (f?.GetType().Name != "PCSSFeature") continue;

            // settings
            var sf = f.GetType().GetField("settings", BindingFlags.Public | BindingFlags.Instance);
            var s = sf.GetValue(f);
            var shf = s.GetType().GetField("pcssShader", BindingFlags.Public | BindingFlags.Instance);
            sb.AppendLine($"pcssShader: {((Shader)shf.GetValue(s))?.name ?? "NULL"}");

            // Feature.m_Material
            var mf = f.GetType().GetField("m_Material", BindingFlags.NonPublic | BindingFlags.Instance);
            var mat = mf.GetValue(f) as Material;
            sb.AppendLine($"Feature.m_Material: {(mat != null ? $"{mat.shader.name} valid={mat.shader.isSupported}" : "NULL")}");

            // PCSSPass
            var pf = f.GetType().GetField("m_PCSSPass", BindingFlags.NonPublic | BindingFlags.Instance);
            var pass = pf.GetValue(f);

            // pass.material (INTERNAL field!)
            var pmf = pass.GetType().GetField("material", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var pmat = pmf?.GetValue(pass) as Material;
            sb.AppendLine($"pass.material: {(pmat != null ? $"{pmat.shader.name} (valid={pmat.shader.isSupported})" : "NULL")}");

            // pass renderPassEvent
            var rpef = typeof(ScriptableRenderPass).GetField("renderPassEvent",
                BindingFlags.Public | BindingFlags.Instance);
            sb.AppendLine($"renderPassEvent: {rpef?.GetValue(pass)}");

            // RT
            var rtf = pass.GetType().GetField("m_RT", BindingFlags.NonPublic | BindingFlags.Instance);
            var rt = rtf?.GetValue(pass) as RTHandle;
            if (rt?.rt != null)
            {
                sb.AppendLine($"RT: {rt.rt.width}x{rt.rt.height} fmt={rt.rt.format} rw={rt.rt.enableRandomWrite} isCreated={rt.rt.IsCreated()}");
            }
            else sb.AppendLine("RT: NULL");

            // PostPass
            var ppf = f.GetType().GetField("m_PCSSPostPass", BindingFlags.NonPublic | BindingFlags.Instance);
            var pp = ppf?.GetValue(f);
            var pprpe = typeof(ScriptableRenderPass).GetField("renderPassEvent",
                BindingFlags.Public | BindingFlags.Instance);
            sb.AppendLine($"PostPass.renderPassEvent: {pprpe?.GetValue(pp)}");

            // Material debug: check if Frag_PCSS is reachable
            if (pmat != null)
            {
                sb.AppendLine($"\nMaterial passes: {pmat.passCount}");
                sb.AppendLine($"Material keywords enabled: {string.Join(", ", pmat.enabledKeywords ?? System.Array.Empty<LocalKeyword>())}");
            }
        }

        sb.AppendLine($"\ns_DebugPass: {PCSSFeature.s_DebugPass}");
        return sb.ToString();
    }
}
