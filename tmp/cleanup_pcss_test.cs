using UnityEngine;

public class Script
{
    public static object Main()
    {
        var objs = new[] { "ShadowTestPlane", "ShadowCaster_0", "ShadowCaster_1", "ShadowCaster_2" };
        int removed = 0;
        foreach (var name in objs)
        {
            var go = GameObject.Find(name);
            if (go != null) { GameObject.DestroyImmediate(go); removed++; }
        }
        return $"Removed {removed} test objects";
    }
}
