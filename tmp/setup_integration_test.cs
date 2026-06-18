using Mine.Chosen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class Script
{
    public static object Main()
    {
        // 1. 创建测试 Cube（带 Picker shader）
        var shader = Shader.Find("Mine/Chosen/Picker");
        if (shader == null) return "ERROR: Picker shader not found";

        var parent = GameObject.Find("PickerTest");
        if (parent != null) Object.DestroyImmediate(parent);

        parent = new GameObject("PickerTest");

        for (int i = 0; i < 4; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"PickerCube_ID{i + 1}";
            cube.transform.SetParent(parent.transform);
            cube.transform.localPosition = new Vector3(i * 3f - 4.5f, 0, 0);
            cube.transform.localScale = Vector3.one * 1.5f;

            var mat = new Material(shader);
            mat.name = $"PickerMat_ID{i + 1}";
            mat.SetInt("_ObjectID", i + 1);
            mat.SetFloat("_DebugScale", 1f);
            cube.GetComponent<Renderer>().sharedMaterial = mat;
        }

        parent.transform.position = new Vector3(0, 0, 5);

        // 2. 创建 PickerReadback GameObject
        var go = GameObject.Find("PickerReadback");
        if (go != null) Object.DestroyImmediate(go);

        go = new GameObject("PickerReadback");
        go.AddComponent<PickerReadback>();

        // 3. 保存场景
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        return "Integration test ready:\n"
             + "  - 4 test cubes (IDs 1-4, production mode)\n"
             + "  - PickerReadback GameObject\n"
             + "  - Scene SAVED\n"
             + "\nClick a cube → yellow outline should appear!";
    }
}
