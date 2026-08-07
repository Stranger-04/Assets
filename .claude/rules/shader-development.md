---
paths:
  - "Assets/Mine/Shaders/**"
  - "**/*.shader"
  - "**/*.hlsl"
---
# Shader 开发规范

## Include 顺序（固定，不可调换）

```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
// 项目自定义 include 放最后
```

## 全屏后处理必须项

- `Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }`
- `Cull Off ZWrite Off ZTest Always`
- `#pragma target 2.0`（Metal 强制）
- 纹理采样用 `SAMPLE_TEXTURE2D_X`，非 `SAMPLE_TEXTURE2D`
- 纹理声明用 `TEXTURE2D_X`，非 `TEXTURE2D`
- 输入纹理名为 `_BlitTexture`，非旧版 `_MainTex`
- Vertex shader 用 Blit.hlsl 提供的 `Vert()`，不手写

## Metal 兼容

- 所有 vertex output 字段必须显式初始化
- `#pragma target 2.0` 必须声明
- 不在 frag shader 中大量使用 `clip()`（会导致 GPU 崩溃）

## 项目约定

- 文件头必须有 `// ═══` 分隔注释块
- Pass 命名用 PascalCase，与功能对应
- 公共函数/结构体提取到 `.hlsl` 文件，不复制粘贴

## ddx/ddy 基底陷阱

`ddx(p)` 是"p 随**屏幕坐标**的变化率"——世界空间输入 ≠ 世界空间导数。`|cross(ddx(p), ddy(p))|` = 屏幕 1 像素在表面上的世界面积，含相机因子 ∝ d²（视距）+ 1/cosθ（掠射角）。

- **症状**：world-space 输入做 ddx/ddy 的 Jacobian 计算，随视距/视角明暗漂移；格状伪影
- **铁律**：同一个 Jacobian 的所有项必须**同基底**。屏幕基底（ddx/ddy）与固定步长差分（世界基底）混用 → 相机因子约不掉
- **屏幕无关的做法**：全部用世界空间固定步长（±eps）差分；场景面采样需把世界偏移重投影回屏幕 UV 采深度
- 详见 memory/2026-08-04-water-caustics-screen-independent.md

## 常见错误诊断

| 症状 | 可能原因 | 诊断 |
|------|---------|------|
| 渲染无效果 | Shader 未绑定 / RenderGraph 纹理未连接 | 检查 Material.SetShader / builder.UseTexture |
| `_BlitTexture` 采样全黑 | 忘记 include Blit.hlsl | 检查 `#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"` |
| Metal 编译失败 | 缺少 `#pragma target 2.0` | 加在 HLSLPROGRAM 内第一行 |
| GPU crash | frag 中大量 `clip()` | Metal 上避免，用 alpha 替代 |
