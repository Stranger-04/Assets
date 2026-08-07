# Screen Space 渲染解耦骨架

## 目标

将 SSR / SSGI / SSAO 等屏幕空间算法统一成可替换架构。

## 1. Data Layer 数据层

负责提供屏幕信息。

输入: - Depth Texture - Normal Texture - Color Texture - Material Data -
Motion Vector

不关心算法。

## 2. Control Layer 控制层(C#)

负责: - 创建 RenderTexture - 设置 Shader 参数 - Dispatch / Blit - 管理
RenderPass - 输出全局纹理

例如: \_ScreenEffectTexture

## 3. Compute Layer 计算层

负责实际算法。

输入: - Screen Data - Parameters

输出: - Result Texture

可替换: - SSR Raymarch - SSR DDA - SSR Hi-Z - SSGI Diffuse - AO Search

## 4. Output Layer 输出层

负责接入: - ShaderGraph Sample Texture - Post Processing - Material
Reflection - Lighting Composite

## 推荐数据流

Camera

↓

Render Feature

↓

Prepare Buffers

↓

Screen Algorithm

↓

Result RT

↓

Global Texture

↓

ShaderGraph / Post Effect

## 替换算法原则

保持:

输入接口: - Depth - Normal - Color - Parameters

输出接口: - Color - Alpha/Confidence

只替换 Compute Shader。

## SSR输出建议

RGBA:

RGB: Reflection Color

A: Confidence

例如:

Fresnel * Smoothness * Hit Confidence
