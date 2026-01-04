using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

[Serializable]
public class PreparationRendererData : ScriptableRendererData
{
#if UNITY_EDITOR
    [MenuItem("Assets/Create/Rendering/Preparation Renderer Data")]
    public static void CreatePreparationRendererData()
    {
        PreparationRendererData instance = ScriptableObject.CreateInstance<PreparationRendererData>();
        ProjectWindowUtil.CreateAsset(instance, "PreparationRendererData.asset");
    }
#endif

    [SerializeField] LayerMask m_OpaqueLayerMask = -1;
    [SerializeField] LayerMask m_TransparentLayerMask = -1;
    [SerializeField] bool m_CopyDepth = true;
    [SerializeField] bool m_CopyColor = false;
    [SerializeField] bool m_CopyNormal = false;
    [SerializeField] bool m_TransparentMode = false;

    public LayerMask opaqueLayerMask
    {
        get { return m_OpaqueLayerMask; }
        set { m_OpaqueLayerMask = value; }
    }
    public LayerMask transparentLayerMask
    {
        get { return m_TransparentLayerMask; }
        set { m_TransparentLayerMask = value; }
    }
    public bool copyDepth
    {
        get { return m_CopyDepth; }
        set { m_CopyDepth = value; }
    }
    public bool copyColor
    {
        get { return m_CopyColor; }
        set { m_CopyColor = value; }
    }
    public bool copyNormal
    {
        get { return m_CopyNormal; }
        set { m_CopyNormal = value; }
    }
    public bool transparentMode
    {
        get { return m_TransparentMode; }
        set { m_TransparentMode = value; }
    }

    protected override ScriptableRenderer Create()
    {
        return new PreparationRenderer(this);
    }
}

