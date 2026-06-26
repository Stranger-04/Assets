# Universal Instance Manager — 架构设计

## 目标

将 GPU 实例化模拟拆分为三层：

- **运动数据**：纯 struct，定义每个实例的数据形态
- **管理中心**：`UniversalInstanceManager`，只负责 Buffer 生命周期、渲染调度、Editor 面板
- **执行逻辑**：`IUniversalInstanceSimulator` 接口 + 具体实现（如 `RainSimulation`），每个模拟自包含 ComputeShader 绑定

## 架构

```
UniversalInstanceManager (管理中心)
  ├── 持有: Mesh, Material, instanceCount, boundsSize
  ├── 通过 GetComponent<IUniversalInstanceSimulator>() 发现插件
  ├── 负责: Buffer 生命周期协调、间接绘制、Editor 面板
  └── 不持有 ComputeShader（由具体模拟自己持有）

IUniversalInstanceSimulator (接口)
  ├── Initialize(int count)     — 创建 Buffer、绑定 Kernel
  ├── Dispatch(float deltaTime) — 每帧更新
  ├── BindMaterial(Material)    — 绑定 Buffer 到渲染材质
  ├── Release()                 — 释放 Buffer
  └── VisibleCountBuffer {get}  — 可选裁剪 Buffer（null = 全部渲染）

RainSimulation : MonoBehaviour, IUniversalInstanceSimulator (执行 + 数据)
  ├── 公有字段: rainShader, gravity, wind, spawnRadius, resetHeight, deadZone
  ├── 数据结构: RainParticle { position, velocity }
  └── 每帧: 施加重力+风力 -> 位置更新 -> 低于阈值则重置到顶部
```

## 文件结构

```
Assets/Mine/Scripts/InstanceManager/
├── Plan.md                        ← 本文件
├── IUniversalInstanceSimulator.cs   ← 模拟插件接口
├── UniversalInstanceManager.cs    ← 通用管理器
└── RainSimulation.cs              ← 下雨模拟（测试用）

Assets/Mine/Shaders/Instance/
├── Rain.compute                   ← 雨滴物理计算
└── RainInstance.shader            ← 雨滴渲染
```

## 设计决策

- **接口不含 ComputeShader 参数**：每个 Simulation 自己在 Inspector 中持有引用
- **通过 GetComponent 发现**：Simulation 作为独立 MonoBehaviour 挂在同一 GameObject
- **不走 SerializeReference**：避免接口序列化兼容问题
- **无 namespace**：遵循项目惯例（ReadOnlyAttribute 也在全局命名空间）
- **雨模拟不用裁剪**：VisibleCountBuffer 返回 null

## 测试步骤

1. 创建空 GameObject，添加 `UniversalInstanceManager` + `RainSimulation`
2. Manager 指定 Quad Mesh + RainInstance 材质
3. Simulation 指定 Rain.compute
4. 点击 "Initialize Instances"，进入 Play Mode 观察
