# POSS — Per-Object Soft Shadow

**路径:** `Assets/Mine/Shaders/PostProcess/POSS/`
**类型:** ScriptableRendererFeature + Compute Shader
**目标管线:** URP 17+ (Unity 6)

---

## 功能概述

为特定动态物体提供独立的逐物体软阴影，与 PCSS/CSM 级联阴影共存：

- **Shadow Atlas**：单张 RFloat 纹理（1024²），Grid 切分为 Tile（256²）
- **紧密投影**：每物体 8 角点 AABB → 光源空间正交投影，最大化 texel 密度
- **PCF 软边缘**：Poisson disk 4-8 tap，半径可配
- **屏幕空间解算**：Compute Shader 逐像素重建世界坐标 → 投影 → 采样 Atlas → PCF → 输出 R8
- **CSM 隔离**：挂载 POSSComponent 自动关闭 Built-in 阴影投射

---

## 文件结构

```
POSS/
├── POSS.compute               ← Compute Shader（屏幕空间解算 + PCF）
├── POSSShadowCaster.shader    ← 光源空间深度写入（Hidden/POSS/ShadowCaster）
├── POSSTemplate.shader        ← 场景物体模板（Forward + DepthNormals + POSSShadowCaster）
├── POSSFeature.cs             ← RendererFeature: CasterPass + ResolvePass
├── POSSComponent.cs           ← 标记组件（仅 OnEnable/OnDisable 注册注销）
├── POSSManager.cs             ← 全局管理器（场景单例）
└── POSS.md                    ← 本文档
```

---

## 渲染管线

```
Frame Start
  │
  ├── POSSSShadowCasterPass (AfterRenderingShadows)
  │     ├── 收集 POSSManager 注册的物体
  │     ├── 每物体 8 角点 AABB → 光空间紧密正交投影
  │     ├── Reversed-Z + scale-bias → [0,1] Atlas 深度
  │     ├── 逐 Tile viewport + SetViewProjectionMatrices
  │     └── DrawMesh (Hidden/POSS/ShadowCaster) → RFloat Atlas
  │
  └── POSSResolvePass (AfterRenderingTransparents)
        ├── DispatchCompute(POSS_Resolve)
        │     ├── Frustum Corner Ray → 世界坐标重建
        │     ├── 投影到各物体光空间（_ObjectVPs[16]）
        │     ├── 双线性采样 Atlas Tile（边界 clamp）
        │     ├── DepthCmpLit（reversed-Z: occluder > receiver - bias）
        │     ├── SoftShadowPCF（Poisson disk, _PCFRadius / _PCFTaps）
        │     └── R8 _POSS_ShadowTexture（屏幕分辨率）
        └── showShadowOnly → Blitter.BlitCameraTexture
```

---

## RT 规格

| RT | 格式 | 分辨率 | 说明 |
|----|------|--------|------|
| `_POSS_ShadowAtlas` | RFloat | 1024² | 多 Tile Shadow Atlas（最多 16 物体） |
| `_POSS_ShadowDepth` | Depth 16bit | 1024² | Caster 深度附件（临时） |
| `_POSS_ShadowTexture` | R8 randomWrite | 全屏 | 屏幕空间阴影结果 |

---

## 参数速查

### Shadow Atlas
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `atlasResolution` | 1024 | Atlas 总分辨率 |
| `tileResolution` | 256 | 单物体 Tile 分辨率 |

### Shadow
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `depthBias` | 0.05 | 深度偏移 |
| `shadowStrength` | 1.0 | 阴影强度 |

### PCF
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `pcfRadius` | 2 | 软边缘半径（像素） |
| `pcfTaps` | 4 | 采样次数（1-8） |

### Debug
| 参数 | 默认值 | 说明 |
|------|--------|------|
| `showShadowOnly` | false | 仅显示阴影纹理 |

---

## 使用方式

1. URP Renderer → Add Renderer Feature → **POSSFeature**，拖入 `POSS.compute`
2. 场景需有 Directional Light
3. 给动态物体添加 `POSSComponent`（自动关闭内置阴影、注册到管理器）
4. 接收阴影的 Shader 采样 `_POSS_ShadowTexture`：

```hlsl
TEXTURE2D(_POSS_ShadowTexture);
SAMPLER(sampler_POSS_ShadowTexture);
float2 screenUV = positionCS.xy / _ScreenParams.xy;
float possShadow = SAMPLE_TEXTURE2D(_POSS_ShadowTexture, sampler_POSS_ShadowTexture, screenUV).r;
float finalShadow = min(mainLight.shadowAttenuation, possShadow);
```

参考 `POSSTemplate.shader` 获取完整示例。

---

## 已知限制

- 仅 Directional Light
- 仅 MeshRenderer + MeshFilter
- 物体数 ≤ (atlasRes / tileRes)²
- 投影距离固定 10m（硬编码）
