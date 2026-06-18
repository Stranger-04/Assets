# CameraRigController — 相机控制系统

> 记录日期: 2026-06-15
> 参考: [ShaderStructure.md](../../../MarkDowns/ShaderStructure.md) 代码注释风格

---

## 概述

基于双层级空物体旋转的 FPS 风格相机控制器。将旋转拆分为 Yaw（水平）和 Pitch（垂直）两个独立层级，配合 WASD 局部空间移动，提供无漂移、可扩展的相机操控。

## 层级结构

```
CameraRig (root, 挂载 CameraRigController)
└── YawPivot (空物体)
    └── PitchPivot (空物体)
        └── Main Camera
```

| 层级 | 职责 | 旋转轴 | 旋转空间 |
|------|------|--------|----------|
| YawPivot | 水平旋转 | 世界 Y 轴 | `rotation`（全局） |
| PitchPivot | 垂直旋转 | 局部 X 轴 | `localRotation`（相对父节点） |

## 参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `_yawPivot` | Transform | 自动解析 | 第一层水平旋转节点 |
| `_pitchPivot` | Transform | 自动解析 | 第二层垂直旋转节点 |
| `_mouseSensitivity` | float | 2 | 鼠标灵敏度倍率 |
| `_pitchMin` | float | -89 | 俯角下限（度） |
| `_pitchMax` | float | 89 | 仰角上限（度） |
| `_moveSpeed` | float | 5 | WASD 移动速度（单位/秒） |
| `_lockCursorOnClick` | bool | true | 点击鼠标左键锁定光标 |

## 输入映射

| 输入 | 行为 |
|------|------|
| 鼠标 X | YawPivot 绕世界 Y 轴旋转 |
| 鼠标 Y | PitchPivot 绕局部 X 轴旋转（Clamp ±89°） |
| W / ↑ | 向相机前方移动 |
| S / ↓ | 向相机后方移动 |
| A / ← | 向相机左方移动 |
| D / → | 向相机右方移动 |
| Esc | 释放光标 |
| 鼠标左键 | 锁定光标（当 `_lockCursorOnClick = true`） |

## 关键实现细节

### 1. 双层级旋转的目的

将 Yaw 和 Pitch 分离到两层空物体，避免直接操作相机的欧拉角导致的万向节锁和累积误差：

- **YawPivot** 始终绕世界 Y 轴旋转，`rotation` 直接设置为 `Quaternion.Euler(0, yaw, 0)`
- **PitchPivot** 绕父节点的局部 X 轴旋转，`localRotation` 仅包含 Pitch 分量

两层互不干扰，修改任意一层不影响另一层的参考系。

### 2. 移动的水平面投影

WASD 移动方向取自相机的 `forward` 和 `right`，但将 Y 分量清零后归一化。这样无论相机俯仰角度多大，移动始终在水平面上进行，速度保持恒定：

```csharp
forward.y = 0f; forward.Normalize();
right.y   = 0f; right.Normalize();
```

### 3. 自动层级解析

若 Inspector 中未手动赋值 `_yawPivot` / `_pitchPivot`，`Awake` 阶段会按 `Child(0)` → `Grandchild(0)` 的顺序自动查找，降低手动装配出错的概率。

### 4. 光标管理

- Play Mode 启动时自动锁定光标
- Esc 释放光标方便操作 Editor
- 鼠标左键重新锁定（可配置关闭）

## 使用方式

1. 在场景中创建如下层级：
   ```
   CameraRig (空 GameObject, 添加 CameraRigController)
   └── YawPivot (空 GameObject)
       └── PitchPivot (空 GameObject)
           └── Main Camera
   ```
2. 将原 Main Camera 拖入 PitchPivot 下
3. 进入 Play Mode，移动鼠标旋转视角，WASD 移动

## 扩展方向

| 方向 | 说明 |
|------|------|
| 加速/奔跑 | Shift 加速、Ctrl 减速，乘以速度倍数 |
| 惯性/平滑 | 旋转和移动加入 `SmoothDamp` 插值 |
| 碰撞检测 | 移动前做 `SphereCast` 防止穿墙 |
| Q/E 升降 | 沿世界 Y 轴上下移动 |
| 手柄支持 | 接入 Input System 的 Gamepad 摇杆 |
| 移动端适配 | 触摸滑动旋转 + 虚拟摇杆移动 |
