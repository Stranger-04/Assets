# PCSS — Percentage-Closer Soft Shadows

**路径:** `Assets/Mine/Shaders/PCSS/`
**类型:** ScriptableRendererFeature + Custom Shadow Caster
**目标管线:** URP 17+ (Unity 6)

---

## 功能概述

自定义阴影管线，绕过 Unity 内置 CSM，完整实现 PCSS 软阴影：

- **多级联 PSSM**：4 级联 + 2×2 Tiled Atlas 布局
- **Blocker Search**：Vogel disk 采样，估算平均遮挡深度
- **Penumbra 估算**：由遮挡深度差推导半影大小
- **变核 PCF**：半影越大 → PCF 核越大 → 阴影越软
- **Penumbra Mask**：快速跳过全亮/全暗像素，减少无效 PCF 计算
- **斜面深度偏移**：PCF 深度偏移随坡度和核大小自适应
- **双边保边模糊**：后处理去噪，保持硬阴影边缘

---

## 文件结构

```
PCSS/
├── PCSS.compute             ← Compute Shader: Blocker Search + Penumbra + PCF
├── PCSS.shader              ← 双边保边模糊（BlurH / BlurV 两个 Pass）
├── PCSSFeature.cs           ← RendererFeature: 级联渲染 + PCSS + Blur Pass
├── CustomShadowCaster.shader ← 阴影深度写入（深度/法线偏移）
├── PCSSDebugPlane.shader    ← Debug 可视化平面（PCSS / Unity CSM）
└── PCSS.md                  ← 本文档
```

---

## 渲染管线

```
Frame Start
  │
  ├── CustomShadowCasterPass (AfterRenderingShadows)
  │     ├── PSSM Split → 4 级联距离
  │     ├── 每级联计算球体包围盒 → Light View/Proj（texel-aligned）
  │     ├── 光源视角 Culling（最宽级联覆盖全场景）
  │     ├── ImportTexture(RFloat RT)
  │     └── 逐级联 DrawRendererList → 2×2 Atlas（R 通道存深度）
  │
  └── PCSSPass (AfterRenderingTransparents)
        ├── cmd.DispatchCompute(PCSS_Main)  [Compute Shader]
        │     ├── Frustum Corner Ray → 世界坐标重建
        │     ├── 级联选择（世界空间距离）
        │     ├── Atlas UV → 双线性采样
        │     ├── Penumbra Mask（3×3 邻域快速判定）
        │     ├── Blocker Search（Vogel disk）
        │     ├── Penumbra 估算
        │     └── 变核 PCF（Vogel disk + 斜面偏移）
        ├── Blitter: BlurH（双边保边水平模糊）
        ├── Blitter: BlurV（双边保边垂直模糊）
        └── Blitter: BlitCameraTexture → 屏幕
```

---

## Pass 编排

| 顺序 | Pass 名称 | 类型 | 目标 | 说明 |
|------|----------|------|------|------|
| — | CustomShadowCaster | RasterRenderPass | `_PCSS_ShadowCacheTex` (RFloat) | 逐级联渲染深度到 2×2 Atlas |
| — | PCSS Compute | UnsafePass (DispatchCompute) | `_PCSS_SoftShadow` (ARGBHalf) | Compute Shader: Blocker Search + Penumbra + PCF |
| 0 | BlurH | UnsafePass (Blitter) | `_PCSS_BlurTemp` (ARGBHalf) | 双边保边水平模糊 |
| 1 | BlurV | UnsafePass (Blitter) | `_PCSS_SoftShadow` (ARGBHalf) | 双边保边垂直模糊 |
| — | Blit | — | `activeColorTexture` | 最终叠加到屏幕（showShadowMap=true） |

## RT 规格

| RT | 格式 | 分辨率 | 生命周期 | 说明 |
|----|------|--------|---------|------|
| `_PCSS_ShadowCacheTex` | RFloat | 2048×2048 | 持久化 | 4 级联 2×2 Atlas，R 通道存深度 |
| `_PCSS_ShadowDepth` | Depth 16bit | 2048×2048 | 临时（Pass 内） | Caster Pass 深度附件 |
| `_PCSS_SoftShadow` | ARGBHalf | 全屏 | 临时（Pass 内） | PCSS 结果 + 模糊中间输出 |
| `_PCSS_BlurTemp` | ARGBHalf | 全屏 | 临时（Pass 内） | 双边模糊临时缓冲 |

## 性能

| 指标 | 数值 | 说明 |
|------|------|------|
| Caster DrawCall | ~场景不透明物体数 | SRP Batcher 合并，每级联 1 次 |
| Blocker Samples | 8-32（默认 32） | 仅 Penumbra 区域（mask 跳过全亮/全暗） |
| PCF Samples | 4-32（默认 32） | 仅 Penumbra 区域 |
| RT 数量 | 3 | ShadowCache + SoftShadow + BlurTemp |
| 带宽 | 2048² RFloat 写入 + 2× 全屏 ARGBHalf 读写 | Caster + PCSS + Blur×2 |
| 主要开销 | Blocker search + PCF 双线性采样 | 每采样 4 次纹理读取 |

