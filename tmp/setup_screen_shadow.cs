using UnityEngine;
using UnityEditor;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();

        // 1. 加载 Shader
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Mine/Shaders/ScreenShadowDemo/ScreenShadowDemo.shader");
        if (shader == null)
            return "ERROR: Shader not found at Assets/Mine/Shaders/ScreenShadowDemo/ScreenShadowDemo.shader";
        sb.AppendLine($"Shader loaded: {shader.name}");

        // 2. 创建材质
        var matPath = "Assets/Mine/Shaders/ScreenShadowDemo/ScreenShadowDemo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = "ScreenShadowDemo";
            mat.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            mat.SetFloat("_ShadowIntensity", 0.6f);
            AssetDatabase.CreateAsset(mat, matPath);
            sb.AppendLine($"Material created: {matPath}");
        }
        else
        {
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            sb.AppendLine($"Material updated: {matPath}");
        }

        // 3. 创建测试父容器
        var parentName = "ScreenShadowTest";
        var parent = GameObject.Find(parentName);
        if (parent == null)
            parent = new GameObject(parentName);

        // 4. 创建地面（接受阴影）
        var ground = GameObject.Find("ScreenShadow_Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ScreenShadow_Ground";
            ground.transform.SetParent(parent.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5, 1, 5);
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.material = mat;
            sb.AppendLine("Ground plane created (receiver)");
        }

        // 5. 创建投射阴影的物体（立方体+球体）
        var casterMatPath = "Assets/Mine/Shaders/ScreenShadowDemo/ScreenShadowDemo_Caster.mat";
        var casterMat = AssetDatabase.LoadAssetAtPath<Material>(casterMatPath);
        if (casterMat == null)
        {
            casterMat = new Material(shader);
            casterMat.name = "ScreenShadowDemo_Caster";
            casterMat.color = new Color(0.95f, 0.6f, 0.3f, 1f);
            casterMat.SetFloat("_ShadowIntensity", 0.6f);
            AssetDatabase.CreateAsset(casterMat, casterMatPath);
        }

        // 立方体
        var cube = GameObject.Find("ScreenShadow_Cube");
        if (cube == null)
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "ScreenShadow_Cube";
            cube.transform.SetParent(parent.transform);
            cube.transform.position = new Vector3(0, 1.5f, 2);
            cube.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            cube.GetComponent<MeshRenderer>().material = casterMat;
            sb.AppendLine("Cube created (caster)");
        }

        // 球体
        var sphere = GameObject.Find("ScreenShadow_Sphere");
        if (sphere == null)
        {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ScreenShadow_Sphere";
            sphere.transform.SetParent(parent.transform);
            sphere.transform.position = new Vector3(2.5f, 1, 1);
            sphere.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            sphere.GetComponent<MeshRenderer>().material = casterMat;
            sb.AppendLine("Sphere created (caster)");
        }

        // 6. 确保有方向光
        var light = GameObject.Find("Directional Light");
        if (light == null)
        {
            light = new GameObject("Directional Light");
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            lightComp.shadows = LightShadows.Soft;
            lightComp.shadowStrength = 0.8f;
            lightComp.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            sb.AppendLine("Directional Light created");
        }
        else
        {
            var lightComp = light.GetComponent<Light>();
            lightComp.shadows = LightShadows.Soft;
            lightComp.shadowStrength = 0.8f;
            sb.AppendLine("Directional Light shadow enabled");
        }

        // 7. 相机位置调整
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(5, 4, -3);
            cam.transform.LookAt(new Vector3(1, 1, 1));
            sb.AppendLine("Camera positioned for shadow view");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        sb.Insert(0, "=== ScreenShadowDemo Setup Complete ===\n");
        return sb.ToString();
    }
}
