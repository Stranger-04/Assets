using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GrassDatabase", menuName = "Grass/Grass Database", order = 0)]
public class GrassDatabase : ScriptableObject
{
    [SerializeField] private Vector3[] positions;
    // Quaternion stored as (x,y,z,w)
    [SerializeField] private Vector4[] rotations;
    [SerializeField] private Vector3[] normals;
    [SerializeField] private float[] heights;

    public int Count => positions != null ? positions.Length : 0;

    public void FillFromGenerator(GrassGenerator generator)
    {
        if (generator == null || generator.pointProperties == null) return;
        List<GrassGenerator.PointProperties> list = generator.pointProperties;
        int n = list.Count;
        positions = new Vector3[n];
        rotations = new Vector4[n];
        normals = new Vector3[n];
        heights = new float[n];
        for (int i = 0; i < n; i++)
        {
            positions[i] = list[i].position;
            Quaternion q = list[i].rotation;
            rotations[i] = new Vector4(q.x, q.y, q.z, q.w);
            normals[i] = list[i].normal;
            heights[i] = list[i].height;
        }
    }

    public Vector3 GetPosition(int i) => positions[i];
    public Quaternion GetRotation(int i)
    {
        Vector4 v = rotations[i];
        return new Quaternion(v.x, v.y, v.z, v.w);
    }
    public Vector3 GetNormal(int i) => normals[i];
    public float GetHeight(int i) => heights[i];
}
