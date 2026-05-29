#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class NoiseGeneratorWindow : EditorWindow
{
    private enum OutputMode
    {
        Texture3D,
        Texture2D
    }

    [System.Serializable]
    private class Texture3DSettings
    {
        public int size = 32;
        public float scale = 4f;
        public bool seamless = true;
        public NoiseGenerator.NoiseType noiseType = NoiseGenerator.NoiseType.Perlin;
    }

    [System.Serializable]
    private class ChannelSettings
    {
        public bool foldout = true;
        public int resolution = 32;
        public float scale = 4f;
        public bool seamless = true;
        public NoiseGenerator.NoiseType noiseType = NoiseGenerator.NoiseType.Perlin;
        public float randomSeed = 0f;
        public Texture2D previewTexture;
        public int previewHash;
    }

    [SerializeField] private OutputMode outputMode = OutputMode.Texture3D;
    [SerializeField] private Texture3DSettings texture3DSettings = new Texture3DSettings();
    [SerializeField] private ChannelSettings[] channelSettings = new ChannelSettings[4];
    [SerializeField] private int activeChannelCount = 4;
    [SerializeField] private int[] previewSlices3D = new int[3];
    [SerializeField] private bool[] previewFoldouts3D = new bool[3] { true, true, true };
    [SerializeField] private bool packedPreviewFoldout = true;
    [SerializeField] private int selectedChannel2D = 0;
    [SerializeField] private int selectedAxis3D = 0;
    [SerializeField] private Vector2 mainScrollPos = Vector2.zero;

    private string outputPath3D = "Assets/Textures/Noise3D.asset";
    private string outputPath2D = "Assets/Textures/Noise2D.asset";

    private Texture3D noiseTexture3D;
    private Texture2D packedNoiseTexture2D;
    private Texture2D[] previewSlices3DTextures = new Texture2D[3];

    private static readonly string[] AxisLabels = { "X", "Y", "Z" };
    private static readonly string[] ChannelLabels = { "R", "G", "B", "A" };

    [MenuItem("Tools/Noise Generator...")]
    public static void ShowWindow()
    {
        var window = GetWindow<NoiseGeneratorWindow>(false, "Noise Generator", true);
        window.minSize = new Vector2(440, 520);
        window.Show();
    }

    private void OnEnable()
    {
        EnsureChannelSettings();
        EnsurePreviewSlices();
    }

    private void OnDisable()
    {
        ReleaseTexture(ref noiseTexture3D);
        ReleaseTexture(ref packedNoiseTexture2D);

        for (int i = 0; i < previewSlices3DTextures.Length; i++)
        {
            ReleaseTexture(ref previewSlices3DTextures[i]);
        }

        for (int i = 0; i < channelSettings.Length; i++)
        {
            if (channelSettings[i] != null)
            {
                ReleaseTexture(ref channelSettings[i].previewTexture);
            }
        }
    }

    private void OnGUI()
    {
        EnsureChannelSettings();
        EnsurePreviewSlices();

        EditorGUILayout.LabelField("Noise Generator", EditorStyles.boldLabel);

        // Wrap the whole panel in a single scroll view so the scrollbar controls the entire window content
        mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

        outputMode = (OutputMode)GUILayout.Toolbar((int)outputMode, new[] { "Texture3D", "Texture2D" });

        EditorGUILayout.Space(6);
        if (outputMode == OutputMode.Texture3D)
        {
            DrawTexture3DSettings();
        }
        else
        {
            DrawTexture2DSettings();
        }

        EditorGUILayout.Space(8);
        DrawOutputSection();

        EditorGUILayout.Space(8);
        if (outputMode == OutputMode.Texture3D)
        {
            DrawTexture3DPreviewSection();
        }
        else
        {
            DrawTexture2DPreviewSection();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTexture3DSettings()
    {
        EditorGUILayout.LabelField("Texture3D Settings", EditorStyles.boldLabel);
        texture3DSettings.size = EditorGUILayout.IntSlider("Size", Mathf.Clamp(texture3DSettings.size, 4, 256), 4, 256);
        texture3DSettings.scale = EditorGUILayout.FloatField("Scale", Mathf.Max(0.0001f, texture3DSettings.scale));
        texture3DSettings.seamless = EditorGUILayout.Toggle("Seamless", texture3DSettings.seamless);
        texture3DSettings.noiseType = (NoiseGenerator.NoiseType)EditorGUILayout.EnumPopup("Noise Type", texture3DSettings.noiseType);
    }

    private void DrawTexture2DSettings()
    {
        EditorGUILayout.LabelField("Texture2D Settings", EditorStyles.boldLabel);
        activeChannelCount = EditorGUILayout.IntSlider("Channels", Mathf.Clamp(activeChannelCount, 1, 4), 1, 4);
        EditorGUILayout.HelpBox("Final packed output uses the max resolution of active channels.", MessageType.Info);

        selectedChannel2D = DrawChannelSelector(selectedChannel2D, activeChannelCount);
        DrawChannelSettings(selectedChannel2D);
    }

    private void DrawChannelSettings(int index)
    {
        ChannelSettings settings = channelSettings[index];

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Channel {ChannelLabels[index]}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Randomize", GUILayout.Width(88)))
        {
            settings.randomSeed = Random.value * 100000f;
        }
        EditorGUILayout.EndHorizontal();

        settings.resolution = EditorGUILayout.IntSlider("Resolution", Mathf.Clamp(settings.resolution, 8, 2048), 8, 2048);
        settings.scale = EditorGUILayout.FloatField("Scale", Mathf.Max(0.0001f, settings.scale));
        settings.seamless = EditorGUILayout.Toggle("Seamless", settings.seamless);
        settings.noiseType = (NoiseGenerator.NoiseType)EditorGUILayout.EnumPopup("Noise Type", settings.noiseType);
        settings.randomSeed = EditorGUILayout.FloatField("Random Seed", settings.randomSeed);

        UpdateChannelPreview(index);
        DrawTexturePreview(settings.previewTexture);

        EditorGUILayout.EndVertical();
    }

    private int DrawChannelSelector(int currentIndex, int channelCount)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Channel", GUILayout.Width(52));

        int selected = currentIndex;
        for (int i = 0; i < 4; i++)
        {
            using (new EditorGUI.DisabledScope(i >= channelCount))
            {
                bool isSelected = selected == i;
                if (GUILayout.Toggle(isSelected, ChannelLabels[i], "Button", GUILayout.Height(22)))
                {
                    selected = i;
                }
            }
        }

        EditorGUILayout.EndHorizontal();
        return Mathf.Clamp(selected, 0, Mathf.Max(0, channelCount - 1));
    }

    private void DrawOutputSection()
    {
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
                string fileName = Path.GetFileName(activePath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    filename = fileName;
                }
            }

            string newPath = EditorUtility.SaveFilePanelInProject(
                outputMode == OutputMode.Texture3D ? "保存 Texture3D" : "保存 Texture2D",
                filename,
                "asset",
                outputMode == OutputMode.Texture3D ? "选择保存 Texture3D 的位置" : "选择保存 Texture2D 的位置",
                directory);

            if (!string.IsNullOrEmpty(newPath))
            {
                SetCurrentOutputPath(newPath);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(outputMode == OutputMode.Texture3D ? "Generate Texture3D" : "Generate Texture2D"))
        {
            if (outputMode == OutputMode.Texture3D)
            {
                Generate3DNoise();
            }
            else
            {
                Generate2DNoise();
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
    }

    private void DrawTexture3DPreviewSection()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        // Axis selector buttons (X/Y/Z)
        selectedAxis3D = GUILayout.Toolbar(selectedAxis3D, AxisLabels);
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Axis {AxisLabels[selectedAxis3D]} Preview", EditorStyles.boldLabel);

        int maxSlice = Mathf.Max(0, texture3DSettings.size - 1);
        previewSlices3D[selectedAxis3D] = EditorGUILayout.IntSlider("Slice", Mathf.Clamp(previewSlices3D[selectedAxis3D], 0, maxSlice), 0, maxSlice);

        UpdatePreviewTexture3D(selectedAxis3D);
        DrawTexturePreview(previewSlices3DTextures[selectedAxis3D]);

        EditorGUILayout.EndVertical();
    }

    private void DrawTexture3DPreviewPanel(int axis)
    {
        EditorGUILayout.BeginVertical("box");
        previewFoldouts3D[axis] = EditorGUILayout.Foldout(previewFoldouts3D[axis], $"Axis {AxisLabels[axis]} Preview", true);

        if (previewFoldouts3D[axis])
        {
            int maxSlice = Mathf.Max(0, texture3DSettings.size - 1);
            previewSlices3D[axis] = EditorGUILayout.IntSlider("Slice", Mathf.Clamp(previewSlices3D[axis], 0, maxSlice), 0, maxSlice);

            UpdatePreviewTexture3D(axis);
            DrawTexturePreview(previewSlices3DTextures[axis]);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawTexture2DPreviewSection()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        selectedChannel2D = DrawChannelSelector(selectedChannel2D, activeChannelCount);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Channel {ChannelLabels[selectedChannel2D]} Preview", EditorStyles.boldLabel);
        UpdateChannelPreview(selectedChannel2D);
        DrawTexturePreview(channelSettings[selectedChannel2D].previewTexture);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        packedPreviewFoldout = EditorGUILayout.Foldout(packedPreviewFoldout, "Packed RGBA Preview", true);
        if (packedPreviewFoldout)
        {
            UpdatePackedPreviewTexture();
            DrawTexturePreview(packedNoiseTexture2D);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawTexturePreview(Texture2D texture)
    {
        if (texture == null)
        {
            EditorGUILayout.HelpBox("Preview is not ready.", MessageType.None);
            return;
        }

        float width = EditorGUIUtility.currentViewWidth - 40f;
        float height = width;
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
        }
    }

    private void UpdatePreviewTexture3D(int axis)
    {
        int size = Mathf.Clamp(texture3DSettings.size, 4, 256);
        int maxSlice = Mathf.Max(0, size - 1);
        previewSlices3D[axis] = Mathf.Clamp(previewSlices3D[axis], 0, maxSlice);

        int hash = Get3DPreviewHash(axis, size);
        if (previewSlices3DTextures[axis] != null && previewSlices3DTextures[axis].width == size && previewSlices3DTextures[axis].height == size)
        {
            if (previewSlices3DTextures[axis].name == hash.ToString())
            {
                return;
            }
        }

        ReleaseTexture(ref previewSlices3DTextures[axis]);
        previewSlices3DTextures[axis] = new Texture2D(size, size, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            name = hash.ToString()
        };

        Color[] colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = Sample3DSlice(axis, x, y, previewSlices3D[axis], size);
                colors[x + y * size] = new Color(value, value, value, 1f);
            }
        }

        previewSlices3DTextures[axis].SetPixels(colors);
        previewSlices3DTextures[axis].Apply(false, false);
    }

    private void UpdateChannelPreview(int index)
    {
        ChannelSettings settings = channelSettings[index];
        int resolution = Mathf.Clamp(settings.resolution, 8, 2048);
        int hash = GetChannelPreviewHash(settings, resolution);

        if (settings.previewTexture != null && settings.previewTexture.width == resolution && settings.previewTexture.height == resolution)
        {
            if (settings.previewHash == hash)
            {
                return;
            }
        }

        ReleaseTexture(ref settings.previewTexture);
        settings.previewTexture = NoiseGenerator.GenerateChannelTexture(
            resolution,
            settings.scale,
            settings.seamless,
            settings.noiseType,
            settings.randomSeed);
        settings.previewTexture.name = hash.ToString();
        settings.previewHash = hash;
    }

    private void UpdatePackedPreviewTexture()
    {
        int activeCount = Mathf.Clamp(activeChannelCount, 1, 4);
        int outputResolution = GetPackedResolution(activeCount);
        int hash = GetPackedPreviewHash(activeCount, outputResolution);

        if (packedNoiseTexture2D != null && packedNoiseTexture2D.width == outputResolution && packedNoiseTexture2D.height == outputResolution)
        {
            if (packedNoiseTexture2D.name == hash.ToString())
            {
                return;
            }
        }

        ReleaseTexture(ref packedNoiseTexture2D);
        packedNoiseTexture2D = new Texture2D(outputResolution, outputResolution, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            name = hash.ToString()
        };

        Color[] colors = new Color[outputResolution * outputResolution];
        for (int y = 0; y < outputResolution; y++)
        {
            float v = (float)y / outputResolution;
            for (int x = 0; x < outputResolution; x++)
            {
                float u = (float)x / outputResolution;
                Color c = new Color(0f, 0f, 0f, 1f);

                for (int channel = 0; channel < activeCount; channel++)
                {
                    Texture2D source = channelSettings[channel].previewTexture;
                    float sample = source != null ? source.GetPixelBilinear(u, v).r : 0f;
                    c[channel] = sample;
                }

                colors[x + y * outputResolution] = c;
            }
        }

        packedNoiseTexture2D.SetPixels(colors);
        packedNoiseTexture2D.Apply(false, false);
    }

    private int GetPackedResolution(int activeCount)
    {
        int outputResolution = 8;
        for (int i = 0; i < activeCount; i++)
        {
            outputResolution = Mathf.Max(outputResolution, Mathf.Clamp(channelSettings[i].resolution, 8, 2048));
        }

        return outputResolution;
    }

    private int Get3DPreviewHash(int axis, int size)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + axis;
            hash = hash * 31 + size;
            hash = hash * 31 + texture3DSettings.scale.GetHashCode();
            hash = hash * 31 + texture3DSettings.seamless.GetHashCode();
            hash = hash * 31 + texture3DSettings.noiseType.GetHashCode();
            hash = hash * 31 + previewSlices3D[axis];
            return hash;
        }
    }

    private int GetChannelPreviewHash(ChannelSettings settings, int resolution)
    {
        unchecked
        {
            int hash = 23;
            hash = hash * 31 + resolution;
            hash = hash * 31 + settings.scale.GetHashCode();
            hash = hash * 31 + settings.seamless.GetHashCode();
            hash = hash * 31 + settings.noiseType.GetHashCode();
            hash = hash * 31 + settings.randomSeed.GetHashCode();
            return hash;
        }
    }

    private int GetPackedPreviewHash(int activeCount, int resolution)
    {
        unchecked
        {
            int hash = 29;
            hash = hash * 31 + activeCount;
            hash = hash * 31 + resolution;
            for (int i = 0; i < activeCount; i++)
            {
                ChannelSettings settings = channelSettings[i];
                hash = hash * 31 + settings.previewHash;
            }
            return hash;
        }
    }

    private float Sample3DSlice(int axis, int x, int y, int slice, int size)
    {
        int xi = 0;
        int yi = 0;
        int zi = 0;

        switch (axis)
        {
            case 0:
                xi = slice;
                yi = y;
                zi = x;
                break;
            case 1:
                xi = x;
                yi = slice;
                zi = y;
                break;
            default:
                xi = x;
                yi = y;
                zi = slice;
                break;
        }

        float wx = (float)xi / size;
        float wy = (float)yi / size;
        float wz = (float)zi / size;
        return NoiseGenerator.Sample3D(wx, wy, wz, texture3DSettings.scale, size, texture3DSettings.seamless, texture3DSettings.noiseType);
    }

    private void Generate3DNoise()
    {
        ReleaseTexture(ref noiseTexture3D);
        noiseTexture3D = NoiseGenerator.Generate3DTexture(
            Mathf.Clamp(texture3DSettings.size, 4, 256),
            texture3DSettings.scale,
            texture3DSettings.seamless,
            texture3DSettings.noiseType);

        Repaint();
        Debug.Log($"3D {texture3DSettings.noiseType} generated. Size: {texture3DSettings.size}");
    }

    private void Generate2DNoise()
    {
        EnsureChannelSettings();

        int activeCount = Mathf.Clamp(activeChannelCount, 1, 4);
        for (int i = 0; i < activeCount; i++)
        {
            UpdateChannelPreview(i);
        }

        UpdatePackedPreviewTexture();
        Repaint();
        Debug.Log($"2D packed noise generated. Channels: {activeCount}, Resolution: {packedNoiseTexture2D.width}x{packedNoiseTexture2D.height}");
    }

    private bool HasCurrentTexture()
    {
        return outputMode == OutputMode.Texture3D ? noiseTexture3D != null : packedNoiseTexture2D != null;
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
        }
        else
        {
            SaveTexture2D();
        }
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
        if (packedNoiseTexture2D == null)
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

        var assetCopy = Object.Instantiate(packedNoiseTexture2D);
        AssetDatabase.DeleteAsset(outputPath2D);
        AssetDatabase.CreateAsset(assetCopy, outputPath2D);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"2D Noise saved to: {outputPath2D}");
    }

    private void EnsureChannelSettings()
    {
        if (channelSettings == null || channelSettings.Length != 4)
        {
            channelSettings = new ChannelSettings[4];
        }

        for (int i = 0; i < channelSettings.Length; i++)
        {
            if (channelSettings[i] == null)
            {
                channelSettings[i] = new ChannelSettings();
            }
        }

        activeChannelCount = Mathf.Clamp(activeChannelCount, 1, 4);
    }

    private void EnsurePreviewSlices()
    {
        if (previewSlices3D == null || previewSlices3D.Length != 3)
        {
            previewSlices3D = new int[3];
        }

        if (previewFoldouts3D == null || previewFoldouts3D.Length != 3)
        {
            previewFoldouts3D = new bool[3] { true, true, true };
        }

        if (previewSlices3DTextures == null || previewSlices3DTextures.Length != 3)
        {
            previewSlices3DTextures = new Texture2D[3];
        }

        selectedChannel2D = Mathf.Clamp(selectedChannel2D, 0, 3);
        selectedAxis3D = Mathf.Clamp(selectedAxis3D, 0, 2);
    }

    private static void ReleaseTexture(ref Texture2D texture)
    {
        if (texture != null)
        {
            DestroyImmediate(texture);
            texture = null;
        }
    }

    private static void ReleaseTexture(ref Texture3D texture)
    {
        if (texture != null)
        {
            DestroyImmediate(texture);
            texture = null;
        }
    }

    private static void EnsureFolders(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return;
        }

        fullPath = fullPath.Replace('\\', '/');
        if (!fullPath.StartsWith("Assets"))
        {
            return;
        }

        string[] parts = fullPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parts[i];
            if (string.IsNullOrEmpty(next))
            {
                continue;
            }

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
