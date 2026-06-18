using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class Script
{
    public static object Main()
    {
        var scenes = new List<object>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            var rootObjects = scene.GetRootGameObjects();
            var roots = new List<object>();
            foreach (var go in rootObjects)
            {
                roots.Add(TraverseGameObject(go));
            }
            scenes.Add(new
            {
                name = scene.name,
                path = scene.path,
                isLoaded = scene.isLoaded,
                rootCount = rootObjects.Length,
                roots = roots
            });
        }
        return scenes;
    }

    static object TraverseGameObject(GameObject go)
    {
        var children = new List<object>();
        for (int i = 0; i < go.transform.childCount; i++)
        {
            children.Add(TraverseGameObject(go.transform.GetChild(i).gameObject));
        }

        var components = new List<string>();
        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp != null)
                components.Add(comp.GetType().Name);
        }

        return new
        {
            name = go.name,
            active = go.activeSelf,
            tag = go.tag,
            layer = LayerMask.LayerToName(go.layer),
            components = components,
            children = children
        };
    }
}
