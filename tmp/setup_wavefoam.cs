using UnityEngine;
using UnityEditor;

public class Script
{
    public static object Main()
    {
        // 1. 查找场景中的 Water 物体
        var waterGo = GameObject.Find("Water");
        if (waterGo == null)
        {
            // 尝试在所有 Renderer 中查找使用 Custom/Water shader 的物体
            var renderers = Object.FindObjectsOfType<Renderer>();
            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null && r.sharedMaterial.shader.name == "Custom/Water")
                {
                    waterGo = r.gameObject;
                    break;
                }
            }
        }

        if (waterGo == null)
            return "ERROR: 场景中未找到使用 Custom/Water shader 的物体。请先创建 Water 物体。";

        // 2. 添加 WaveFoamManager 组件
        var manager = waterGo.GetComponent<Mine.Water.WaveFoamManager>();
        if (manager == null)
            manager = waterGo.AddComponent<Mine.Water.WaveFoamManager>();

        // 3. 加载资源并赋值（通过 SerializedObject）
        var so = new SerializedObject(manager);

        var csProp = so.FindProperty("_waveFoamCS");
        if (csProp != null && csProp.objectReferenceValue == null)
        {
            var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Mine/Shaders/WaterTotal/Water/WaveFoam.compute");
            if (cs != null)
            {
                csProp.objectReferenceValue = cs;
            }
            else
            {
                return "ERROR: 未找到 WaveFoam.compute，路径是否正确？";
            }
        }

        var foamProp = so.FindProperty("_foamTex");
        if (foamProp != null && foamProp.objectReferenceValue == null)
        {
            // 从 Water Material 读取 _FoamTex 引用
            var waterMat = waterGo.GetComponent<Renderer>()?.sharedMaterial;
            if (waterMat != null)
            {
                var foamTex = waterMat.GetTexture("_FoamTex");
                if (foamTex != null)
                {
                    foamProp.objectReferenceValue = foamTex;
                }
            }
        }

        so.ApplyModifiedProperties();

        return $"OK: WaveFoamManager 已挂载到 '{waterGo.name}'。" +
               $"\n  ComputeShader: {csProp.objectReferenceValue?.name ?? "null"}" +
               $"\n  FoamTex: {foamProp.objectReferenceValue?.name ?? "null (请手动拖入)"}";
    }
}
