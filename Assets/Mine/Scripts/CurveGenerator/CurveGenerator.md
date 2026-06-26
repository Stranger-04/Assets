# CurveGenerator — 曲线预计算工具

**路径:** `Assets/Mine/Scripts/CurveGenerator/`
**类型:** 编辑器工具 + 运行时数据资产
**依赖:** `CurveAsset` (ScriptableObject), `CurveGeneratorWindow` (EditorWindow)

---

## 功能概述

将用户指定的控制点（场景空物体）烘焙为均匀采样的曲线数据资产（`CurveAsset`），供 GPU 粒子模拟（如轨迹跟随）直接读取。支持 Catmull-Rom 和 Bezier 两种插值，支持 2D 平面约束。

---

## 架构

```
CurveGeneratorWindow (Editor)
  │  GUI: 控制点拖拽、曲线类型/维度选择、Generate/Save
  │  预览: SceneView Handles 绘制控制点 + 曲线 + 切线
  │
  ├─→ CurveBake (Runtime Utility)
  │     static Bake(pts, type, dim, samples, loop) → Result
  │     内部: ProjectPositions → CatmullRom / Bezier 采样
  │     返回: positions[], tangents[], arcLengths[], totalLength
  │
  └─→ CurveAsset (ScriptableObject)
        持久化: 采样数据 + 元信息
        CreateAssetMenu: "Mine/Curve Asset"
```

### 类关系

| 类 | 位置 | 职责 |
|---|---|---|
| `CurveGeneratorWindow` | `Assets/Editor/` | EditorWindow: GUI + Scene 预览 + Save |
| `CurveBake` | `Scripts/CurveGenerator/` | 无状态数学工具: 投影 + Catmull-Rom + Bezier |
| `CurveAsset` | `Scripts/CurveGenerator/` | ScriptableObject: 曲线数据容器 |

### 数据流

```
场景空物体 (Transform[])          用户操作
  │                                Tools → Curve Generator...
  ▼
控制点位置 (Vector3[])             
  │                                
  ├── ProjectPositions() ─── 2D 模式: 投影到 XY/XZ/YZ 平面
  │
  ├── CatmullRom 或 Bezier ─── 均匀采样 N 段
  │
  ▼
CurveBake.Result                   内存预览
  │  positions[] tangents[] arcLengths[]
  │
  ├── SceneView Handles ────── 黄色 CP 球 + 青色曲线 + 橙色切线
  │
  └── Save → CurveAsset.asset ─ 持久化到磁盘
```

---

## 配置参数

### Window 面板

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| Control Points | Transform[] | — | 场景空物体，拖拽入窗口（≥2 个） |
| Dimension | Toolbar | XYZ | XYZ / XY / XZ / YZ，投影到指定平面 |
| Type | Enum | CatmullRom | CatmullRom（过控制点）/ Bezier（过控制点，自动手柄） |
| Samples | Slider | 256 | 采样分辨率 (16–4096) |
| Loop | Toggle | false | 首尾闭合 |
| Asset Path | Text | `Assets/Mine/Curves/NewCurve.asset` | Save 输出路径 |

### CurveAsset 资产格式

| 字段 | 类型 | 说明 |
|------|------|------|
| `curveType` | Enum | CatmullRom / Bezier |
| `dimension` | Enum | XYZ / XY / XZ / YZ |
| `loop` | bool | 是否闭合 |
| `positions` | Vector3[] | 均匀采样的曲线点 |
| `tangents` | Vector3[] | 每点的归一化切线方向 |
| `arcLengths` | float[] | 累计弧长（从起点起算） |
| `totalLength` | float | 曲线总长度 |
| `sampleCount` | int | 采样点数 |
| `controlPointCount` | int | 控制点数量 |
| `controlPointPositions` | Vector3[] | Bake 时控制点位置快照 |

---

## 曲线类型

### Catmull-Rom

- C1 连续（位置 + 切线连续）
- 曲线精确穿过每个控制点
- 适合：有机运动轨迹、相机路径

### Bezier（自动手柄）

- 自动从相邻控制点计算手柄（Catmull-Rom 1/6 因子转换）
- 曲线精确穿过每个控制点
- 手柄不可单独调整（简化版，保证平滑）
- 适合：与 Catmull-Rom 交叉验证、后续扩展手动手柄

### 2D 模式

选择 XY/XZ/YZ 后，所有控制点投影到目标平面（取平均深度），生成的曲线完全平直。

---

## 使用方式

### 前置条件

1. 场景中有 ≥2 个空物体作为控制点
2. 打开 `Tools → Curve Generator...`

### 操作流程

1. 拖拽场景中的控制点空物体到窗口 `CP 0` … `CP N` 字段
2. 选择 Dimension 和 Type
3. 点击 `Generate`——Scene 视图显示曲线预览
4. 调整控制点位置后重新 Generate
5. 满意后点击 `Save`，存为 `.asset`

### 运行时读取

```csharp
CurveAsset curve = Resources.Load<CurveAsset>("Curves/MyCurve");

// 归一化采样 (t ∈ [0,1])
curve.Sample(0.5f, out Vector3 pos, out Vector3 tangent);

// 或直接访问数组
for (int i = 0; i < curve.positions.Length; i++)
    Debug.Log(curve.positions[i]);
```

---

## 文件清单

```
Assets/
├── Editor/
│   └── CurveGeneratorWindow.cs      ← EditorWindow（GUI + Scene 预览 + Save）
│
└── Mine/Scripts/CurveGenerator/
    ├── CurveGenerator.md             ← 本文档
    ├── CurveBake.cs                  ← 曲线烘焙算法（Catmull-Rom / Bezier）
    └── CurveAsset.cs                 ← ScriptableObject 数据容器
```

---

## 扩展点

- **BSpline / Hermite**：在 `CurveBake.Bake()` 中添加分支
- **手动手柄 Bezier**：控制点改为 `Transform[2]`（位置 + 手柄）
- **3D 曲线管**：将 `positions[]` + `tangents[]` + `tubeRadius` 上传 GPU ComputeBuffer
- **实时烘焙**：`CurveBake` 是纯静态方法，可在运行时调用（如动态生成的曲线）
