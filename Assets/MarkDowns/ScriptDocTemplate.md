# 脚本文档模板

> 适用于 `ScriptableRendererFeature` + `ScriptableRenderPass` 后处理系统，以及 `MonoBehaviour` 游戏逻辑脚本。

---

## 模板

```markdown
# [系统名称] — [一句话描述]

**路径:** `Assets/Mine/Scripts/[Folder]/[FileName].cs`
**类型:** RendererFeature / MonoBehaviour / RuntimeSet
**依赖:** [Shader 路径 / Package / 其他]

---

## 功能概述

[2-3 句话说明这个系统的用途和整体行为]

---

## 架构

```
[简化的系统层级或数据流图]

  输入 (鼠标点击)
    │
    ├─ Pass A (MRT)
    │     └──→ RT: ObjectID / Depth / Normal
    │
    ├─ Pass B (AsyncReadback)
    │     └──→ CPU: selectedID
    │
    └─ Pass C (Composite)
          └──→ 屏幕输出
```

### 类关系

| 类 | 父类 | 职责 |
|---|---|---|
| `XxxFeature` | ScriptableRendererFeature | 面板参数 + Pass 注册 |
| `XxxPass` | ScriptableRenderPass | 渲染逻辑 |
| `XxxReadback` | MonoBehaviour | 输入处理 + GPU Readback |

---

## 配置参数

### Feature 面板参数

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `_shader` | Shader | null | 渲染用的 Shader |
| `_debugView` | enum | Off | 调试视图（ObjectID / Depth / Normal） |

### Shader 参数

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `_ObjectID` | Int | 0 | 物体唯一 ID（0-16M，RGB24 编码） |
| `_OutlineWidth` | Range(1,5) | 2 | 描边像素宽度 |

---

## 渲染管线

### Pass 编排

| 顺序 | Pass 名称 | 类型 | 目标 | 说明 |
|---|---|---|---|---|
| 1 | Picker MRT | UnsafePass | 3×RT | 绘制可选物体 |
| 2 | Outline Mask | UnsafePass | R8 RT | 绘制选中物体 Mask |
| 3 | Outline Composite | UnsafePass | CameraColor | 四邻采样 + 合成 |

### RT 规格

| RT | 格式 | 分辨率 | 生命周期 | 说明 |
|---|---|---|---|---|
| ObjectID | ARGB32, sRGB=false | 全屏 | 持久化 | Readback 用 |
| Depth | 相机格式 | 全屏 | 临时 | |
| Normal | 相机格式 | 全屏 | 临时 | |
| OutlineMask | R8 | 全屏 | 临时 | |

---

## 使用方式

### 前置条件

1. URP 管线，RenderGraph 模式
2. `PC_Renderer` 中添加 `XxxFeature`
3. 场景中有带对应 Shader 的物体

### 运行时行为

1. 每帧自动渲染到 MRT
2. 鼠标点击 → Readback → 更新选中状态
3. 选中物体 → 自动显示描边

### Debug 模式

- `debugView = ObjectID`：显示 ID RT 灰度图
- `debugView = Depth`：显示深度图
- `debugShowMask = true`：显示 Mask RT 白模

---

## 性能

| 指标 | 数值 | 说明 |
|---|---|---|
| DrawCall / 帧 | N (可拾取物体数) | SRP Batcher 合并 |
| RT 数量 | 4 | ObjectID + Depth + Normal + OutlineMask |
| Readback 延迟 | 1-2 帧 | AsyncGPUReadback |
| ID 范围 | 0-16M | RGB24 编码 |

---

## 已知限制

- 鼠标坐标依赖 `Camera.ScreenToViewportPoint`，Game 视图缩放比例变化时需重试
- `ImportTexture` 持久化 RT 混入 MRT 可能导致兼容性问题（当前已验证通过）
- Outline 描边在物体边缘重叠区域可能出现不连续

---

## 扩展点

- 深度感知选物：利用 Depth RT 做前后遮挡判断
- 法线加权描边：利用 Normal RT 做边缘方向增强
- 多选支持：selectedObjectID 改为 selectedSet
- Outline 跳变/动画：在 Composite Pass 中加入时间变量
```

---

## 字段命名速查

| 可见性 | 前缀 | 示例 |
|---|---|---|
| `public` / 属性 | 无 | `debugView` |
| `[SerializeField] private` | `_` | `_pickerShader` |
| `private` | `_` | `_objIDRT` |
| `static readonly int` (PropertyToID) | `s_` 或 PascalCase | `s_ObjectIDProp` |
| 局部变量 | 无 | `camDesc`, `renderers` |
