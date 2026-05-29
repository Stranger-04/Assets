using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(ActiveRagdollManager))]
public class ActiveRagdollManagerEditor : Editor
{
    private ActiveRagdollManager manager;
    private string newPresetName = "新预设";
    private Vector2 scrollPosition;

    void OnEnable()
    {
        manager = (ActiveRagdollManager)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 基础设置
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("基础设置", EditorStyles.boldLabel);
        
        manager.physicalRagdollRoot = (Transform)EditorGUILayout.ObjectField("物理骨骼根节点", 
            manager.physicalRagdollRoot, typeof(Transform), true);
        
        manager.animationSkeletonRoot = (Transform)EditorGUILayout.ObjectField("动画骨骼根节点", 
            manager.animationSkeletonRoot, typeof(Transform), true);

        EditorGUILayout.Space();

        // 默认设置
        EditorGUILayout.LabelField("默认PID设置", EditorStyles.boldLabel);
        DrawPIDSettings(manager.defaultSettings);

        EditorGUILayout.Space();

        // 控制按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("自动检测骨骼", GUILayout.Height(30)))
        {
            manager.AutoDetectBones();
            EditorUtility.SetDirty(manager);
        }
        if (GUILayout.Button("应用所有设置", GUILayout.Height(30)))
        {
            manager.ApplyAllSettings();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 预设管理
        DrawPresetSection();

        EditorGUILayout.Space();

        // 骨骼配置
        if (manager.boneConfigs != null && manager.boneConfigs.Count > 0)
        {
            EditorGUILayout.LabelField($"骨骼配置 ({manager.boneConfigs.Count})", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(400));
            
            for (int i = 0; i < manager.boneConfigs.Count; i++)
            {
                DrawBoneConfig(manager.boneConfigs[i], i);
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndScrollView();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPIDSettings(PIDSettings settings)
    {
        EditorGUI.indentLevel++;
        
        EditorGUILayout.LabelField("位置控制", EditorStyles.miniLabel);
        settings.posKp = EditorGUILayout.FloatField("Kp", settings.posKp);
        settings.posKi = EditorGUILayout.FloatField("Ki", settings.posKi);
        settings.posKd = EditorGUILayout.FloatField("Kd", settings.posKd);
        settings.posLowPassFactor = EditorGUILayout.Slider("滤波因子", settings.posLowPassFactor, 0f, 1f);
        
        EditorGUILayout.Space(3);
        
        EditorGUILayout.LabelField("旋转控制", EditorStyles.miniLabel);
        settings.rotKp = EditorGUILayout.FloatField("Kp", settings.rotKp);
        settings.rotKi = EditorGUILayout.FloatField("Ki", settings.rotKi);
        settings.rotKd = EditorGUILayout.FloatField("Kd", settings.rotKd);
        settings.rotLowPassFactor = EditorGUILayout.Slider("滤波因子", settings.rotLowPassFactor, 0f, 1f);
        
        EditorGUI.indentLevel--;
    }

    private void DrawBoneConfig(BonePIDConfig config, int index)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.BeginHorizontal();
        
        string displayName = config.boneName;
        if (config.physicalBone == null) displayName += " (缺失物理骨骼)";
        if (config.animationBone == null) displayName += " (缺失动画骨骼)";
        
        config.isExpanded = EditorGUILayout.Foldout(config.isExpanded, displayName, true);
        
        if (GUILayout.Button("应用", GUILayout.Width(50)))
        {
            manager.ApplySettings(config);
        }
        
        if (GUILayout.Button("重置", GUILayout.Width(50)))
        {
            config.settings = new PIDSettings(manager.defaultSettings);
            manager.ApplySettings(config);
            EditorUtility.SetDirty(manager);
        }
        
        EditorGUILayout.EndHorizontal();

        if (config.isExpanded)
        {
            EditorGUI.indentLevel++;
            
            config.physicalBone = (PIDBoneFollower)EditorGUILayout.ObjectField("物理骨骼", 
                config.physicalBone, typeof(PIDBoneFollower), true);
            
            config.animationBone = (Transform)EditorGUILayout.ObjectField("动画骨骼", 
                config.animationBone, typeof(Transform), true);
            
            EditorGUILayout.Space(5);
            DrawPIDSettings(config.settings);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawPresetSection()
    {
        EditorGUILayout.LabelField("预设管理", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        newPresetName = EditorGUILayout.TextField("预设名称", newPresetName);
        if (GUILayout.Button("保存预设", GUILayout.Width(80)))
        {
            manager.SaveAsPreset(newPresetName);
            EditorUtility.SetDirty(manager);
        }
        EditorGUILayout.EndHorizontal();

        if (manager.presets != null && manager.presets.Count > 0)
        {
            EditorGUILayout.LabelField("已保存的预设:", EditorStyles.miniLabel);
            foreach (var preset in manager.presets.ToList())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(preset.presetName, GUILayout.ExpandWidth(true));
                
                if (GUILayout.Button("加载", GUILayout.Width(50)))
                {
                    manager.LoadPreset(preset);
                    EditorUtility.SetDirty(manager);
                }
                
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    manager.presets.Remove(preset);
                    EditorUtility.SetDirty(manager);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
