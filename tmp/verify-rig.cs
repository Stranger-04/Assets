using UnityEngine;
using System.Linq;

public class Script
{
    public static object Main()
    {
        var rig = GameObject.Find("CameraRig");
        if (rig == null) return "CameraRig not found";

        var ctrl = rig.GetComponent<Mine.CamController.CameraRigController>();
        var cam  = rig.GetComponentInChildren<Camera>();

        string Traverse(Transform t, int depth = 0)
        {
            var indent = new string(' ', depth * 2);
            var comps  = string.Join(", ", t.GetComponents<Component>().Select(c => c.GetType().Name));
            var result = $"{indent}{t.name} [{comps}]\n";
            for (int i = 0; i < t.childCount; i++)
                result += Traverse(t.GetChild(i), depth + 1);
            return result;
        }

        return new
        {
            hasController = ctrl != null,
            hasCamera     = cam != null,
            hierarchy     = "\n" + Traverse(rig.transform)
        };
    }
}
