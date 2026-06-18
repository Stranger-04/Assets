using UnityEngine;
using System.Linq;

public class Script
{
    public static object Main()
    {
        var cam = Camera.main;
        if (cam == null) return "no main camera";
        return new {
            name = cam.name,
            tag = cam.tag,
            pos = cam.transform.position.ToString(),
            rot = cam.transform.rotation.eulerAngles.ToString(),
            parent = cam.transform.parent?.name ?? "none"
        };
    }
}
