using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GrassGenerator))]
public class GrassGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GrassGenerator grassGenerator = (GrassGenerator)target;
        if (GUILayout.Button("Load From Database"))
        {
            grassGenerator.LoadFromDatabase(true);
        }
        if (GUILayout.Button("Generate Points"))
        {
            grassGenerator.GeneratePoints();
        }
        if (GUILayout.Button("Clean Points"))
        {
            grassGenerator.CleanPoints();
        }
        if (GUILayout.Button("Save To Database"))
        {
            grassGenerator.SaveToDatabase();
        }
    }
}
