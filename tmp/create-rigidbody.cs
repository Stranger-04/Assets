using UnityEngine;
using Mine.CamController;

public class Script
{
    public static object Main()
    {
        var cam = Camera.main;
        var camPos = cam != null ? cam.transform.position : Vector3.zero;
        var spawnPos = camPos + cam.transform.forward * 4f + Vector3.up * 0.5f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "RigidbodyMover_Cube";

        var rb = body.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDrag = 1f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        var mover = body.AddComponent<RigidbodyMover>();
        // CameraTransform will auto-resolve to Camera.main in Awake

        body.transform.position = spawnPos;
        body.transform.localScale = Vector3.one * 0.5f;

        // Give it a distinct material color if possible
        var renderer = body.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            var mat = new Material(renderer.sharedMaterial);
            mat.color = new Color(1f, 0.5f, 0.1f);
            renderer.sharedMaterial = mat;
        }

        return new
        {
            name = body.name,
            position = body.transform.position.ToString("F2"),
            hasRigidbody = rb != null,
            mass = rb.mass,
            hasMover = mover != null,
            cameraRef = "Camera.main (auto-resolved)"
        };
    }
}