---

### Shadow Map

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `shadowMapResolution` | 256-4096 | 2048 | Atlas 总分辨率（每 tile = Res/2） |

### Cascades

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `cascadeCount` | 1-8 | 4 | 级联数量 |
| `pssmLambda` | 0-1 | 0.75 | PSSM 混合因子（0=均匀, 1=对数） |
| `shadowDistance` | 10-200 | 50 | 阴影覆盖距离 |

### PCSS

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `blockerSamples` | 8-64 | 32 | Blocker search 采样数 |
| `pcfSamples` | 4-64 | 32 | PCF 采样数 |
| `lightSize` | 0.1-5 | 1.0 | 光源尺寸（影响半影大小） |
| `softness` | 0.1-2 | 1.0 | PCF 偏移缩放 |

### Shadow Bias

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `depthBias` | 0-2 | 0.5 | 深度偏移（沿光源方向） |
| `normalBias` | 0-2 | 0.4 | 法线偏移（沿表面法线） |

### Blur

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `enableBlur` | bool | true | 启用双边保边模糊 |
| `blurScale` | 0-5 | 1.0 | 模糊强度 |

### Debug

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `showShadowMap` | bool | true | 将阴影叠加到屏幕 |
| `debugMode` | 0-6 | 0 | 0=正常, 1=blockerRatio, 2=hardShadow, 3=avgBlocker, 4=penumbra, 5=pcfBias, 6=penumbraMask |

---

## 世界空间重建

Compute Shader 不能访问 `UNITY_MATRIX_I_VP`（引擎内置矩阵不走全局 shader property），改用 **Frustum Corner Ray** 方法：

```
C# ViewportToWorldPoint(4 corners) → 4 条远平面射线
HLSL lerp(4 rays, uv) → 像素方向
positionWS = cameraPos + ray * Linear01Depth(rawDepth)
```

与 `mul(invVP, clipPos)` 数学等价，但避免了 float4x4 传递问题。

## Shader 常量速查

| 常量 | 值 | 用途 |
|------|-----|------|
| `linear01` | `1 / (_ZBufferParams.x * rawDepth + _ZBufferParams.y)` | 线性化深度（0=近, 1=远） |
| `searchPixels` | `20 * halfW0 / halfWci` | Blocker 搜索半径（像素），远级联自动缩小 |
| `preRadiusPixels` | `0.15 * 512 / halfWci` | 预扫描半径（固定世界空间 0.15m，与 ShadowDistance 解耦） |
| `maskPixels` | `clamp(roughPenumbra * 0.67, 2, 50)` | Penumbra mask 步长（自适应：接触阴影窄，远距离遮挡宽） |
| `penumbraWS` | `depthDiff * zDist / halfW * lightSize` | 半影世界空间尺寸 |
| `penumbraPixels` | `penumbraWS * 512 / halfW * softness, capped 256` | 半影像素数（物理正确，与 ShadowDistance 解耦） |
| `pcfBias` | `baseBias * (penumbraPixels / 100.0) * softness` | PCF 深度偏移（核越大偏移越大） |
| `baseBias` | `(20 * halfW0 / 1024) * gradient / zDist` | 斜面基础偏移 |

### Mask 自适应策略

```
4-sample 对角线预扫描（0.15m 世界半径）
  ├─ all-4 lit     → 跳过 3×3，return 9（全亮）
  ├─ all-4 shadow  → 跳过 3×3，return 0（全暗）
  └─ mixed (1-3)   → roughPenumbra → adaptive maskPixels → 3×3 精确判定
```

均匀区域只需 4 次采样，仅在边缘区域执行完整 3×3。

---

## 使用方式

### 前置条件

1. URP Renderer 的 `Renderer Features` 中添加 `PCSSFeature`
2. 拖入 `CustomShadowCaster.shader` → `Shadow Caster Shader`
3. 拖入 `PCSS.shader` → `PCSS Shader`（双边模糊用）
4. 拖入 `PCSS.compute` → `PCSS Compute Shader`
5. 场景中必须有 Directional Light（`RenderSettings.sun`）

### 推荐场景

- PCSS 软阴影效果展示
- 级联 Shadow Map 调试
- 自定义阴影管线研究

---

## 已知限制

- **Compute Shader 矩阵不可用**：URP 中 `UNITY_MATRIX_I_VP` 不走全局 shader property，改 Frustum Corner Ray 替代
- **全场景重绘**：每帧将所有不透明物体重绘到 RFloat RT
- **不劫持 URP 阴影**：其他 Shader 仍使用 Unity 内置阴影（需手动替换宏）
- **Alpha Test 支持有限**：CustomShadowCaster 有 AlphaTest Pass，但需物体使用对应 Shader
- **点光源不支持**：仅 Directional Light

## 扩展点

- 劫持 `_ScreenSpaceShadowmapTexture` 实现零侵入替换
- 级联轮换更新（奇数帧更新 0/2，偶数帧更新 1/3）
- 点光源 / 聚光灯 PCSS
