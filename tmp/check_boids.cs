using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        var allObjects = Object.FindObjectsOfType<GameObject>(true);
        var boidsObjects = new System.Collections.Generic.List<GameObject>();

        foreach (var go in allObjects)
        {
            var bm = go.GetComponent("BoidsManager");
            if (bm != null)
                boidsObjects.Add(go);
        }

        if (boidsObjects.Count == 0)
        {
            sb.AppendLine("RESULT: No BoidsManager found");
        }
        else
        {
            foreach (var go in boidsObjects)
                sb.AppendLine($"BoidsManager on: \"{go.name}\" (root: {go.transform.root.name})");
            sb.AppendLine($"RESULT: {boidsObjects.Count} instance(s)");
        }

        // Also check for BoidsSimulation
        foreach (var go in allObjects)
        {
            var bs = go.GetComponent("BoidsSimulation");
            if (bs != null)
                sb.AppendLine($"BoidsSimulation on: \"{go.name}\"");
        }

        return sb.ToString();
    }
}
