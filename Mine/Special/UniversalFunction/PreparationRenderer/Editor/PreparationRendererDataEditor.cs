#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;

[CustomEditor(typeof(PreparationRendererData), true)]
public class PreparationRendererDataEditor : ScriptableRendererDataEditor
{
    SerializedProperty m_OpaqueLayerMask;
    SerializedProperty m_TransparentLayerMask;
    SerializedProperty m_CopyDepth;
    SerializedProperty m_CopyColor;
    SerializedProperty m_CopyNormal;
    SerializedProperty m_TransparentMode;

    private void OnEnable()
    {
        m_OpaqueLayerMask       = serializedObject.FindProperty("m_OpaqueLayerMask");
        m_TransparentLayerMask  = serializedObject.FindProperty("m_TransparentLayerMask");

        m_CopyDepth       = serializedObject.FindProperty("m_CopyDepth");
        m_CopyColor       = serializedObject.FindProperty("m_CopyColor");
        m_CopyNormal      = serializedObject.FindProperty("m_CopyNormal");
        m_TransparentMode = serializedObject.FindProperty("m_TransparentMode");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.LabelField("Preparation Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_OpaqueLayerMask);
        EditorGUILayout.PropertyField(m_TransparentLayerMask);

        EditorGUILayout.PropertyField(m_CopyDepth);
        EditorGUILayout.PropertyField(m_CopyColor);
        EditorGUILayout.PropertyField(m_CopyNormal);
        EditorGUILayout.PropertyField(m_TransparentMode);

        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        base.OnInspectorGUI();

    }
}
#endif