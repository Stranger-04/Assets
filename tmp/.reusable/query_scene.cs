using UnityEngine;

/// <summary>Query scene hierarchy — list all root GameObjects and their children.</summary>
public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            ListTree(root, 0, sb);
        return sb.ToString();
    }

    static void ListTree(GameObject go, int depth, System.Text.StringBuilder sb)
    {
        var indent = new string(' ', depth * 2);
        var comps = go.GetComponents<Component>();
        var desc = "";
        for (int i = 1; i < comps.Length && i < 4; i++) // skip Transform[0]
            desc += " [" + comps[i].GetType().Name + "]";
        sb.AppendLine($"{indent}{go.name}{desc} (active={go.activeSelf})");
        foreach (Transform child in go.transform)
            ListTree(child.gameObject, depth + 1, sb);
    }
}
