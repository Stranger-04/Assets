# PCSS — Percentage-Closer Soft Shadows

**路径:** `Assets/Mine/Shaders/PostProcess/PCSS/`
**类型:** ScriptableRendererFeature + Compute Shader
**目标管线:** URP 17+ (Unity 6)

---

## 功能概述

自定义阴影管线，绕过 Unity 内置 CSM，完整实现 PCSS 软阴影：

- **多级联 PSSM**：4 级联 + 2×2 Tiled Atlas 布局
- **逐级联 Culling**：远近级联独立剔除，远级联自动减少 draw
- **Penumbra Mask**：4-sample 对角线预扫描 + 自适应步长 3×3，快速跳过全亮/全暗
- **Blocker Search**：Vogel disk 采样，估算平均遮挡深度
- **Penumbra 估算**：世界空间 → shadowmap pixels 物理正确转换，随 ShadowDistance 自动缩放
- **变核 PCF**：半影越大 → PCF 核越大 → 阴影越软
- **斜面深度偏移**：PCF 深度偏移随坡度和核大小自适应
- **双边保边模糊**：Compute Shader 实现，仅 penumbra 像素执行

---

## 文件结构

```
PCSS/
├── PCSS.compute              ← Compute Shader（3 kernel 入口 + #include）
├── PCSS_Function.hlsl        ← 共享纹理声明、VogelDisk、全部工具函数 + 双边模糊
├── PCSSTemplate.shader        ← 场景物体模板 shader（Forward + DepthNormals + CustomShadowCaster）
├── PCSSFeature.cs             ← ScriptableRendererFeature: CasterPass + PCSSPass
├── PCSSDebugPlane.shader      ← Debug 可视化平面
└── PCSS.md                    ← 本文档
```

---

## 渲染管线

```
Frame Start
  │
  ├── CustomShadowCasterPass (AfterRenderingShadows)
  │     ├── PSSM Split → 4 级联距离
  │     ├── 每级联独立球体包围盒 → Light View/Proj（texel-aligned）
  │     ├── 逐级联光源视角 Culling
  │     ├── ImportTexture(RFloat RT + Depth RT)
  │     └── 逐级联 DrawRendererList → 2×2 Atlas（RFloat R 通道存深度）
  │         （原生 pass，无 override material，SRP Batcher 有效）
  │
  └── PCSSPass (AfterRenderingTransparents)
        ├── cmd.DispatchCompute(PCSS_Main)
        │     ├── Frustum Corner Ray → 世界坐标重建
        │     ├── 级联选择 → Atlas UV → 双线性采样
        │     ├── Penumbra Mask（预扫描 + 自适应 3×3）
        │     ├── Blocker Search（Vogel disk）
        │     ├── Penumbra 估算 → 变核 PCF
        │     └── 输出 alpha 编码 mask（1=penumbra, 0=skip）
        ├── cmd.DispatchCompute(PCSS_BlurH)  [仅 mask 像素]
        ├── cmd.DispatchCompute(PCSS_BlurV)  [仅 mask 像素]
        └── Blitter.BlitCameraTexture → 屏幕
```

---

## Pass 编排

| 顺序 | Pass 名称 | 类型 | 目标 | 说明 |
|------|----------|------|------|------|
| — | CustomShadowCaster | RasterRenderPass | `_PCSS_ShadowCacheTex` (RFloat) | 逐级联原生 pass → 2×2 Atlas |
| — | PCSS Compute | UnsafePass (DispatchCompute) | `_PCSS_SoftShadow` (ARGBHalf) | PCSS + mask 编码 |
| — | BlurH Compute | UnsafePass (DispatchCompute) | `_PCSS_BlurTemp` (ARGBHalf) | 水平双边模糊（仅 penumbra） |
| — | BlurV Compute | UnsafePass (DispatchCompute) | `_PCSS_SoftShadow` (ARGBHalf) | 垂直双边模糊（仅 penumbra） |
| — | Blit | — | `activeColorTexture` | 最终叠加到屏幕 |

## RT 规格

