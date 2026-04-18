#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class NoiseGeneratorWindow : EditorWindow
{
    private enum OutputMode
    {
        Texture3D,
        Texture2DSlices
    }

    private int size = 32;
    private float scale = 4f;
    private bool seamless = true;
    private NoiseGenerator.NoiseType noiseType = NoiseGenerator.NoiseType.Perlin;

    private OutputMode outputMode = OutputMode.Texture3D;
    private int sliceResolution = 32;
    private int sliceChannels = 4;
    private float sliceStart = 0f;
    private float sliceDistance = 0.1f;

    private string outputPath3D = "Assets/Textures/Noise3D.asset";
    private string outputPath2D = "Assets/Textures/Noise2D.asset";

    private Texture3D noiseTexture3D;
    private Texture2D noiseTexture2D;

    private int previewAxis = 2; // 0:X,1:Y,2:Z
    private int previewSlice = 0;
    private Texture2D previewSlice2D;

    private int preview2DChannel = 0; // 0:R, 1:G, 2:B, 3:A, -1:RGBA composite

    [MenuItem("Tools/Noise Generator...")]
    public static void ShowWindow()
    {
        var window = GetWindow<NoiseGeneratorWindow>(false, "Noise Generator", true);
        window.minSize = new Vector2(360, 340);
        window.Show();
    }

    private void OnDisable()
    {
        if (previewSlice2D != null)
        {
            DestroyImmediate(previewSlice2D);
            previewSlice2D = null;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Noise Settings", EditorStyles.boldLabel);
        size = EditorGUILayout.IntSlider("Size", Mathf.Clamp(size, 4, 256), 4, 256);
        scale = EditorGUILayout.FloatField("Scale", Mathf.Max(0.0001f, scale));
        seamless = EditorGUILayout.Toggle("Seamless", seamless);
        noiseType = (NoiseGenerator.NoiseType)EditorGUILayout.EnumPopup("Noise Type", noiseType);
        outputMode = (OutputMode)EditorGUILayout.EnumPopup("Output Mode", outputMode);

        if (outputMode == OutputMode.Texture2DSlices)
        {
            Draw2DSettingsGUI();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        string currentPath = GetCurrentOutputPath();
        currentPath = EditorGUILayout.TextField("Asset Path", currentPath);
        SetCurrentOutputPath(currentPath);

        if (GUILayout.Button("Browse...", GUILayout.MaxWidth(90)))
        {
            string directory = "Assets";
            string filename = outputMode == OutputMode.Texture3D ? "Noise3D.asset" : "Noise2D.asset";
            string activePath = GetCurrentOutputPath();
            if (!string.IsNullOrEmpty(activePath))
            {
                directory = Path.GetDirectoryName(activePath)?.Replace('\\', '/') ?? "Assets";
                var fn = Path.GetFileName(activePath);
                if (!string.IsNullOrEmpty(fn)) filename = fn;
            }

            string newPath = EditorUtility.SaveFilePanelInProject(
                outputMode == OutputMode.Texture3D ? "保存 Texture3D" : "保存 Texture2D",
                filename,
                "asset",
                outputMode == OutputMode.Texture3D ? "选择保存 Texture3D 的位置" : "选择保存 Texture2D 的位置",
                directory
            );
            if (!string.IsNullOrEmpty(newPath))
            {
                SetCurrentOutputPath(newPath);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(outputMode == OutputMode.Texture3D ? "Generate 3D Noise" : "Generate 2D Noise"))
        {
            if (outputMode == OutputMode.Texture3D)
            {
                Generate3DNoise();
            }
            else
            {
                Generate2DSliceNoise();
            }
        }

        using (new EditorGUI.DisabledScope(!HasCurrentTexture()))
        {
            if (GUILayout.Button(outputMode == OutputMode.Texture3D ? "Save Texture3D" : "Save Texture2D"))
            {
                SaveCurrentNoise();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        DrawPreviewGUI();
    }

    private void Draw2DSettingsGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2D Slice Settings", EditorStyles.boldLabel);
        sliceResolution = EditorGUILayout.IntSlider("Resolution (n*n)", Mathf.Clamp(sliceResolution, 8, 2048), 8, 2048);
        sliceChannels = EditorGUILayout.IntSlider("Channels", Mathf.Clamp(sliceChannels, 1, 4), 1, 4);
        sliceStart = EditorGUILayout.Slider("Slice Start", sliceStart, 0f, 1f);
        sliceDistance = EditorGUILayout.FloatField("Slice Distance", Mathf.Max(0f, sliceDistance));
    }

    private void DrawPreviewGUI()
    {
        if (outputMode == OutputMode.Texture2DSlices)
        {
            Draw2DPreviewGUI();
            return;
        }

        using (new EditorGUI.DisabledScope(noiseTexture3D == null))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            previewAxis = GUILayout.Toolbar(previewAxis, new[] { "X", "Y", "Z" });
            EditorGUILayout.EndHorizontal();

            int maxSlice = Mathf.Max(0, size - 1);
            previewSlice = EditorGUILayout.IntSlider("Slice", Mathf.Clamp(previewSlice, 0, maxSlice), 0, maxSlice);

            if (noiseTexture3D != null)
            {
                UpdatePreviewTexture3D();
                if (previewSlice2D != null)
                {
                    float w = EditorGUIUtility.currentViewWidth - 40;
                    float h = w;
                    Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(true));
                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.DrawTexture(r, previewSlice2D, ScaleMode.ScaleToFit, false);
                    }
                }
            }
        }
    }

    private void Draw2DPreviewGUI()
    {
        using (new EditorGUI.DisabledScope(noiseTexture2D == null))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Channel:", GUILayout.Width(70));
            
            string[] channelOptions = new string[sliceChannels + 1];
            for (int i = 0; i < sliceChannels; i++)
            {
                channelOptions[i] = ((char)('R' + i)).ToString();
            }
            channelOptions[sliceChannels] = "RGBA";
            
            int newChannel = EditorGUILayout.Popup(preview2DChannel, channelOptions, GUILayout.Width(80));
            if (newChannel != preview2DChannel)
            {
                preview2DChannel = newChannel;
            }
            
            EditorGUILayout.EndHorizontal();

            if (noiseTexture2D != null)
            {
                float w = EditorGUIUtility.currentViewWidth - 40;
                float h = w;
                Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    if (preview2DChannel < sliceChannels)
                    {
                        // Single channel preview
                        Texture2D channelPreview = Get2DChannelPreview(preview2DChannel);
                        if (channelPreview != null)
                        {
                            GUI.DrawTexture(r, channelPreview, ScaleMode.ScaleToFit, false);
                        }
                    }
                    else
                    {
                        // Show full RGBA
                        GUI.DrawTexture(r, noiseTexture2D, ScaleMode.ScaleToFit, false);
                    }
                }
            }
        }
    }

    private Texture2D Get2DChannelPreview(int channel)
    {
        if (noiseTexture2D == null || channel < 0 || channel >= 4) return null;

        int res = noiseTexture2D.width;
        Texture2D channelPreview = new Texture2D(res, res, TextureFormat.R8, false) 
        { 
            filterMode = FilterMode.Point 
        };

        Color[] sourceColors = noiseTexture2D.GetPixels();
        Color[] previewColors = new Color[res * res];

        for (int i = 0; i < sourceColors.Length; i++)
        {
            float value = sourceColors[i][channel];
            previewColors[i] = new Color(value, value, value, 1);
        }

        channelPreview.SetPixels(previewColors);
        channelPreview.Apply(false, false);

        return channelPreview;
    }

    private void UpdatePreviewTexture3D()
    {
        if (noiseTexture3D == null) return;
        if (previewSlice2D == null || previewSlice2D.width != size || previewSlice2D.height != size)
        {
            if (previewSlice2D != null) DestroyImmediate(previewSlice2D);
            previewSlice2D = new Texture2D(size, size, TextureFormat.R8, false) { filterMode = FilterMode.Point };
        }

        Color[] colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = SampleSlice(x, y);
                colors[x + y * size] = new Color(v, v, v, 1);
            }
        }
        previewSlice2D.SetPixels(colors);
        previewSlice2D.Apply(false, false);
    }

    private float SampleSlice(int x, int y)
    {
        // noiseTexture is RFloat encoded via SetPixels with noise in .r
        // Convert coordinates based on axis/slice
        int xi = 0, yi = 0, zi = 0;
        switch (previewAxis)
        {
            case 0: // X slice
                xi = previewSlice; yi = y; zi = x; break;
            case 1: // Y slice
                xi = x; yi = previewSlice; zi = y; break;
            default: // Z slice
                xi = x; yi = y; zi = previewSlice; break;
        }
        float wx = (float)xi / size;
        float wy = (float)yi / size;
        float wz = (float)zi / size;
        return NoiseGenerator.Sample3D(wx, wy, wz, scale, size, seamless, noiseType);
    }

    private void Generate3DNoise()
    {
        noiseTexture3D = NoiseGenerator.Generate3DTexture(size, scale, seamless, noiseType);

        Repaint();
        Debug.Log($"3D {noiseType} generated. Size: {size}");
    }

    private void Generate2DSliceNoise()
    {
        int resolution = Mathf.Clamp(sliceResolution, 8, 2048);
        int channels = Mathf.Clamp(sliceChannels, 1, 4);

        noiseTexture2D = NoiseGenerator.Generate2DSliceTexture(
            resolution,
            channels,
            sliceStart,
            sliceDistance,
            scale,
            size,
            seamless,
            noiseType);

        preview2DChannel = 0;
        Repaint();
        Debug.Log($"2D {noiseType} generated. Resolution: {resolution}x{resolution}, Channels: {channels}");
    }

    private bool HasCurrentTexture()
    {
        return outputMode == OutputMode.Texture3D ? noiseTexture3D != null : noiseTexture2D != null;
    }

    private string GetCurrentOutputPath()
    {
        return outputMode == OutputMode.Texture3D ? outputPath3D : outputPath2D;
    }

    private void SetCurrentOutputPath(string path)
    {
        if (outputMode == OutputMode.Texture3D)
        {
            outputPath3D = path;
        }
        else
        {
            outputPath2D = path;
        }
    }

    private void SaveCurrentNoise()
    {
        if (outputMode == OutputMode.Texture3D)
        {
            SaveTexture3D();
            return;
        }

        SaveTexture2D();
    }

    private void SaveTexture3D()
    {
        if (noiseTexture3D == null)
        {
            Debug.LogWarning("Noise texture is null. Generate first.");
            return;
        }

        if (string.IsNullOrEmpty(outputPath3D))
        {
            Debug.LogWarning("Output path is empty. Set a valid project-relative path like 'Assets/Textures/Noise3D.asset'.");
            return;
        }

        outputPath3D = outputPath3D.Replace('\\', '/');
        string dir = Path.GetDirectoryName(outputPath3D)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir) || !outputPath3D.StartsWith("Assets"))
        {
            Debug.LogError("Output path must be inside project, e.g., 'Assets/Textures/Noise3D.asset'.");
            return;
        }

        EnsureFolders(dir);

        var assetCopy = Object.Instantiate(noiseTexture3D);
        AssetDatabase.DeleteAsset(outputPath3D);
        AssetDatabase.CreateAsset(assetCopy, outputPath3D);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"3D Noise saved to: {outputPath3D}");
    }

    private void SaveTexture2D()
    {
        if (noiseTexture2D == null)
        {
            Debug.LogWarning("Noise texture is null. Generate first.");
            return;
        }

        if (string.IsNullOrEmpty(outputPath2D))
        {
            Debug.LogWarning("Output path is empty. Set a valid project-relative path like 'Assets/Textures/Noise2D.asset'.");
            return;
        }

        outputPath2D = outputPath2D.Replace('\\', '/');
        string dir = Path.GetDirectoryName(outputPath2D)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir) || !outputPath2D.StartsWith("Assets"))
        {
            Debug.LogError("Output path must be inside project, e.g., 'Assets/Textures/Noise2D.asset'.");
            return;
        }

        EnsureFolders(dir);

        var assetCopy = Object.Instantiate(noiseTexture2D);
        AssetDatabase.DeleteAsset(outputPath2D);
        AssetDatabase.CreateAsset(assetCopy, outputPath2D);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"2D Noise saved to: {outputPath2D}");
    }

    private static void EnsureFolders(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return;
        fullPath = fullPath.Replace('\\', '/');
        if (!fullPath.StartsWith("Assets")) return;

        string[] parts = fullPath.Split('/');
        string current = parts[0]; // Assets
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parts[i];
            if (string.IsNullOrEmpty(next)) continue;
            string combined = current + "/" + next;
            if (!AssetDatabase.IsValidFolder(combined))
            {
                AssetDatabase.CreateFolder(current, next);
            }
            current = combined;
        }
    }
}
#endif
