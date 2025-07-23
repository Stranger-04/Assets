# 物理传送门系统使用说明

## 概述
这套物理传送门系统专注于传送功能，不涉及视觉效果。系统包含两个主要组件：
- `PhysicalPortal` - 传送门本体
- `PhysicalPortalTraveller` - 可被传送的物体

## 快速设置

### 1. 设置传送门
1. 创建两个空的 GameObject 作为传送门
2. 为每个传送门添加 `PhysicalPortal` 脚本
3. 为每个传送门添加 Collider（设置为 Trigger）
4. 在 PhysicalPortal 的 `Target Portal` 字段中互相引用对方

### 2. 设置可传送物体
1. 为要传送的物体添加 `PhysicalPortalTraveller` 脚本
2. 确保物体有 Collider 组件
3. 如果需要保持速度，确保物体有 Rigidbody 组件

## 主要特性

### PhysicalPortal 功能：
- **即时传送**：物体接触传送门立即传送
- **位置转换**：保持相对于传送门的位置关系
- **方向转换**：保持相对于传送门的旋转关系
- **速度转换**：保持相对于传送门的速度方向
- **防重复传送**：避免物体在传送门之间反复弹跳
- **调试可视化**：Scene 视图中显示传送门方向和连接线

### PhysicalPortalTraveller 功能：
- **传送冷却**：防止传送抖动
- **速度保持**：可选择是否保持物体的速度
- **传送状态管理**：追踪传送状态，防止重复传送
- **可扩展事件**：提供传送前后的虚方法供继承
- **调试信息**：运行时显示传送状态

## 使用示例

### 基础使用
```csharp
// 检查物体是否可以被传送
PhysicalPortalTraveller traveller = GetComponent<PhysicalPortalTraveller>();
if (traveller.CanBeTeleported())
{
    // 物体可以被传送
}

// 动态控制传送能力
traveller.SetCanBeTeleported(false); // 禁用传送
traveller.SetCanBeTeleported(true);  // 启用传送

// 重置传送冷却
traveller.ResetTeleportCooldown();
```

### 扩展传送效果
继承 `PhysicalPortalTraveller` 并重写虚方法：
```csharp
public class MyPortalTraveller : PhysicalPortalTraveller
{
    protected override void OnBeforeTeleport()
    {
        base.OnBeforeTeleport();
        // 添加传送前的特效、音效等
    }
    
    protected override void OnAfterTeleport()
    {
        base.OnAfterTeleport();
        // 添加传送后的特效、音效等
    }
}
```

## 配置参数

### PhysicalPortal 参数：
- `Target Portal`: 目标传送门
- `Enable Debug Log`: 启用调试日志

### PhysicalPortalTraveller 参数：
- `Can Be Teleported`: 是否可以被传送
- `Preserve Velocity`: 是否保持速度
- `Teleport Cooldown`: 传送冷却时间（秒）
- `Enable Debug Log`: 启用调试日志

## 注意事项

1. **Collider 要求**：传送门必须有 Trigger Collider，可传送物体必须有 Collider
2. **Rigidbody 推荐**：如果需要保持速度，物体应该有 Rigidbody 组件
3. **传送门方向**：传送门的 forward 方向决定传送的正面方向
4. **性能考虑**：大量物体频繁传送时，可以调整冷却时间来优化性能
5. **层级管理**：建议将传送门和可传送物体放在不同的层级上便于管理

## 调试技巧

1. 启用 `Enable Debug Log` 可以在控制台看到详细的传送信息
2. Scene 视图中可以看到传送门的方向箭头和连接线
3. 运行时在 Game 视图中可以看到物体的传送状态（需要启用调试）
4. 使用 `GetTimeSinceLastTeleport()` 检查物体的传送冷却状态

## 常见问题

**Q: 物体传送后立即反向传送怎么办？**
A: 系统有防重复传送机制，如果仍有问题，可以增加 `teleportCooldown` 值。

**Q: 如何让某些物体不能被传送？**
A: 不添加 `PhysicalPortalTraveller` 组件，或者设置 `canBeTeleported = false`。

**Q: 速度方向不对怎么办？**
A: 检查传送门的朝向（forward 方向），确保两个传送门的相对朝向正确。

**Q: 如何添加传送特效？**
A: 继承 `PhysicalPortalTraveller` 并重写 `OnBeforeTeleport()` 和 `OnAfterTeleport()` 方法。
