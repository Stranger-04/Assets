using UnityEngine;
using UnityEditor;

public class Script
{
    public static object Main()
    {
        var focus = GameObject.Find("FocusPoint");
        if (focus == null) return "FocusPoint not found";

        int before = focus.GetComponents<Component>().Length;
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(focus);
        int after  = focus.GetComponents<Component>().Length;

        return new
        {
            obj = focus.name,
            componentsBefore = before,
            componentsAfter  = after,
            removed = before - after
        };
    }
}
