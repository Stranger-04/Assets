using UnityEngine;
using UnityEngine.Rendering;

public class Script {
    public static object Main() {
        var s = "=== Keywords ===\n";
        s += $"SCREEN:  {Shader.IsKeywordEnabled(new GlobalKeyword("_MAIN_LIGHT_SHADOWS_SCREEN"))}\n";
        s += $"CASCADE: {Shader.IsKeywordEnabled(new GlobalKeyword("_MAIN_LIGHT_SHADOWS_CASCADE"))}\n";
        s += $"SHADOWS: {Shader.IsKeywordEnabled(new GlobalKeyword("_MAIN_LIGHT_SHADOWS"))}\n";

        var t = Shader.GetGlobalTexture("_ScreenSpaceShadowmapTexture");
        s += $"\n_ScreenSpaceShadowmapTexture: {(t!=null?$"{t.width}x{t.height}":"NULL")}\n";
        s += $"s_DebugPass: {PCSSFeature.s_DebugPass}\n";
        return s;
    }
}
