using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ActiveRagdollManager : MonoBehaviour
{
    [Header("物理骨骼根节点")]
    public Transform physicalRagdollRoot;
    
    [Header("动画骨骼根节点")]
    public Transform animationSkeletonRoot;

    [Header("默认PID设置")]
    public PIDSettings defaultSettings = new PIDSettings();

    [Header("骨骼配置")]
    public List<BonePIDConfig> boneConfigs = new List<BonePIDConfig>();

    [Header("预设")]
    public List<PIDPreset> presets = new List<PIDPreset>();

    void Awake()
    {
        // 运行时不执行自动检测，只在编辑器中使用
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            AutoDetectBones();
        }
        #endif
    }

    [ContextMenu("自动检测骨骼")]
    public void AutoDetectBones()
    {
        if (physicalRagdollRoot == null || animationSkeletonRoot == null)
        {
            Debug.LogWarning("请先设置物理骨骼根节点和动画骨骼根节点");
            return;
        }

        boneConfigs.Clear();

        // 获取高级配置组件
        AdvancedRagdollConfig advancedConfig = GetComponent<AdvancedRagdollConfig>();

        // 获取所有物理骨骼
        PIDBoneFollower[] physicalBones = physicalRagdollRoot.GetComponentsInChildren<PIDBoneFollower>();
        
        foreach (var physicalBone in physicalBones)
        {
            string boneName = physicalBone.name;
            Transform animationBone = FindAnimationBone(boneName);
            
            if (animationBone != null)
            {
                BonePIDConfig config = new BonePIDConfig(boneName, physicalBone, animationBone);
                
                // 如果有高级配置，使用高级配置的设置，否则使用默认设置
                if (advancedConfig != null)
                {
                    config.settings = advancedConfig.GetSettingsForBone(boneName);
                    Debug.Log($"找到骨骼配对: {boneName} (类型: {advancedConfig.GetBoneType(boneName)})");
                }
                else
                {
                    config.settings = new PIDSettings(defaultSettings);
                    Debug.Log($"找到骨骼配对: {boneName} (使用默认设置)");
                }
                
                boneConfigs.Add(config);
                
                // 设置目标骨骼
                physicalBone.target = animationBone;
                physicalBone.BoneName = boneName;
            }
            else
            {
                Debug.LogWarning($"未找到对应的动画骨骼: {boneName}");
            }
        }

        ApplyAllSettings();
    }

    private Transform FindAnimationBone(string boneName)
    {
        // 递归查找同名骨骼
        return FindBoneRecursive(animationSkeletonRoot, boneName);
    }

    private Transform FindBoneRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindBoneRecursive(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    [ContextMenu("应用所有设置")]
    public void ApplyAllSettings()
    {
        foreach (var config in boneConfigs)
        {
            if (config.physicalBone != null)
            {
                ApplySettings(config);
            }
        }
    }

    public void ApplySettings(BonePIDConfig config)
    {
        if (config.physicalBone != null && config.settings != null)
        {
            config.physicalBone.UpdatePIDParameters(
                config.settings.posKp,
                config.settings.posKi,
                config.settings.posKd,
                config.settings.posLowPassFactor,
                config.settings.rotKp,
                config.settings.rotKi,
                config.settings.rotKd,
                config.settings.rotLowPassFactor
            );
        }
    }

    public void LoadPreset(PIDPreset preset)
    {
        if (preset == null) return;

        foreach (var config in boneConfigs)
        {
            var presetConfig = preset.boneSettings.FirstOrDefault(b => b.boneName == config.boneName);
            if (presetConfig != null)
            {
                config.settings = new PIDSettings(presetConfig.settings);
            }
        }

        ApplyAllSettings();
    }

    public void SaveAsPreset(string presetName)
    {
        PIDPreset newPreset = new PIDPreset
        {
            presetName = presetName,
            boneSettings = new List<BonePIDConfig>()
        };

        foreach (var config in boneConfigs)
        {
            newPreset.boneSettings.Add(new BonePIDConfig(config.boneName, null, null)
            {
                settings = new PIDSettings(config.settings)
            });
        }

        presets.Add(newPreset);
    }
}

[System.Serializable]
public class PIDPreset
{
    public string presetName;
    public List<BonePIDConfig> boneSettings = new List<BonePIDConfig>();
}
