#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Mine.FGDLutBaker;

public class FGDLutBakerWindow : EditorWindow
{
    [SerializeField] private int    _resolution  = 128;
    [SerializeField] private int    _sampleCount = 1024;
    [SerializeField] private string _savePath    = "Assets/Textures/FGD_LUT.asset";
    [SerializeField] private string _loadPath    = "Assets/Textures/FGD_LUT.asset";

    private Texture2D _bakedTexture;
    private bool      _isBaked;  // true = 自己烘焙需清理，false = 从磁盘载入不清理
    private Vector2   _scrollPos;

    [MenuItem("Tools/FGD Lut Baker...")]
    public static void ShowWindow()
    {
        var window = GetWindow<FGDLutBakerWindow>(false, "FGD Lut Baker", true);
        window.minSize = new Vector2(360, 400);
        window.Show();
    }

    private void OnDisable()
    {
        if (_bakedTexture != null && _isBaked)
            DestroyImmediate(_bakedTexture, true);
        _bakedTexture = null;
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.LabelField("FGD Lut Baker", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _resolution  = EditorGUILayout.IntSlider("Resolution", _resolution, 16, 512);
        _sampleCount = EditorGUILayout.IntSlider("Sample Count", _sampleCount, 64, 8192);

        EditorGUILayout.Space(4);
        DrawPathField("Save Path", ref _savePath);
        DrawPathField("Load Path", ref _loadPath);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bake", GUILayout.Height(30))) Bake();
        if (GUILayout.Button("Load", GUILayout.Height(30))) LoadAsset();
        GUI.enabled = _bakedTexture != null;
        if (GUILayout.Button("Save", GUILayout.Height(30))) SaveAsset();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = _bakedTexture != null;
        if (GUILayout.Button("Set LUT", GUILayout.Height(26))) FGDLutBaker.SetGlobalLut(_bakedTexture);
        GUI.enabled = true;
        if (GUILayout.Button("Clear LUT", GUILayout.Height(26))) FGDLutBaker.ClearGlobalLut();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        if (_bakedTexture != null)
        {
            float w = EditorGUIUtility.currentViewWidth - 32f;
            Rect r = GUILayoutUtility.GetRect(w, w, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawPreviewTexture(r, _bakedTexture, null, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUILayout.HelpBox("Bake or load a FGD LUT to preview.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawPathField(string label, ref string path)
    {
        EditorGUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(label, path);
        if (GUILayout.Button("...", GUILayout.Width(32)))
        {
            string dir  = Path.GetDirectoryName(path) ?? "Assets";
            string name = Path.GetFileName(path);
            string chosen = EditorUtility.SaveFilePanelInProject(label, name, "asset", "", dir);
            if (!string.IsNullOrEmpty(chosen)) path = chosen;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void Bake()
    {
        if (_bakedTexture != null && _isBaked) DestroyImmediate(_bakedTexture, true);
        EditorUtility.DisplayProgressBar("FGD Lut Baker", "Baking...", 0.5f);
        try { _bakedTexture = FGDLutBaker.Bake(_resolution, _sampleCount); }
        finally { EditorUtility.ClearProgressBar(); }
        _isBaked = true;
        Repaint();
    }

    private void LoadAsset()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(_loadPath);
        if (tex == null)
        {
            Debug.LogWarning($"FGD Lut Baker: No texture at \"{_loadPath}\"");
            return;
        }
        if (_bakedTexture != null && _isBaked) DestroyImmediate(_bakedTexture, true);
        _bakedTexture = tex;
        _isBaked = false;
        Repaint();
    }

    private void SaveAsset()
    {
        if (_bakedTexture == null) return;
        string path = _savePath.Replace('\\', '/');
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) return;

        string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir)) EnsureFolders(dir);

        var copy = Object.Instantiate(_bakedTexture);
        copy.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"FGD LUT saved to {path}");
    }

    private static void EnsureFolders(string path)
    {
        path = path.Replace('\\', '/');
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
