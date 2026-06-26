using UnityEngine;

public class Script
{
    public static object Main()
    {
        var go = GameObject.Find("Plane (1)");
        if (go == null) return "ERROR: Plane (1) not found";

        var manager = go.GetComponent<Mine.Water.WaveFoamManager>();
        if (manager == null) return "ERROR: WaveFoamManager not found";

        // Check if the global texture is set
        var mat = go.GetComponent<Renderer>()?.sharedMaterial;
        if (mat == null) return "ERROR: No material on Plane (1)";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Material shader: {mat.shader.name}");
        sb.AppendLine($"_WaveFoamTex global: {Shader.GetGlobalTexture("_WaveFoamTex")?.name ?? "NULL"}");
        sb.AppendLine($"_WaveFoamWorldTexSize global: {Shader.GetGlobalFloat("_WaveFoamWorldTexSize")}");

        // Check material's FoamScale etc.
        sb.AppendLine($"_FoamScale: {mat.GetFloat("_FoamScale")}");
        sb.AppendLine($"_FoamSpeed: {mat.GetFloat("_FoamSpeed")}");
        sb.AppendLine($"_FoamIntensity: {mat.GetFloat("_FoamIntensity")}");

        return sb.ToString();
    }
}
