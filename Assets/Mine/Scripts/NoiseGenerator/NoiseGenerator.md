# NoiseGenerator — 程序化噪声纹理生成

> 日期: 2026-06-25
> 路径: `Assets/Mine/Scripts/NoiseGenerator/`
> 依赖: Unity 6, Editor Coroutine (for EditorWindow)

---

## 概述

`NoiseGenerator` 是一套纯 CPU 的 3D 噪声生成工具，将多种噪声算法打包为静态方法，在 Editor 和 Runtime 均可调用。配套 `NoiseGeneratorWindow` 提供可视化参数面板，一键生成并保存 `Texture2D` / `Texture3D` 资源。

- **Perlin Noise** — 经典梯度噪声，适合自然纹理、体积云、地形高度图
- **Voronoi (Worley) Noise** — 细胞噪声，适合石材纹理、生物结构、星云效果

两种噪声均支持 **无缝平铺（seamless tiling）**，生成的纹理可直接用于 WrapMode.Repeat。

---

## 文件清单

### C# Scripts

| 文件 | 类 | 职责 |
|---|---|---|
| `NoiseGenerator.cs` | `NoiseGenerator` (static) | 外观入口：纹理生成、采样调度、平铺处理、共享工具 |
| `Noises/PerlinNoise.cs` | `PerlinNoise` (static) | Perlin 3D 噪声核心：perm 表、梯度插值、周期变体 |
| `Noises/VoronoiNoise.cs` | `VoronoiNoise` (static) | Voronoi 3D 噪声核心：特征点哈希、距离计算、周期变体 |

### Editor

| 文件 | 类 | 职责 |
|---|---|---|
| `Assets/Editor/NoiseGeneratorWindow.cs` | `NoiseGeneratorWindow : EditorWindow` | 可视化面板：参数调节、实时预览、资源保存 |

---

## 架构

```
NoiseGenerator (facade)
├── NoiseType enum   → Perlin / Voronoi
├── 纹理生成          → Generate3DTexture / GenerateChannelTexture / Generate2DSliceTexture
├── 无缝平铺          → MakeSeamless2D / MakeSeamless3D
├── 采样入口          → Sample3D → SampleTileable3D → SampleBaseNoise
├── 共享工具          → Repeat01 / Lerp / Seed01
│
├── PerlinNoise       → Sample(x,y,z,scale) / SamplePeriodic(x,y,z,scale,period)
│   ├── perm[512]     ← 标准排列表
│   ├── Fade(t)       ← 6t⁵-15t⁴+10t³ 平滑曲线
│   └── Grad(hash, x, y, z) ← 梯度向量点积
│
└── VoronoiNoise      → Sample(x,y,z,scale) / SamplePeriodic(x,y,z,scale,period)
    ├── Hash01        ← 3D 整数哈希 → [0,1)
    ├── FeaturePoint  ← 单元格内随机特征点
    └── PositiveMod   ← 正模运算（支持负数）
```

### 数据流

```
调用方
  → NoiseGenerator.Generate3DTexture(size, scale, seamless, noiseType)
    → 遍历 [x,y,z] → Sample3D(wx, wy, wz, scale, period, seamless, type)
      ├─ seamless=false → PerlinNoise.Sample / VoronoiNoise.Sample
      └─ seamless=true  → SampleTileable3D（四维8点插值→消除接缝）
                            → SampleBaseNoise → PerlinNoise.Sample / VoronoiNoise.Sample
    → MakeSeamless3D（最后一行/列/层从对侧拷贝，闭合周期）
    → Texture3D.SetPixels + Apply
```

---

## 公共 API

### Texture3D 生成

```csharp
Texture3D tex = NoiseGenerator.Generate3DTexture(
    size: 32,           // 每维分辨率 [4, 256]
    scale: 4f,          // 噪声频率倍率
    seamless: true,     // 是否无缝平铺
    noiseType: NoiseGenerator.NoiseType.Perlin
);
```

### Texture2D 生成（单通道，从 3D 切片）

```csharp
Texture2D tex = NoiseGenerator.GenerateChannelTexture(
    resolution: 256,    // 分辨率 [8, 2048]
    scale: 4f,          // 噪声频率倍率
    seamless: true,     // 是否无缝平铺
    noiseType: NoiseGenerator.NoiseType.Voronoi,
    randomSeed: 42f     // 选择 3D 体中哪个 Y 切片
);
```

### Texture2D 生成（多通道 RGBA，从 3D 体切片）

```csharp
Texture2D tex = NoiseGenerator.Generate2DSliceTexture(
    resolution: 256,
    channels: 4,          // 1–4 通道输出
    sliceStart: 0.1f,     // Y 起始位置
    sliceDistance: 0.25f, // 通道间 Y 间距
    scale: 4f,
    period: 256,
    seamless: true,
    noiseType: NoiseGenerator.NoiseType.Perlin
);
```

### 原始采样

```csharp
float value = NoiseGenerator.Sample3D(
    x: 0.5f, y: 0.3f, z: 0.8f,
    scale: 4f,
    period: 256,        // 无缝平铺的周期
    seamless: false,
    noiseType: NoiseGenerator.NoiseType.Perlin
);
// 返回值 ∈ [0, 1]
```

---

## 噪声算法

### Perlin Noise

经典 3D Perlin 噪声实现：

