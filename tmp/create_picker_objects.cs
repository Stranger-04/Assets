using UnityEngine;

public class Script
{
    public static object Main()
    {
        var shader = Shader.Find("Mine/Chosen/Picker");
        if (shader == null) return "ERROR: Picker shader not found";

        var old = GameObject.Find("PickerTest");
        if (old != null) Object.DestroyImmediate(old);

        var parent = new GameObject("PickerTest");

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
            mat.SetFloat("_DebugScale", 1f); // 生产模式: ÷255, Readback 直接得 ID
            cube.GetComponent<Renderer>().sharedMaterial = mat;
        }

        parent.transform.position = new Vector3(0, 0, 5);

        return "4 test cubes ready (production mode: _DebugScale=1, IDs 1-4).\n"
             + "Click a cube → Picker reads ID → Outline shows yellow border.";
    }
}
