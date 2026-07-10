using UnityEngine;

public class Script
{
    public static object Main()
    {
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "ShadowTestPlane";
        plane.transform.position = Vector3.zero;
        plane.transform.localScale = new Vector3(20, 1, 20);

        // 创建几个立方体用于投射阴影
        for (int i = 0; i < 3; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"ShadowCaster_{i}";
            cube.transform.position = new Vector3(-3 + i * 3, 2, 0);
            cube.transform.localScale = new Vector3(1, 2 + i, 1);
        }

        // 确保光源存在
        var light = GameObject.FindObjectOfType<Light>();
        if (light == null)
        {
            var lightGo = new GameObject("Directional Light");
            light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        return $"Plane + {3} cubes created. Light: {light?.name}";
    }
}
