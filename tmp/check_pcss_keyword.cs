using UnityEngine;
using UnityEngine.Rendering;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();

        // 检查全局关键字
        var kwScreen = new GlobalKeyword("_MAIN_LIGHT_SHADOWS_SCREEN");
        var kwCascade = new GlobalKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
        var kwShadow = new GlobalKeyword("_MAIN_LIGHT_SHADOWS");

        bool screenOn = Shader.IsKeywordEnabled(kwScreen);
        bool cascadeOn = Shader.IsKeywordEnabled(kwCascade);
        bool shadowOn = Shader.IsKeywordEnabled(kwShadow);

        sb.AppendLine("=== Global Keywords (Editor) ===");
        sb.AppendLine($"_MAIN_LIGHT_SHADOWS_SCREEN:  {screenOn}");
        sb.AppendLine($"_MAIN_LIGHT_SHADOWS_CASCADE: {cascadeOn}");
        sb.AppendLine($"_MAIN_LIGHT_SHADOWS:         {shadowOn}");

        // 检查 _ScreenSpaceShadowmapTexture
        var ssst = Shader.GetGlobalTexture("_ScreenSpaceShadowmapTexture");
        sb.AppendLine($"\n_ScreenSpaceShadowmapTexture: {(ssst != null ? $"{ssst.width}x{ssst.height} {ssst.graphicsFormat}" : "NULL")}");

        // 检查 _MainLightShadowmapTexture
        var mlst = Shader.GetGlobalTexture("_MainLightShadowmapTexture");
        sb.AppendLine($"_MainLightShadowmapTexture: {(mlst != null ? $"{mlst.width}x{mlst.height} {mlst.graphicsFormat}" : "NULL")}");

        // PCSSFeature state
        sb.AppendLine($"\ns_DebugPass: {PCSSFeature.s_DebugPass}");

        return sb.ToString();
    }
}
