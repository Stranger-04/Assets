using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Organize test objects in the scene into a __TestObjects__ container.</summary>
public class Script
{
    static string[] TestPatterns = { "Test", "CP", "Temp", "Debug" };

    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        var testObjects = new System.Collections.Generic.List<GameObject>();

        var allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in allRoots)
            Collect(root, testObjects);

        if (testObjects.Count == 0) return "No test objects found.";

        var old = GameObject.Find("__TestObjects__");
        if (old != null) Object.DestroyImmediate(old);

        var container = new GameObject("__TestObjects__");
        Undo.RegisterCreatedObjectUndo(container, "Create TestObjects container");

        var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<GameObject>>();
        foreach (var go in testObjects)
        {
            var g = Categorize(go.name);
            if (!groups.ContainsKey(g)) groups[g] = new System.Collections.Generic.List<GameObject>();
            groups[g].Add(go);
        }

        int moved = 0;
        foreach (var kvp in groups)
        {
            var gGo = new GameObject($"__{kvp.Key}__");
            gGo.transform.SetParent(container.transform);
            Undo.RegisterCreatedObjectUndo(gGo, "Create group");
            foreach (var go in kvp.Value)
            {
                Undo.SetTransformParent(go.transform, gGo.transform, "Move test object");
                moved++;
            }
            sb.AppendLine($"  [{kvp.Key}] x{kvp.Value.Count}");
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        sb.Insert(0, $"Organized {moved} objects into __TestObjects__:\n");
        return sb.ToString();
    }

    static void Collect(GameObject go, System.Collections.Generic.List<GameObject> list)
    {
        foreach (Transform child in go.transform) Collect(child.gameObject, list);
        foreach (var p in TestPatterns)
            if (go.name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0)
            { list.Add(go); break; }
    }

    static string Categorize(string name)
    {
        var l = name.ToLower();
        if (l.Contains("rain")) return "Rain";
        if (l.Contains("fish")) return "Fish";
        if (l.Contains("boid")) return "Boids";
        if (l.Contains("heart")) return "Curve";
        if (l.Contains("picker")) return "Picker";
        return "Other";
    }
}
