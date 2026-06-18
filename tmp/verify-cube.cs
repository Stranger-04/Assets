using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class Script
{
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.GetRootGameObjects()
            .Where(g => g.name.Contains("CLI"))
            .Select(g => new {
                name = g.name,
                position = g.transform.position.ToString(),
                active = g.activeSelf
            })
            .ToList();
    }
}
