# FGDLutBaker — FGD LUT 预积分 BRDF 烘焙工具

**路径:** `Assets/Mine/Scripts/FGDLutBaker/`
**类型:** 静态工具类 + Editor Window
**依赖:** Unity 6 URP, `ImageBasedLighting.hlsl` (URP Core 包)

---

## 功能概述

将 GGX BRDF 菲涅尔/几何遮蔽/法线分布项 + Disney Diffuse 的半球积分预烘焙到一张 2D LUT。

运行时配合 [ENVFunction.hlsl](../../Special/HLSL/ENVFunction.hlsl) 的 `BRDF_Env` 使用：设置全局 LUT 后自动启用 FGD 裂项近似，未设置时回退 Karis 分析近似。

---

## 文件清单

| 文件 | 职责 |
|------|------|
| `FGDLutBaker.cs` | 静态工具类：`Bake()` / `SetGlobalLut()` / `ClearGlobalLut()` / `LogDiagnostics()` |
| `FGDPacker.shader` | Hidden/Mine/FGDPacker — 逐像素调用 `IntegrateGGXAndDisneyDiffuseFGD` |
| `FGDLutBaker.md` | 本文档 |

### Editor

| 文件 | 职责 |
|------|------|
| `Assets/Editor/FGDLutBakerWindow.cs` | 可视化面板：参数调节、烘焙/载入/保存、全局 LUT 管理、预览 |

---

## 公共 API

```csharp
// 烘焙（GPU pixel shader → Readback）
Texture2D lut = FGDLutBaker.Bake(resolution: 128, sampleCount: 1024);

// 设为全局 LUT → 所有 ENVFunction shader 自动启用 FGD 路径
FGDLutBaker.SetGlobalLut(lut);

// 清除 → 回退 Karis 分析近似
FGDLutBaker.ClearGlobalLut();

// 打印四角像素值
FGDLutBaker.LogDiagnostics(lut);
```

---

## LUT 通道布局

| 通道 | 存储值 | 运行时解码 | 说明 |
|------|--------|-----------|------|
| R | scale（F₀=0 时的积分） | `(1−F₀) × R` | Schlick Fresnel bias |
| G | bias（F₀=1 时的积分） | `F₀ × G` | Schlick Fresnel scale |
| B | diffuse − 0.5 | `B + 0.5` | Disney Diffuse 响应 |
| A | 未使用 | — | 保留 |

---

## Editor 窗口

菜单栏 `Tools → FGD Lut Baker...`

| 控件 | 说明 |
|------|------|
| Resolution / Sample Count | 烘焙参数 |
| Save Path | 保存 .asset 路径 |
| Load Path | 载入已保存的 LUT 路径 |
| Bake | GPU 烘焙新 LUT |
| Load | 从 Load Path 载入 |
| Save | 保存到 Save Path |
| Set LUT | 设为全局纹理 |
| Clear LUT | 清除全局纹理 |
| Preview | 当前纹理预览 |

---

## ENVFunction 集成

[ENVFunction.hlsl](../../Special/HLSL/ENVFunction.hlsl) 的 `BRDF_Env` 通过全局浮点数 `_UseFGDLut` 自动选择路径：

```
_UseFGDLut = 0（默认）→ Karis 2013 分析近似（移动端友好）
_UseFGDLut = 1        → FGD 裂项近似（更精确的 Fresnel/Geometry/Diffuse）
```

调用方无需任何修改 — 签名与原始 `BRDF_Env` 完全兼容。

---

## 性能

| 指标 | 128² + 1024spl | 256² + 4096spl |
|------|-----------------|-----------------|
| GPU 时间 | ~0.1s | ~1.5s |
| 内存 | 128KB (RGBAHalf) | 512KB |

烘焙为一次性 Editor 操作，不在运行时执行。

---

## 已知限制

- 依赖 `ImageBasedLighting.hlsl`（URP Core 包），仅 Unity 6 URP 17+ 验证
- 使用 `GetDimensions` + `_UseFGDLut` toggle 进行 LUT 检测
- Metal 平台已验证
