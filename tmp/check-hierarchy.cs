using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Script
{
    public static object Main()
    {
        var cube = GameObject.Find("RigidbodyMover_Cube");
        if (cube == null) return "cube not found";
        return Traverse(cube.transform, 0);
    }

    static object Traverse(Transform t, int depth)
    {
        var kids = new List<object>();
        for (int i = 0; i < t.childCount; i++)
            kids.Add(Traverse(t.GetChild(i), depth + 1));
        return new
        {
            name       = t.name,
            depth      = depth,
            localPos   = t.localPosition.ToString("F2"),
            components = string.Join(", ", t.GetComponents<Component>().Select(c => c.GetType().Name)),
            children   = kids
        };
    }
}
