# Screen-Space PCSS 技术文档

> 劫持 `_ScreenSpaceShadowmapTexture` · 零侵入 · 接触硬化软阴影

---

## 架构

```
Unity CSM 管线（不动）
  │
  ├── ShadowCaster ──→ _MainLightShadowmapTexture（4 级联深度图）
  │
  ├── 【PCSSFeature — 注入: AfterRenderingShadows】
  │     ├── Compute Shader: 读深度 + CSM → 每像素 PCSS
  │     ├── 输出 → SetGlobalTexture("_ScreenSpaceShadowmapTexture")
  │     └── 关键字: _MAIN_LIGHT_SHADOWS_SCREEN ON
  │
  ├── Forward Pass（所有 Shader 零改动）
  │     └── GetMainLight() → SampleScreenSpaceShadowmap() → PCSS 软阴影
  │
  └── 【PCSSPostPass — 注入: BeforeRenderingTransparents】
        └── 恢复 _MAIN_LIGHT_SHADOWS_CASCADE（透明物体用标准 CSM）
```

## 文件

| 文件 | 职责 |
|------|------|
| `PCSSFeature.cs` | Feature + PCSSPass (Compute + 关键字) + PostPass (恢复) |
| `PCSS.compute` | Compute Shader: 深度重建 → 级联选择 → Blocker Search → PCF |
| `PCSS.shader` | Debug 显示 Shader（灰度输出） |

## 参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `lightSize` | 1.0 | 光源角直径，越大半影越宽 |
| `blockerSamples` | 16 | Blocker 搜索采样数 |
| `blockerSearchRadius` | 8 | 搜索半径（UV 像素） |
| `pcfSamples` | 16 | PCF 采样数 |
| `softness` | 1.0 | 柔化倍数 |

## 注入点

- PCSSPass: `AfterRenderingShadows`（Unity CSM 已生成，可采样）
- PCSSPostPass: `BeforeRenderingTransparents`

## 关键字

| 阶段 | _MAIN_LIGHT_SHADOWS | _CASCADE | _SCREEN |
|------|:---:|:---:|:---:|
| 默认 | OFF | ON | OFF |
| PCSSPass 后 | OFF | OFF | ON |
| PostPass 后（透明） | OFF | ON | OFF |

## 扩展

- V2: Shadowmask 分级 (1/4 res tiles, 跳过大面积全影/全亮)
- V3: 保边 PCF (Normal/Color cross-bilateral)
- V4: 时序复用
