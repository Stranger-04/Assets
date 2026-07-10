# PCSS — 屏幕空间软阴影

**路径:** `Assets/Mine/Shaders/PCSS/`
**类型:** RendererFeature + Custom Shadow Caster
**目标管线:** URP 17+ (Unity 6)

---

## 功能概述

自定义阴影管线，绕过 Unity 内置 CSM，用球体包围盒计算光源正交投影，将不透明物体深度写入 RFloat RT。后续用 ComputeShader 实现 PCSS 软阴影。

V1 仅完成 Shadow RT 生成 + Debug 可视化。

---

## 渲染管线

```
Frame Start
  │
  ├── URP 内置 ShadowCaster（不动）
  │
  ├── PCSSFeature.CustomShadowCasterPass (AfterRenderingShadows)
  │     ├── 球体包围盒 → light view/proj
  │     ├── ImportTexture(RFloat RT)
  │     ├── CreateRendererList(overrideMat=CustomShadowCaster)
  │     └── DrawRendererList → RFloat RT（深度写入 R 通道）
  │
  └── [Debug] showShadowMap → Blitter.BlitCameraTexture
```

---

## 参数说明

### Shadow Map

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `shadowCasterShader` | Shader | null | Inspector 拖入 CustomShadowCaster.shader |
| `shadowMapResolution` | Range(256, 4096) | 2048 | Shadow RT 分辨率 |

### Debug

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `showShadowMap` | bool | true | ON = 将 Shadow RT 灰度图叠加到屏幕 |

---

## 使用方式

### 前置条件

1. URP Renderer 的 `Renderer Features` 中添加 PCSSFeature
2. 拖入 `CustomShadowCaster.shader` 到 `Shadow Caster Shader` 字段
3. 场景中必须有 Directional Light（RenderSettings.sun）

### 推荐场景

- PCSS 软阴影调试
- 自定义 Shadow Map 可视化
- 后续 ComputeShader 集成

---

## 已知限制

- **单级联**：V1 无多级联，远距离阴影精度受限于分辨率
- **全场景重绘**：每帧将所有不透明物体重绘到 RFloat RT
- **Debug 仅灰度**：直接 blit 原始深度，无 PCF/PCSS
- **不劫持 URP 阴影**：其他 Shader 仍使用 Unity 内置阴影

## 扩展点

- 多级联 + 轮换更新
- ComputeShader PCSS 软阴影
- 劫持 `_ScreenSpaceShadowmapTexture`（零侵入替换）
