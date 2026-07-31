---
name: fgd-lut-baker
description: FGD LUT 烘焙工具 — GPU pixel shader 预积分 GGX BRDF + Disney Diffuse，配合 ENVFunction.hlsl 使用
metadata:
  type: project
---

# FGD LUT Baker — 2026-07-30

## 做了什么

创建了完整的 FGD LUT 烘焙工具链：

1. **FGDPacker.shader** — Hidden/Mine/FGDPacker，逐像素调用 `IntegrateGGXAndDisneyDiffuseFGD`
2. **FGDLutBaker.cs** — `Mine.FGDLutBaker` 静态工具类：`Bake()` / `SetGlobalLut()` / `ClearGlobalLut()` / `LogDiagnostics()`
3. **FGDLutBakerWindow.cs** — Editor 窗口：`Tools → FGD Lut Baker...`
4. **ENVFunction.hlsl** — 合并 `BRDF_Env` + `BRDF_Env_HD` 为单一函数，`_UseFGDLut` toggle 自动选择 FGD LUT / Karis 分析近似

## 关键技术决策

- **SV_Position 方案**：URP 兼容模式下 vertex shader texcoord 传递不可靠，改用 `positionSS : SV_Position` 在 fragment 中计算 UV
- **CommandBuffer.DrawProcedural**：替代 `Graphics.Blit`，确保全屏三角形正确执行
- **_UseFGDLut toggle**：Unity 给未绑定 `TEXTURE2D` 的默认纹理尺寸不稳定，弃用 `GetDimensions` 检测，改用显式全局浮点数

## 调用方影响

Water.shader、PBRToon.shader 无需任何修改 — `BRDF_Env` 签名完全兼容。

**Why:** 项目需要 FGD LUT 来替代 Karis 分析近似，获得更精确的环境光镜面反射。同时需要自动回退机制保证移动端兼容。

**How to apply:** Editor 菜单 `Tools → FGD Lut Baker...` → Bake → Set LUT；或代码 `FGDLutBaker.Bake()` + `FGDLutBaker.SetGlobalLut()`。
