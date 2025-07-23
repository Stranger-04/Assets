using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class BoneTypeSettings
{
    public string boneType;
    public PIDSettings settings;
    public List<string> boneKeywords = new List<string>();
    
    public BoneTypeSettings(string type)
    {
        boneType = type;
        settings = new PIDSettings();
    }
}

public class AdvancedRagdollConfig : MonoBehaviour
{
    [Header("骨骼类型设置")]
    public List<BoneTypeSettings> boneTypeSettings = new List<BoneTypeSettings>();
    
    [Header("自动分类规则")]
    public bool autoClassifyBones = true;
    
    void Awake()
    {
        InitializeDefaultBoneTypes();
    }
    
    public void InitializeDefaultBoneTypes()
    {
        if (boneTypeSettings.Count == 0)
        {
            // 脊柱骨骼设置 - 需要更强的控制
            var spineSettings = new BoneTypeSettings("Spine");
            spineSettings.settings.posKp = 150f;
            spineSettings.settings.posKd = 15f;
            spineSettings.settings.rotKp = 150f;
            spineSettings.settings.rotKd = 15f;
            spineSettings.boneKeywords.AddRange(new[] { "spine", "chest", "pelvis", "hip" });
            boneTypeSettings.Add(spineSettings);
            
            // 头部设置 - 需要精确控制
            var headSettings = new BoneTypeSettings("Head");
            headSettings.settings.posKp = 200f;
            headSettings.settings.posKd = 20f;
            headSettings.settings.rotKp = 200f;
            headSettings.settings.rotKd = 20f;
            headSettings.boneKeywords.AddRange(new[] { "head", "neck" });
            boneTypeSettings.Add(headSettings);
            
            // 手臂设置 - 平衡控制
            var armSettings = new BoneTypeSettings("Arms");
            armSettings.settings.posKp = 120f;
            armSettings.settings.posKd = 12f;
            armSettings.settings.rotKp = 120f;
            armSettings.settings.rotKd = 12f;
            armSettings.boneKeywords.AddRange(new[] { "arm", "shoulder", "elbow", "hand", "finger" });
            boneTypeSettings.Add(armSettings);
            
            // 腿部设置 - 强力支撑
            var legSettings = new BoneTypeSettings("Legs");
            legSettings.settings.posKp = 180f;
            legSettings.settings.posKd = 18f;
            legSettings.settings.rotKp = 180f;
            legSettings.settings.rotKd = 18f;
            legSettings.boneKeywords.AddRange(new[] { "leg", "thigh", "knee", "foot", "toe" });
            boneTypeSettings.Add(legSettings);
            
            // 默认设置
            var defaultSettings = new BoneTypeSettings("Default");
            defaultSettings.settings.posKp = 100f;
            defaultSettings.settings.posKd = 10f;
            defaultSettings.settings.rotKp = 100f;
            defaultSettings.settings.rotKd = 10f;
            boneTypeSettings.Add(defaultSettings);
        }
    }
    
    public PIDSettings GetSettingsForBone(string boneName)
    {
        if (!autoClassifyBones)
            return GetDefaultSettings();
            
        string lowerBoneName = boneName.ToLower();
        
        // 查找匹配的骨骼类型
        foreach (var boneType in boneTypeSettings)
        {
            if (boneType.boneType == "Default") continue;
            
            foreach (var keyword in boneType.boneKeywords)
            {
                if (lowerBoneName.Contains(keyword.ToLower()))
                {
                    return new PIDSettings(boneType.settings);
                }
            }
        }
        
        // 如果没找到匹配的，返回默认设置
        return GetDefaultSettings();
    }
    
    private PIDSettings GetDefaultSettings()
    {
        var defaultType = boneTypeSettings.Find(t => t.boneType == "Default");
        return defaultType != null ? new PIDSettings(defaultType.settings) : new PIDSettings();
    }
    
    [ContextMenu("应用高级设置到管理器")]
    public void ApplyAdvancedSettings()
    {
        ActiveRagdollManager manager = GetComponent<ActiveRagdollManager>();
        if (manager == null)
        {
            Debug.LogError("未找到ActiveRagdollManager组件");
            return;
        }
        
        foreach (var config in manager.boneConfigs)
        {
            PIDSettings newSettings = GetSettingsForBone(config.boneName);
            config.settings = newSettings;
            Debug.Log($"为骨骼 {config.boneName} 应用了 {GetBoneType(config.boneName)} 类型的设置");
        }
        
        manager.ApplyAllSettings();
    }
    
    public string GetBoneType(string boneName)
    {
        string lowerBoneName = boneName.ToLower();
        
        foreach (var boneType in boneTypeSettings)
        {
            if (boneType.boneType == "Default") continue;
            
            foreach (var keyword in boneType.boneKeywords)
            {
                if (lowerBoneName.Contains(keyword.ToLower()))
                {
                    return boneType.boneType;
                }
            }
        }
        
        return "Default";
    }
}