- **梯度向量**：12 条边方向 + 4 条体对角线（标准的 16 方向集）
- **平滑曲线**：`Fade(t) = 6t⁵ − 15t⁴ + 10t³`（保证 C² 连续，消除视觉上的二阶不连续）
- **排列表**：标准 256 值 × 2 副本（512 条目），避免周期性索引越界
- **输出值域**：`[0, 1]`（内部 `[-1,1]` 经过 `*0.5+0.5` 映射）

### Voronoi (Worley) Noise

3D 细胞噪声，计算到最近特征点的距离：

- **特征点生成**：每个整数格点的特征点偏移量由 `Hash01(cx,cy,cz, seed)` 确定，落在 `[0,1)³` 范围内
- **搜索邻域**：包含自身在内的 3×3×3 共 27 个格点
- **距离度量**：欧几里得距离
- **输出值域**：`[0, 1]`（`1 − clamp(minDist, 0, 1)`，靠近特征点 = 0，远离 = 1）

### 周期（无缝）变体

两种噪声的无缝变体通过坐标包装实现，使噪声图案在 `[0, period)` 范围内无缝重复：

- **PerlinPeriodic**：坐标映射到 `[0, period)` 范围，利用周期模运算访问排列表，梯度插值正常进行
- **VoronoiPeriodic**：坐标映射到 `[0, period)` 范围，特征点搜索时使用 `PositiveMod` 进行周期包装，距离计算使用最小镜像距离

---

## 无缝平铺机制

`NoiseGenerator` 实现两层无缝保障：

1. **采样阶段**（seamless=true 时）：
   - `SampleTileable3D` 对输入坐标做 8 个偏移采样（`{0,1}³`），通过三线性插值消除周期边界的梯度不连续
   - 坐标超出 [0,1] 时 `Repeat01` 做模运算

2. **后处理阶段**（MakeSeamless）：
   - `MakeSeamless3D`：将最后一层/行/列的像素值替换为第一层/行/列的对应值
   - `MakeSeamless2D`：同理，消除纹理自身的边界接缝

---

## Parameters

### Texture3D 设置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `size` | int | 32 | 每维分辨率，范围 [4, 256] |
| `scale` | float | 4 | 噪声缩放/频率倍率，越大噪声越密集 |
| `seamless` | bool | true | 是否生成无缝平铺纹理 |
| `noiseType` | NoiseType | Perlin | 噪声算法选择 |

### Texture2D Channel 设置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `resolution` | int | 32 | 分辨率，范围 [8, 2048] |
| `scale` | float | 4 | 噪声缩放/频率倍率 |
| `seamless` | bool | true | 是否无缝平铺 |
| `noiseType` | NoiseType | Perlin | 噪声算法选择 |
| `randomSeed` | float | 0 | 随机种子，控制 3D 体中采样的 Y 切片位置 |
| `channels` | int | 4 | RGBA 通道数，范围 [1, 4] |

---

## 使用方式

### 通过 Editor 窗口

1. 菜单栏：`Tools → Noise Generator...`
2. 切换输出模式 `Texture3D` / `Texture2D`
3. 调整参数（噪声类型、分辨率、缩放、无缝选项）
4. 点击 `Generate` 预览效果
5. 设置输出路径，点击 `Save` 将 .asset 写入项目

### 通过代码

```csharp
using UnityEngine;

// 生成 3D Perlin 噪声纹理
var noise3D = NoiseGenerator.Generate3DTexture(32, 4f, true, NoiseGenerator.NoiseType.Perlin);

// 生成 2D Voronoi 无缝纹理
var noise2D = NoiseGenerator.GenerateChannelTexture(512, 8f, true, NoiseGenerator.NoiseType.Voronoi, 0.5f);

// 对 Shader 中的 custom 体积噪声进行手动采样
float n = NoiseGenerator.Sample3D(uv.x, uv.y, uv.z, 4f, 256, true, NoiseGenerator.NoiseType.Perlin);
```

### 常见用途

| 用途 | 推荐噪声 | 设置 |
|------|----------|------|
| 体积云基础层 | Perlin 3D | `size=64, scale=4` |
| 体积云细节层 | Voronoi 3D | `size=64, scale=12` |
| 地形高度图 | Perlin 2D | `resolution=512, scale=8` |
| 石材/大理石 | Voronoi 2D | `resolution=512, scale=6` |
| 水波法线图 | Perlin RGBA (4通道) | `channels=4, scale=3` |

---

## 扩展方向

| 方向 | 说明 |
|------|------|
| Simplex Noise | 比 Perlin 具有更好的各向同性，减少方向性 artifacts |
| FBM / Turbulence | 多倍频叠加（楼座式），octaves + lacunarity + gain |
| GPU Compute Shader | 将生成移到 GPU，处理 256³ 及以上的体纹理 |
| Voronoi 距离类型 | 除 `F1`（最近点）外增加 `F2`（次近点）、`F2-F1`（边缘）、`F1*F2` |
| Domain Warping | 用一组噪声扭曲另一组噪声的输入坐标，产生有机流动效果 |
| Curl Noise | 从 Perlin 梯度场提取旋转分量，用于无散流体速度场 |
| 运行时异步生成 | 用 `UniTask` 或 `Job System` 在后台线程生成，避免主线程卡顿 |
| 序列化 3D 切片 | 将 3D 体纹理的切片导出为 2D 纹理数组序列，用于 Flipbook 动画 |