| RT | 格式 | 分辨率 | 生命周期 | 说明 |
|----|------|--------|---------|------|
| `_PCSS_ShadowCacheTex` | RFloat | 2048×2048 | 持久化 | 4 级联 2×2 Atlas |
| `_PCSS_ShadowDepth` | Depth 16bit | 2048×2048 | 临时 | Caster Pass 深度附件 |
| `_PCSS_SoftShadow` | ARGBHalf (enableRandomWrite) | 全屏 | 持久化 | PCSS 结果 + 模糊输出（alpha=mask） |
| `_PCSS_BlurTemp` | ARGBHalf (enableRandomWrite) | 全屏 | 持久化 | 模糊中间缓冲 |

---

## 参数速查

### Resources
| 参数 | 说明 |
|------|------|
| `pcssTemplateShader` | 场景物体 shader（需含 `LightMode=CustomShadowCaster` pass） |
| `pcssComputeShader` | `PCSS.compute` |

### Shadow Map
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `shadowMapResolution` | 2048 | Atlas 总分辨率（tile = Res/2） |

### Cascades
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `cascadeCount` | 4 | 级联数量 |
| `pssmLambda` | 0.75 | PSSM 混合因子 |
| `shadowDistance` | 50 | 阴影覆盖距离 (m) |

### PCSS
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `quality` | High | Low(8) / Medium(16) / High(32) 采样数 |
| `lightSize` | 1.0 | 光源尺寸 |
| `softness` | 1.0 | PCF 软度缩放 |

### Shadow Bias
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `depthBias` | 0.1 | 沿光源反方向偏移 (m) |
| `normalBias` | 0.0 | 沿法线偏移 (m) |

### Blur
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `enableBlur` | true | 双边保边模糊 |
| `blurScale` | 1.0 | 模糊强度 |

### Debug
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `showShadowMap` | true | 阴影叠加到屏幕 |

---

## Shader 常量速查

| 常量 | 值 | 用途 |
|------|-----|------|
| `searchPixels` | `20 * halfW0 / halfWci` | Blocker 搜索半径，远级联自动缩小 |
| `preRadiusPixels` | `0.15 * 512 / halfWci` | Mask 预扫描半径（固定 0.15m 世界空间） |
| `maskPixels` | `clamp(roughPenumbra * 0.67, 2, max(2, preRadius))` | Mask 自适应步长 |
| `penumbraWS` | `depthDiff * zDist / halfW * lightSize` | 半影世界空间尺寸 |
| `penumbraPixels` | `penumbraWS * 512 / halfW, cap 256` | 半影像素数（物理正确，与 ShadowDistance 解耦） |
| `pcfBias` | `baseBias * (penumbraPixels / 100) * softness` | PCF 深度偏移 |
| `baseBias` | `(20 * halfW0 / 1024) * √(1-N·L²)/N·L / zDist` | 斜面基础偏移 |

---

## 使用方式

### 前置条件

1. URP Renderer 的 **Renderer Features** 中添加 `PCSSFeature`
2. 拖入 `PCSSTemplate.shader` → **Pcss Template Shader**
3. 拖入 `PCSS.compute` → **Pcss Compute Shader**
4. 场景物体使用 `PCSSTemplate` shader（或任何含 `LightMode=CustomShadowCaster` pass 的 shader）
5. 场景中必须有 Directional Light（`RenderSettings.sun`）

### 自定义 shader 接入

物体 shader 只需添加一个 `CustomShadowCaster` pass，参考 `PCSSTemplate.shader`：
- `LightMode = "CustomShadowCaster"`
- Vertex 施加 shadow bias
- Fragment 返回 `positionCS.z`

---

## 已知限制

- 仅 Directional Light
- 场景物体需使用含 `CustomShadowCaster` pass 的 shader
- Compute Shader 无法访问 `UNITY_MATRIX_I_VP`，改 Frustum Corner Ray 替代
- 不劫持 URP 阴影，其他物体仍使用 Unity 内置阴影

## 扩展点

- 劫持 `_ScreenSpaceShadowmapTexture` 零侵入替换
- 级联轮换更新（奇数帧 0/2，偶数帧 1/3）
- 点光源 / 聚光灯 PCSS
