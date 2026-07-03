using UnityEngine;
using UnityEngine.Rendering;

public class Script
{
    public static object Main()
    {
        PCSSFeature.s_DebugPass = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"s_DebugPass={PCSSFeature.s_DebugPass}");
        sb.AppendLine($"SCREEN keyword: {Shader.IsKeywordEnabled("_MAIN_LIGHT_SHADOWS_SCREEN")}");

        var tex = Shader.GetGlobalTexture("_PCSS_DEBUG_TEX") as RenderTexture;
        if (tex == null || !tex.IsCreated()) return sb.Append("_PCSS_DEBUG_TEX: NULL").ToString();

        var prev = RenderTexture.active;
        RenderTexture.active = tex;
        var t2d = new Texture2D(tex.width, tex.height, TextureFormat.R8, false);
        t2d.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        t2d.Apply();
        RenderTexture.active = prev;

        float ctr = t2d.GetPixel(tex.width/2, tex.height/2).r;
        float min=1, max=0;
        for (int y=0;y<8;y++) for (int x=0;x<8;x++)
        { float v = t2d.GetPixel(x*tex.width/8, y*tex.height/8).r; if (v<min) min=v; if (v>max) max=v; }

        string d = max<0.001f ? "ALL_BLACK(Blit failed)" : $"values[{min:F2},{max:F2}] ctr={ctr:F2}";
        sb.AppendLine($"_PCSS_DEBUG_TEX {tex.width}x{tex.height}: {d}");
        Object.DestroyImmediate(t2d);
        return sb.ToString();
    }
}
