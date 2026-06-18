using UnityEngine;

public class Script
{
    public static object Main()
    {
        var rig  = GameObject.Find("CameraRig");
        if (rig == null) return "CameraRig not found";

        rig.transform.SetParent(null);

        return new
        {
            rigParent = rig.transform.parent?.name ?? "null (独立)",
            rigPosition = rig.transform.position.ToString("F2")
        };
    }
}
