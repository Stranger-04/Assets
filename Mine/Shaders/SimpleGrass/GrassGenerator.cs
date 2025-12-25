using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GrassGenerator : MonoBehaviour
{
    [System.Serializable]
    public class GrassSurface
    {
        public MeshFilter MeshFilter;
        public Transform transform;
    }
    public GrassSurface[] GrassSurfaces;
    public int pointCount = 1024;

    public struct PointProperties
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 normal;
        public float height;
    }
    public List<PointProperties> pointProperties = new List<PointProperties>();
    public GrassDatabase targetDatabase;

    #if UNITY_EDITOR
    [Header("Height Painter")]
    public bool ShowGrass = true;
    public bool DrawGrass = false;
    public float BrushRadius = 0.5f;
    public float BrushStrength = 0.25f;
    public bool Eraser = false;
    #endif

    [ReadOnly]
    [SerializeField]
    public bool Generated = false;

    public void GeneratePoints()
    {
        float[] meshAreas = new float[GrassSurfaces.Length];
        float totalSurfaceArea = 0f;

        // Calculate area for each mesh surface
        for (int i = 0; i < GrassSurfaces.Length; i++)
        {
            Mesh mesh = GrassSurfaces[i].MeshFilter.sharedMesh;
            float area = 0f;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int j = 0; j < triangles.Length; j += 3)
            {
                Vector3 v0 = vertices[triangles[j]];
                Vector3 v1 = vertices[triangles[j + 1]];
                Vector3 v2 = vertices[triangles[j + 2]];

                area += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            }

            meshAreas[i] = area;
            totalSurfaceArea += area;
        }

        // Generate points based on area proportion
        for (int i = 0; i < GrassSurfaces.Length; i++)
        {
            int pointsPerSurface = Mathf.RoundToInt(pointCount * meshAreas[i] / totalSurfaceArea);
            
            Transform transform = GrassSurfaces[i].transform;
            Mesh mesh = GrassSurfaces[i].MeshFilter.sharedMesh;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float[] triangleAreas = new float[triangles.Length / 3];
            float totalArea = 0f;

            // Calculate area for each triangle
            for (int j = 0; j < triangles.Length; j += 3)
            {
                Vector3 v0 = vertices[triangles[j]];
                Vector3 v1 = vertices[triangles[j + 1]];
                Vector3 v2 = vertices[triangles[j + 2]];

                float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                triangleAreas[j / 3] = area;
                totalArea += area;
            }

            // Generate random points on the surface
            for (int j =0; j < pointsPerSurface; j++)
            {
                float randomSample = Random.Range(0f, totalArea);
                float cumulativeArea = 0f;
                int triangleIndex = 0;

                for (int k = 0; k < triangleAreas.Length; k++)
                {
                    cumulativeArea += triangleAreas[k];
                    if (randomSample <= cumulativeArea)
                    {
                        triangleIndex = k * 3;
                        break;
                    }
                }

                Vector3 v0 = vertices[triangles[triangleIndex]];
                Vector3 v1 = vertices[triangles[triangleIndex + 1]];
                Vector3 v2 = vertices[triangles[triangleIndex + 2]];

                float r1 = Random.Range(0f, 1f);
                float r2 = Random.Range(0f, 1f);

                Vector3 randomPoint = (1 - Mathf.Sqrt(r1)) * v0 + (Mathf.Sqrt(r1) * (1 - r2)) * v1 + (Mathf.Sqrt(r1) * r2) * v2;
                randomPoint = transform.TransformPoint(randomPoint);
                Vector3 Normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                Normal = transform.TransformDirection(Normal);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Normal);

                pointProperties.Add(new PointProperties { position = randomPoint, rotation = rotation, normal = Normal, height = 0f });
            }
        }

        Debug.Log("Points Generated");
        Generated = true;
    }

    public void SaveToDatabase()
    {
        targetDatabase.FillFromGenerator(this);
        #if UNITY_EDITOR
        EditorUtility.SetDirty(targetDatabase);
        #endif
        Debug.Log("Grass data saved to database: " + targetDatabase.name);
    }

    public void LoadFromDatabase(bool clearExisting = true)
    {
        if (targetDatabase == null)
        {
            return;
        }
        if (targetDatabase.Count <= 0)
        {
            return;
        }

        if (clearExisting)
            pointProperties.Clear();

        int count = targetDatabase.Count;
        for (int i = 0; i < count; i++)
        {
            PointProperties p = new PointProperties
            {
                position = targetDatabase.GetPosition(i),
                rotation = targetDatabase.GetRotation(i),
                normal = targetDatabase.GetNormal(i),
                height = targetDatabase.GetHeight(i)
            };
            pointProperties.Add(p);
        }

        Generated = true;
        Debug.Log($"Loaded {count} grass points from database '{targetDatabase.name}'.");
    }

    public void CleanPoints()
    {
        pointProperties.Clear();
        Debug.Log("Points Cleaned");
        Generated = false;
    }

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!ShowGrass) return;
        if (pointProperties == null || pointProperties.Count == 0) return;

        const float normalLength = 0.2f;
        Color low = new Color(0.05f, 0.30f, 0.05f);
        Color high = new Color(0.60f, 1.00f, 0.60f);

        for (int i = 0; i < pointProperties.Count; i++)
        {
            var p = pointProperties[i];
            Vector3 normalDir = p.normal;
            float t = Mathf.Clamp01(p.height);
            Gizmos.color = Color.Lerp(low, high, t);
            Gizmos.DrawLine(p.position, p.position + normalDir * normalLength * p.height);
        }
    }
    #endif

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSceneGUI(SceneView sv)
    {
        if (!DrawGrass || pointProperties == null || pointProperties.Count == 0) return;

        Event e = Event.current;

        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hoverHit, 10000f))
        {
            Handles.color = new Color(1f, 0.25f, 0.1f, 0.8f);
            Handles.DrawWireDisc(hoverHit.point, hoverHit.normal, BrushRadius);
        }


        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
            {
                PaintAtWorldPoint(hit.point, BrushRadius, Eraser ? -BrushStrength : BrushStrength);
                e.Use();
                SceneView.RepaintAll();
            }
        }
    }

    void PaintAtWorldPoint(Vector3 center, float radius, float strength)
    {
        if (radius <= 0f || Mathf.Approximately(strength, 0f)) return;

        for (int i = 0; i < pointProperties.Count; i++)
        {
            var p = pointProperties[i];
            float d = Vector3.Distance(center, p.position);
            if (d > radius) continue;
            float u = 1f - (d / radius);
            float w = u * u * (3f - 2f * u);
            p.height = Mathf.Clamp01(p.height + strength * w);
            pointProperties[i] = p;
        }
    }
}