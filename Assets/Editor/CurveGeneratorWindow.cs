#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Editor window for baking control-point curves into CurveAsset ScriptableObjects.
/// Thin GUI shell — all math is delegated to <see cref="CurveBake"/>.
/// </summary>
public class CurveGeneratorWindow : EditorWindow
{
    // ── Input ──────────────────────────────────────────────
    [SerializeField] private bool      _useParentMode;
    [SerializeField] private Transform _controlParent;          // empty GO: reads its direct children
    [SerializeField] private Transform[] _controlPoints = new Transform[0];  // manual mode
    [SerializeField] private CurveAsset.CurveType      _curveType  = CurveAsset.CurveType.CatmullRom;
    [SerializeField] private CurveAsset.CurveDimension  _dimension  = CurveAsset.CurveDimension.XYZ;
    [SerializeField] private int    _sampleCount = 256;
    [SerializeField] private bool   _loop;

    // ── Output ─────────────────────────────────────────────
    [SerializeField] private string _outputPath = "Assets/Mine/Curves/NewCurve.asset";

    // ── Generated ──────────────────────────────────────────
    private CurveBake.Result _result;
    private bool             _hasData;

    // ── Scroll ─────────────────────────────────────────────
    private Vector2 _scrollPos;

    private static readonly string[] DimensionLabels = { "XYZ", "XY", "XZ", "YZ" };

    [MenuItem("Tools/Curve Generator...")]
    public static void ShowWindow()
    {
        var window = GetWindow<CurveGeneratorWindow>(false, "Curve Generator", true);
        window.minSize = new Vector2(380, 480);
        window.Show();
    }

