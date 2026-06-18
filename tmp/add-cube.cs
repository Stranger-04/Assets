using UnityEngine;

public class Script
{
    public static object Main()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube_From_CLI";
        cube.transform.position = new Vector3(0, 1, 0);
        return new
        {
            name = cube.name,
            position = cube.transform.position.ToString(),
            components = cube.GetComponents<Component>().Length,
            meshName = cube.GetComponent<MeshFilter>()?.mesh?.name
        };
    }
}
