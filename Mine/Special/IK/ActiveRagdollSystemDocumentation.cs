using UnityEngine;

/// <summary>
/// Active Ragdoll 主动布娃娃系统使用说明
/// 
/// 这是一个完整的主动布娃娃管理系统，包含以下功能：
/// 
/// 1. PIDBoneFollower - 单个骨骼的PID控制器
/// 2. ActiveRagdollManager - 统一管理所有骨骼的主要脚本
/// 3. AdvancedRagdollConfig - 高级配置，支持按骨骼类型自动设置参数
/// 4. RagdollSetupHelper - 辅助工具，帮助设置物理属性
/// 5. PIDSettings - PID参数配置数据结构
/// 
/// 使用步骤：
/// 
/// 1. 准备工作
///    - 确保你的角色有两套骨骼：动画骨骼（Animator控制）和物理骨骼（带Rigidbody和Collider）
///    - 物理骨骼需要添加PIDBoneFollower组件
/// 
/// 2. 设置管理器
///    - 在场景中创建空物体，添加ActiveRagdollManager脚本
///    - 可选：同时添加AdvancedRagdollConfig脚本以获得更智能的参数配置
///    - 可选：添加RagdollSetupHelper脚本以快速设置物理属性
/// 
/// 3. 配置根节点
///    - 将物理骨骼的根节点拖拽到"物理骨骼根节点"字段
///    - 将动画骨骼的根节点拖拽到"动画骨骼根节点"字段
/// 
/// 4. 自动检测和配置
///    - 点击"自动检测骨骼"按钮，系统会自动：
///      * 找到所有PIDBoneFollower组件
///      * 匹配对应的动画骨骼
///      * 设置目标引用
///      * 应用默认或智能PID参数
/// 
/// 5. 参数调整
///    - 在Inspector中展开每个骨骼的配置
///    - 调整PID参数后点击"应用"按钮
///    - 使用预设功能保存和加载参数配置
/// 
/// 6. 高级功能
///    - 使用AdvancedRagdollConfig可以为不同类型的骨骼设置不同的参数
///    - 系统会根据骨骼名称中的关键词自动分类（如spine、arm、leg等）
///    - 支持自定义骨骼类型和关键词匹配规则
/// 
/// 7. 物理设置辅助
///    - 使用RagdollSetupHelper快速设置所有Rigidbody的基础属性
///    - 可以批量添加关节约束和禁用内部碰撞
/// 
/// 注意事项：
/// - 这些脚本主要在编辑时工作，不会影响运行时性能
/// - 确保物理骨骼和动画骨骼有相同或相似的命名
/// - 建议先用默认参数测试，再根据需要微调
/// - PID参数的调整需要一定经验，建议从小值开始逐渐增大
/// 
/// 参数说明：
/// - Kp (比例): 控制响应强度，值越大反应越强烈
/// - Ki (积分): 控制长期误差修正，通常保持较小值或0
/// - Kd (微分): 控制稳定性，防止震荡
/// - 滤波因子: 平滑输出，减少抖动（0=无滤波，1=完全滤波）
/// </summary>
public class ActiveRagdollSystemDocumentation : MonoBehaviour
{
    [Header("系统文档")]
    [TextArea(10, 20)]
    public string documentation = 
        "Active Ragdoll 主动布娃娃系统\n\n" +
        "这是一个完整的主动布娃娃管理系统，支持：\n" +
        "• 自动检测和配置骨骼\n" +
        "• 智能PID参数分配\n" +
        "• 可视化参数调整界面\n" +
        "• 预设保存和加载\n" +
        "• 按骨骼类型自动优化\n\n" +
        "详细使用说明请查看脚本注释。";
        
    [Header("快速操作")]
    [Space(10)]
    public bool showQuickActions = true;
    
    [ContextMenu("显示使用教程")]
    public void ShowTutorial()
    {
        Debug.Log("=== Active Ragdoll 系统使用教程 ===\n" +
                 "1. 添加 ActiveRagdollManager 到场景\n" +
                 "2. 设置物理骨骼和动画骨骼根节点\n" +
                 "3. 点击'自动检测骨骼'按钮\n" +
                 "4. 在Inspector中调整各骨骼的PID参数\n" +
                 "5. 使用预设功能保存配置\n\n" +
                 "更多详细信息请查看脚本注释！");
    }
}
