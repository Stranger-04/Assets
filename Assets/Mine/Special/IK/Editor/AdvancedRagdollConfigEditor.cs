using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AdvancedRagdollConfig))]
public class AdvancedRagdollConfigEditor : Editor
{
    private AdvancedRagdollConfig config;
    private Vector2 scrollPosition;

    void OnEnable()
    {
        config = (AdvancedRagdollConfig)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("高级布娃娃配置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        config.autoClassifyBones = EditorGUILayout.Toggle("自动分类骨骼", config.autoClassifyBones);
        
        if (GUILayout.Button("应用高级设置到管理器", GUILayout.Height(30)))
        {
            config.ApplyAdvancedSettings();
            EditorUtility.SetDirty(config);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("骨骼类型设置", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < config.boneTypeSettings.Count; i++)
        {
            DrawBoneTypeSettings(config.boneTypeSettings[i], i);
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加骨骼类型"))
        {
            config.boneTypeSettings.Add(new BoneTypeSettings("New Type"));
            EditorUtility.SetDirty(config);
        }
        if (GUILayout.Button("重置为默认"))
        {
            config.boneTypeSettings.Clear();
            config.InitializeDefaultBoneTypes(); // 重新初始化默认设置
            EditorUtility.SetDirty(config);
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBoneTypeSettings(BoneTypeSettings boneType, int index)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        boneType.boneType = EditorGUILayout.TextField("类型名称", boneType.boneType);
        
        if (boneType.boneType != "Default" && GUILayout.Button("删除", GUILayout.Width(50)))
        {
            config.boneTypeSettings.RemoveAt(index);
            EditorUtility.SetDirty(config);
            return;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 关键词设置
        EditorGUILayout.LabelField("匹配关键词", EditorStyles.miniLabel);
        
        for (int i = 0; i < boneType.boneKeywords.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            boneType.boneKeywords[i] = EditorGUILayout.TextField(boneType.boneKeywords[i]);
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                boneType.boneKeywords.RemoveAt(i);
                EditorUtility.SetDirty(config);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("添加关键词", GUILayout.Width(100)))
        {
            boneType.boneKeywords.Add("");
            EditorUtility.SetDirty(config);
        }

        EditorGUILayout.Space(5);

        // PID设置
        EditorGUILayout.LabelField("PID设置", EditorStyles.miniLabel);
        DrawPIDSettings(boneType.settings);

        EditorGUILayout.EndVertical();
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
}
