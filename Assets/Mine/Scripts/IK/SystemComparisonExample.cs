using UnityEngine;

/// <summary>
/// 主管理器 vs 高级配置 对比示例
/// </summary>
public class SystemComparisonExample : MonoBehaviour
{
    [Header("=== 使用场景对比 ===")]
    
    [Header("1. 仅使用主管理器")]
    [TextArea(5, 10)]
    public string basicManagerUsage = 
        "适用场景：简单项目、少量骨骼、手动精调\n\n" +
        "工作流程：\n" +
        "• 所有骨骼使用统一默认参数\n" +
        "• 需要逐个手动调整每个骨骼\n" +
        "• 适合对特定角色进行精细调试\n" +
        "• 参数完全由开发者控制\n\n" +
        "优点：完全可控、精确调试\n" +
        "缺点：工作量大、容易遗漏、缺乏经验指导";

    [Header("2. 主管理器 + 高级配置")]
    [TextArea(5, 10)]
    public string advancedConfigUsage = 
        "适用场景：复杂角色、多种骨骼类型、批量处理\n\n" +
        "工作流程：\n" +
        "• 高级配置自动识别骨骼类型\n" +
        "• 应用对应类型的最佳实践参数\n" +
        "• 大幅减少手动调参工作\n" +
        "• 基于经验的智能优化\n\n" +
        "优点：高效、智能、基于最佳实践\n" +
        "缺点：可能需要微调、依赖命名规范";

    [Header("=== 参数对比示例 ===")]
    
    [Header("传统方式：所有骨骼相同参数")]
    public PIDSettings uniformSettings = new PIDSettings();
    
    [Header("智能方式：不同类型不同参数")]
    public PIDSettings spineSettings = new PIDSettings { posKp = 150f, rotKp = 150f }; // 脊柱需要强控制
    public PIDSettings headSettings = new PIDSettings { posKp = 200f, rotKp = 200f };  // 头部需要精确控制  
    public PIDSettings armSettings = new PIDSettings { posKp = 120f, rotKp = 120f };   // 手臂平衡控制
    public PIDSettings legSettings = new PIDSettings { posKp = 180f, rotKp = 180f };   // 腿部强力支撑

    [ContextMenu("显示详细对比")]
    public void ShowDetailedComparison()
    {
        Debug.Log("=== 主管理器 vs 高级配置详细对比 ===\n\n" +
                 
                 "【主管理器 ActiveRagdollManager】\n" +
                 "核心职能：基础管理者\n" +
                 "• 检测和连接骨骼\n" +
                 "• 统一参数应用\n" +
                 "• 预设管理\n" +
                 "• 实时控制\n\n" +
                 
                 "【高级配置 AdvancedRagdollConfig】\n" +
                 "核心职能：智能顾问\n" +
                 "• 骨骼类型识别\n" +
                 "• 最佳参数推荐\n" +
                 "• 自动分类优化\n" +
                 "• 经验知识库\n\n" +
                 
                 "【协作关系】\n" +
                 "高级配置 → 提供智能参数 → 主管理器 → 应用到骨骼\n\n" +
                 
                 "【选择建议】\n" +
                 "• 简单项目：只用主管理器\n" +
                 "• 复杂角色：两者配合使用\n" +
                 "• 批量处理：必须用高级配置\n" +
                 "• 学习阶段：先用高级配置了解最佳实践");
    }

    [ContextMenu("模拟智能配置过程")]
    public void SimulateSmartConfiguration()
    {
        // 模拟骨骼名称
        string[] boneNames = { "Spine1", "Head", "LeftArm", "RightLeg", "UnknownBone" };
        
        Debug.Log("=== 模拟智能配置过程 ===\n");
        
        foreach (string boneName in boneNames)
        {
            string boneType = ClassifyBone(boneName);
            PIDSettings recommendedSettings = GetRecommendedSettings(boneType);
            
            Debug.Log($"骨骼: {boneName}\n" +
                     $"  识别类型: {boneType}\n" +
                     $"  推荐参数: Kp={recommendedSettings.posKp}, Kd={recommendedSettings.posKd}\n" +
                     $"  原因: {GetReasonForSettings(boneType)}\n");
        }
    }
    
    private string ClassifyBone(string boneName)
    {
        string lower = boneName.ToLower();
        if (lower.Contains("spine") || lower.Contains("chest")) return "脊柱";
        if (lower.Contains("head") || lower.Contains("neck")) return "头部";  
        if (lower.Contains("arm") || lower.Contains("shoulder")) return "手臂";
        if (lower.Contains("leg") || lower.Contains("thigh")) return "腿部";
        return "默认";
    }
    
    private PIDSettings GetRecommendedSettings(string boneType)
    {
        switch (boneType)
        {
            case "脊柱": return spineSettings;
            case "头部": return headSettings;
            case "手臂": return armSettings; 
            case "腿部": return legSettings;
            default: return uniformSettings;
        }
    }
    
    private string GetReasonForSettings(string boneType)
    {
        switch (boneType)
        {
            case "脊柱": return "脊柱是身体支撑结构，需要强控制力";
            case "头部": return "头部需要精确控制以保持稳定";
            case "手臂": return "手臂需要平衡的控制力和灵活性";
            case "腿部": return "腿部承重较大，需要强力支撑";
            default: return "使用通用平衡参数";
        }
    }
}
