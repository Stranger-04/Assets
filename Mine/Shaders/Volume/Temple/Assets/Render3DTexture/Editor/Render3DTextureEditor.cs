using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(Render3DTexture))]
public class Render3DTextureEditor : Editor {
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if(GUILayout.Button("RenderTexture"))
        {
            ((Render3DTexture)target).StartRender();
        }
    }
}
