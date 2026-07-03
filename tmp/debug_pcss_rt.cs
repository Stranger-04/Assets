using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

public class Script
{
    public static object Main()
    {
        // 1. 读取 _ScreenSpaceShadowmapTexture 全局纹理
        var shadowTex = Shader.GetGlobalTexture("_ScreenSpaceShadowmapTexture") as RenderTexture;
        if (shadowTex == null)
        {
            // 尝试其他可能的名称
            shadowTex = Shader.GetGlobalTexture("_PCSS_SoftShadow") as RenderTexture;
        }

        if (shadowTex == null)
            return "[PCSS Debug] _ScreenSpaceShadowmapTexture is NULL - not set by any pass";

        // 2. 纹理基本信息
        var info = $"[PCSS Debug] RT: {shadowTex.width}x{shadowTex.height} " +
                   $"format={shadowTex.graphicsFormat} depth={shadowTex.depth} " +
                   $"active={shadowTex.IsCreated()}";

        // 3. 读像素（同步，仅 Editor 可用）
        var prev = RenderTexture.active;
        RenderTexture.active = shadowTex;

        var tex = new Texture2D(shadowTex.width, shadowTex.height, TextureFormat.R8, false);
        tex.ReadPixels(new Rect(0, 0, shadowTex.width, shadowTex.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;

        // 采样几个关键像素
        int cx = shadowTex.width / 2;
        int cy = shadowTex.height / 2;
        float c = tex.GetPixel(cx, cy).r;
        float tl = tex.GetPixel(0, 0).r;
        float tr = tex.GetPixel(shadowTex.width - 1, 0).r;
        float bl = tex.GetPixel(0, shadowTex.height - 1).r;
        float br = tex.GetPixel(shadowTex.width - 1, shadowTex.height - 1).r;

        float min = 1f, max = 0f;
        // 快速扫描 10x10 格点
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
        {
            float v = tex.GetPixel(x * shadowTex.width / 10, y * shadowTex.height / 10).r;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        Object.DestroyImmediate(tex);

        string verdict;
        if (max < 0.001f) verdict = "*** ALL BLACK — Blitter NOT writing to RT ***";
        else if (Mathf.Abs(c - 0.5f) < 0.01f && Mathf.Abs(min - 0.5f) < 0.01f) verdict = "*** ALL 0.5 — Blitter OK, PCSS shader return 0.5 works ***";
        else verdict = $"*** HAS VALUES: min={min:F3} max={max:F3} center={c:F3} ***";

        return $"{info}\n" +
               $"  center=({cx},{cy})={c:F4}\n" +
               $"  corners: TL={tl:F4} TR={tr:F4} BL={bl:F4} BR={br:F4}\n" +
               $"  min={min:F4} max={max:F4}\n" +
               $"{verdict}";
    }
}
