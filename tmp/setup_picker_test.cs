using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mine.Picker;

public class Script
{
    public static object Main()
    {
        var shader = Shader.Find("Mine/Picker/Picker");
        if (shader == null) return "ERROR: Picker shader not found. Check shader name.";

        // 清理旧测试物体
        var old = GameObject.Find("PickerTest");
        if (old != null) Object.DestroyImmediate(old);

        var oldRB = GameObject.Find("PickerReadback");
        if (oldRB != null) Object.DestroyImmediate(oldRB);

        // ── 父节点 ──────────────────────────────────────────────
        var parent = new GameObject("PickerTest");
        parent.transform.position = new Vector3(0, 0, 6);

        // ── 测试单位 ────────────────────────────────────────────
        int id = 1;
        var cam = Camera.main;
        var camPos = cam != null ? cam.transform.position : Vector3.zero;
        var camForward = cam != null ? cam.transform.forward : Vector3.forward;

        // Cube 矩阵: 3×3 网格
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"PickerCube_ID{id:D2}";
                cube.transform.SetParent(parent.transform);
                cube.transform.localPosition = new Vector3(col * 2.5f - 2.5f, row * 2.5f - 2.5f, 0);
                cube.transform.localScale = Vector3.one * 1.2f;

                var mat = new Material(shader);
                mat.SetInt("_ObjectID", id);
                cube.GetComponent<Renderer>().sharedMaterial = mat;
                id++;
            }
        }

        // Sphere 组: 2 个球体
        for (int i = 0; i < 2; i++)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"PickerSphere_ID{id:D2}";
            sphere.transform.SetParent(parent.transform);
            sphere.transform.localPosition = new Vector3(i * 3f - 1.5f, -4.5f, 0);
            sphere.transform.localScale = Vector3.one * 0.9f;

            var mat = new Material(shader);
            mat.SetInt("_ObjectID", id);
            sphere.GetComponent<Renderer>().sharedMaterial = mat;
            id++;
        }

        // Cylinder 组: 2 个圆柱
        for (int i = 0; i < 2; i++)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = $"PickerCylinder_ID{id:D2}";
            cyl.transform.SetParent(parent.transform);
            cyl.transform.localPosition = new Vector3(i * 3f - 1.5f, 4.5f, 0);
            cyl.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);

            var mat = new Material(shader);
            mat.SetInt("_ObjectID", id);
            cyl.GetComponent<Renderer>().sharedMaterial = mat;
            id++;
        }

        // ── PickerReadback GameObject ───────────────────────────
        var rbGO = new GameObject("PickerReadback");
        rbGO.AddComponent<PickerReadback>();

        // ── 保存场景 ────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        return $"Created {id - 1} test units:\n"
             + "  9× Cube    (ID 01–09, 3×3 grid)\n"
             + "  2× Sphere  (ID 10–11, bottom row)\n"
             + "  2× Cylinder (ID 12–13, top row)\n"
             + "  PickerReadback GameObject\n"
             + "  Scene saved.";
    }
}
