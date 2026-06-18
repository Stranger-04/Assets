using System.IO;
using UnityEngine;

public class Script
{
    public static object Main()
    {
        var cam = Camera.main;
        if (cam == null) return "No main camera";

        // 读取当前 camera target 的像素
        var rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        var prevRT = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prevRT;

        // 保存
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        var bytes = tex.EncodeToPNG();
        var path = Path.Combine(Application.dataPath, "../Screenshots/manual_capture.png");
        File.WriteAllBytes(path, bytes);

        Object.Destroy(tex);
        Object.Destroy(rt);

        return $"Manual capture saved: {path} ({rt.width}x{rt.height})";
    }
}