    private void OnEnable()  { SceneView.duringSceneGui += OnSceneGUI; }
    private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; }

    // ═══════════════════════════════════════════════════════════
    //  GUI
    // ═══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.LabelField("Curve Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);
        DrawControlPoints();
        EditorGUILayout.Space(6);
        DrawSettings();
        EditorGUILayout.Space(8);
        DrawOutput();
        EditorGUILayout.Space(8);
        DrawInfo();

        EditorGUILayout.EndScrollView();
    }

    private void DrawControlPoints()
    {
        EditorGUILayout.LabelField("Control Points", EditorStyles.boldLabel);

        // Mode toggle
        _useParentMode = EditorGUILayout.Toggle("Use Parent Object", _useParentMode);

        if (_useParentMode)
        {
            EditorGUI.indentLevel++;
            _controlParent = (Transform)EditorGUILayout.ObjectField(
                "Parent", _controlParent, typeof(Transform), true);

            if (_controlParent != null)
            {
                int childCount = _controlParent.childCount;
                EditorGUILayout.LabelField($"  → {childCount} children found");
                if (childCount < 2)
                    EditorGUILayout.HelpBox("Need at least 2 children as control points.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Assign an empty GameObject whose direct children are the control points.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
        else
        {
            int newSize = EditorGUILayout.IntField("Count", _controlPoints.Length);
            if (newSize != _controlPoints.Length && newSize >= 0)
                System.Array.Resize(ref _controlPoints, newSize);

            if (_controlPoints.Length < 2)
                EditorGUILayout.HelpBox("Need at least 2 control points.", MessageType.Warning);

            for (int i = 0; i < _controlPoints.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"CP {i}", GUILayout.Width(36));
                _controlPoints[i] = (Transform)EditorGUILayout.ObjectField(
                    _controlPoints[i], typeof(Transform), true);
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    /// <summary>Gather control point positions from current mode.</summary>
    private Vector3[] GatherControlPointPositions()
    {
        var pts = new List<Vector3>();
        if (_useParentMode)
        {
            if (_controlParent != null)
            {
                for (int i = 0; i < _controlParent.childCount; i++)
                    pts.Add(_controlParent.GetChild(i).position);
            }
        }
        else
        {
            for (int i = 0; i < _controlPoints.Length; i++)
                if (_controlPoints[i] != null)
                    pts.Add(_controlPoints[i].position);
        }
        return pts.ToArray();
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("Curve Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Dimension", GUILayout.Width(72));
        int dimIndex = (int)_dimension;
        dimIndex = GUILayout.Toolbar(dimIndex, DimensionLabels);
        _dimension = (CurveAsset.CurveDimension)dimIndex;
        EditorGUILayout.EndHorizontal();

        _curveType = (CurveAsset.CurveType)EditorGUILayout.EnumPopup("Type", _curveType);
        _sampleCount = EditorGUILayout.IntSlider("Samples", Mathf.Clamp(_sampleCount, 16, 4096), 16, 4096);
        _loop = EditorGUILayout.Toggle("Loop", _loop);
    }

    private void DrawOutput()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Asset Path", _outputPath);
        if (GUILayout.Button("Browse...", GUILayout.MaxWidth(90)))
        {
            string dir = "Assets/Mine/Curves";
            string fn  = "NewCurve.asset";
            if (!string.IsNullOrEmpty(_outputPath))
            {
                dir = Path.GetDirectoryName(_outputPath)?.Replace('\\', '/') ?? "Assets";
                fn  = Path.GetFileName(_outputPath);
            }
            string p = EditorUtility.SaveFilePanelInProject("Save Curve Asset", fn, "asset",
                "Choose where to save.", dir);
            if (!string.IsNullOrEmpty(p)) _outputPath = p;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate", GUILayout.Height(28))) Generate();
        using (new EditorGUI.DisabledScope(!_hasData))
            if (GUILayout.Button("Save", GUILayout.Height(28))) Save();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawInfo()
    {
        if (!_hasData) return;
        EditorGUILayout.LabelField("Preview Info", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  Type: {_curveType}   Dim: {_dimension}   Loop: {_loop}");
        EditorGUILayout.LabelField($"  Samples: {_result.sampleCount}   Length: {_result.totalLength:F2}");
        EditorGUILayout.HelpBox("Curve preview is drawn in the Scene view.", MessageType.Info);
    }

    // ═══════════════════════════════════════════════════════════
    //  Generate / Save
    // ═══════════════════════════════════════════════════════════

    public void Generate()
    {
        var pts = GatherControlPointPositions();

        if (pts.Length < 2)
        {
            Debug.LogWarning("CurveGenerator: Need at least 2 valid control points.");
            _hasData = false;
            return;
        }

        _result = CurveBake.Bake(pts, _curveType, _dimension, _sampleCount, _loop);
        _hasData = true;
        Repaint();

        Debug.Log($"Curve baked [{_curveType}/{_dimension}]: {_result.sampleCount} samples, length={_result.totalLength:F2}");
    }

    private void Save()
    {
        if (!_hasData) { Debug.LogWarning("Generate first."); return; }

        _outputPath = _outputPath.Replace('\\', '/');
        if (!_outputPath.StartsWith("Assets"))
        {
            Debug.LogError("Path must be inside project (e.g. Assets/Mine/Curves/My.asset).");
            return;
        }

        var asset = CreateInstance<CurveAsset>();
        asset.curveType   = _curveType;
        asset.dimension   = _dimension;
        asset.loop        = _loop;
        asset.positions   = _result.positions;
        asset.tangents    = _result.tangents;
        asset.arcLengths  = _result.arcLengths;
        asset.normals     = _result.normals;
        asset.curvatures  = _result.curvatures;
        asset.totalLength = _result.totalLength;
        asset.sampleCount = _result.sampleCount;

        var cpList = GatherControlPointPositions();
        asset.controlPointCount     = cpList.Length;
        asset.controlPointPositions = cpList;

        string dir = Path.GetDirectoryName(_outputPath)?.Replace('\\', '/');
        EnsureFolders(dir);
        AssetDatabase.DeleteAsset(_outputPath);
        AssetDatabase.CreateAsset(asset, _outputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"CurveAsset saved → {_outputPath}");
    }

    // ═══════════════════════════════════════════════════════════
    //  Scene Preview
    // ═══════════════════════════════════════════════════════════

    private void OnSceneGUI(SceneView sv)
    {
        if (!_hasData || _result.positions == null || _result.positions.Length < 2) return;
        if (Event.current.type != EventType.Repaint) return;  // only draw during repaint to avoid flicker

        // Control points
        Handles.color = Color.yellow;
        if (_useParentMode && _controlParent != null)
        {
            for (int i = 0; i < _controlParent.childCount; i++)
            {
                var child = _controlParent.GetChild(i);
                float r = HandleUtility.GetHandleSize(child.position) * 0.1f;
                Handles.SphereHandleCap(0, child.position, Quaternion.identity, r, EventType.Repaint);
                Handles.Label(child.position + Vector3.up * r * 2, $"CP{i}");
            }
        }
        else
        {
            for (int i = 0; i < _controlPoints.Length; i++)
            {
                if (_controlPoints[i] == null) continue;
                float r = HandleUtility.GetHandleSize(_controlPoints[i].position) * 0.1f;
                Handles.SphereHandleCap(0, _controlPoints[i].position, Quaternion.identity, r, EventType.Repaint);
                Handles.Label(_controlPoints[i].position + Vector3.up * r * 2, $"CP{i}");
            }
        }

        // Curve
        Handles.color = new Color(0, 1, 1, 0.9f);
        for (int i = 0; i < _result.positions.Length - 1; i++)
            Handles.DrawLine(_result.positions[i], _result.positions[i + 1], 1.5f);
        if (_loop)
            Handles.DrawLine(_result.positions[_result.positions.Length - 1], _result.positions[0], 1.5f);

        // Tangents (sparse)
        Handles.color = new Color(1, 0.5f, 0, 0.6f);
        int stride = Mathf.Max(1, _result.positions.Length / 32);
        for (int i = 0; i < _result.positions.Length; i += stride)
        {
            Vector3 p = _result.positions[i];
            Vector3 t = _result.tangents[i];
            float len = HandleUtility.GetHandleSize(p) * 0.15f;
            Handles.DrawLine(p, p + t * len, 1f);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Utility
    // ═══════════════════════════════════════════════════════════

    private static void EnsureFolders(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || !fullPath.StartsWith("Assets")) return;
        fullPath = fullPath.Replace('\\', '/');
        string[] parts = fullPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length - 1; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            string combined = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(combined))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = combined;
        }
    }
}
#endif
