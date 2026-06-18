using UnityEngine;
using Mine.CamController;

public class Script
{
    public static object Main()
    {
        var focus = GameObject.Find("FocusPoint");
        if (focus == null) return "FocusPoint not found";

        // 添加 CamController（如果还没有）
        if (focus.GetComponent<CamController>() == null)
            focus.AddComponent<CamController>();

        // 数组件：null 项 = missing script
        int missing = 0;
        foreach (var c in focus.GetComponents<Component>())
            if (c == null) missing++;

        return new
        {
            focusPoint = focus.name,
            parent     = focus.transform.parent?.name ?? "null",
            mode       = focus.transform.parent != null ? "第三人称" : "自由飞行",
            missingScripts = missing,
            added       = "CamController"
        };
    }
}
