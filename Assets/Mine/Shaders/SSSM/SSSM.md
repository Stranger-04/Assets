# SSSM（Screen Space Shadow Maps）— 使用说明

## 概述

`SSSM.shader` 实现了**屏幕空间阴影追踪**，采用 **DDA 2D**（Digital Differential Analyzer）算法沿主光源方向在屏幕空间步进，利用 Depth Buffer 检测遮挡物。

与 Unity URP 内置的 Cascaded Shadow Maps（CSM）互补：
- **CSM**：全场景大尺度阴影（光源空间），擅长远距离但锯齿明显
- **SSSM**：视野内接触阴影（屏幕空间），分辨率原生但只覆盖屏幕可见区域

**核心思路**：从屏幕每像素出发，沿"表面→光源"方向步进，将步进点投影到屏幕 UV 并采样场景深度。采用**双空间联合判定**（相机空间 + 光源空间）来避免假阳性：

- **相机空间**：光线深度 > 场景深度 + `_Thickness` → 同一屏幕 UV 上有表面挡在光线前面（必要不充分）
- **光源空间**：该遮挡物与光线点在光源空间内距离接近（同一光柱 + Z 排序正确）→ 确认遮挡物确实在光路上

两重过滤后的遮挡才判定为阴影，遵循"宁少勿多"原则。

```
         Camera
           │
           ▼  ┌─────────────┐
              │  遮挡物体    │  ← 更靠近相机
              │  (Occluder)  │
              └─────────────┘
           ╱
    ┌─────╱──────┐
    │ 当前像素    │────────→ 光源方向
    │ (Receiver) │  步进路径
    └────────────┘
```

## Pass 管线总览

| Index | Name | 功能 |
|:-----:|------|------|
| **0** | SSSM_RayMarch | DDA 2D 雷步进 → 输出阴影遮罩（R: shadow, G: occluder depth） |
| **1** | SSSM_BlurHorizontal | 水平 5-tap Gaussian 模糊（可选） |
| **2** | SSSM_BlurVertical | 垂直 5-tap Gaussian 模糊（可选） |

**管线流程**（C# `SSSMFeature.cs` 控制）：

```
(无模糊)
source → [RayMarch] → shadowRT → 设置全局纹理 _SSSM_ShadowMask
                                   └─ SSSMFeature=ON: 显示shadowRT → source (debug)

(有模糊)
source → [RayMarch] → shadowRT → [BlurH] → blurRT → [BlurV] → shadowRT → 设置全局纹理 _SSSM_ShadowMask
                                                                          └─ SSSMFeature=ON: 显示shadowRT → source (debug)
```

**关键设计**：
- **始终输出**阴影遮罩到全局纹理 `_SSSM_ShadowMask`，其他 Shader 按需采样
- **SSSMFeature 开关**控制是否在屏幕上显示阴影图（调试用）
- 关闭时不修改场景颜色，仅生成阴影图供消耗

---

## 参数说明

### Ray Marching 参数

#### Step Size — 步长倍率

| 范围 | 默认 | 作用 |
|:----:|:----:|------|
| 0.1 – 2.0 | 0.5 | DDA 步幅的缩放系数 |

| 步长 | 效果 |
|:----:|------|
| 较小 (0.1–0.3) | 步进密集，命中检测更精确，但最大覆盖距离缩短 |
| 中等 (0.5) | 默认平衡 |
| 较大 (1.0–2.0) | 步进稀疏，覆盖更远但可能遗漏薄遮挡物 |

**实际步幅** = `(endPoint - startPoint) / StepCount × StepSize`

---

#### Max Distance — 最大追踪距离

| 范围 | 默认 | 作用 |
|:----:|:----:|------|
| 1 – 200 | 50.0 | 沿光源方向的最大追踪距离（世界单位） |

| 距离 | 效果 |
|:----:|------|
| 较小 (5–20) | 只捕捉近处遮挡，性能开销小 |
| 中等 (50) | 默认平衡 |
| 较大 (100–200) | 捕捉远处遮挡，但走出屏幕范围后终止 |

> 光线超出屏幕边界后自动终止（走出屏幕 = 无深度信息可检测）。

---

#### Step Count — 步数

| 范围 | 默认 | 作用 |
|:----:|:----:|------|
| 4 – 128 | 32 | 最大步进次数 |

| 步数 | 效果 |
|:----:|------|
| 较少 (4–16) | 性能最快，但容易漏检或产生条带伪影 |
| 中等 (32) | 默认平衡 |
| 较多 (64–128) | 更精确，逐帧开销随步数线性增长 |

**性能建议**：
- 移动端：16–24 步
- PC：32–64 步
- 配合 Jitter 时，较低步数 + 模糊也可接受

---

#### Thickness — 厚度阈值（Bias）

| 范围 | 默认 | 作用 |
|:----:|:----:|------|
| 0.001 – 0.5 | 0.05 | 避免自遮挡的深度偏差（Eye Space） |

| 厚度 | 效果 |
|:----:|------|
| 较小 (0.001–0.01) | 自遮挡伪影（Shadow Acne）风险大 |
| 中等 (0.05) | 默认平衡 |
| 较大 (0.1–0.5) | 避免自遮挡但可能漏掉薄遮挡物 |

> 区别于 CSM 的 Shadow Bias（在光源空间操作），这里的 _Thickness 在眼空间（Eye Space）操作，物理含义是"光线需要穿过多厚的遮挡物才算被挡住"。

---

#### Light Ray Thickness — 光柱厚度

| 范围 | 默认 | 作用 |
|:----:|:----:|------|
| 0.01 – 5.0 | 0.5 | 光源空间验证中光柱的厚度容差（世界单位） |

| 厚度 | 效果 |
|:----:|------|
| 极小 (0.01–0.1) | 极为严格的几何验证，几乎只有精确命中光线的遮挡物才产生阴影（阴影偏少） |
| 中等 (0.5) | 默认平衡，允许轻微偏离光轴的遮挡物 |
| 较大 (1.0–5.0) | 宽容的验证，接近纯相机空间判定，假阳性风险增大 |

**物理含义**：定义遮挡物可以偏离光线轴多远仍被接受为"在光路上"。该值越大，越接近纯相机空间判定（回归假阳性问题）；越小，阴影判定越保守（"宁少勿多"）。

---

### Debug 参数

#### SSSMFeature — 显示阴影遮罩

| 默认 | 作用 |
|:----:|------|
| true | ON 时在屏幕显示原始阴影遮罩，OFF 时不修改场景画面 |

- **ON**（调试模式）：阴影图以黑白图像显示（白=照亮，黑=阴影）
- **OFF**（生产模式）：只在后台生成 `_SSSM_ShadowMask` 全局纹理，供其他 Shader 使用

---

### Blur 参数

#### Enable Blur — 启用模糊软化

| 默认 | 作用 |
|:----:|------|
| false | 是否启用 Pass 1 + 2 对阴影遮罩做双边模糊 |

#### Blur Scale — 模糊半径

| 范围 | 默认 | 作用 |
|:----:|:----:|------|
| 0.0 – 5.0 | 1.0 | 模糊采样间距的缩放系数 |

| 模糊半径 | 效果 |
|:--------:|------|
| 0.0 | 无模糊，保留 DDA 硬边缘 |
| 1.0 | 轻度软化，消除锯齿 |
| 2.0–5.0 | 强烈柔化，适合低步数下的降噪 |

> **注意**：当前模糊使用 5-tap Gaussian 核（`BlurFunction.hlsl`），非边缘保持滤波。在深度/法线不连续处可能出现阴影渗漏（light leaking）。后续可升级为**双边模糊**或**PCSS 自适应滤波**。

---

## 在其他 Shader 中使用

在需要阴影的 Shader 中采样 `_SSSM_ShadowMask` 全局纹理即可：

```hlsl
TEXTURE2D_X(_SSSM_ShadowMask);

// 读取场景颜色与阴影
float3 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
float  shadow     = SAMPLE_TEXTURE2D_X(_SSSM_ShadowMask, sampler_LinearClamp, uv).r;

// 应用阴影
float shadowFactor = lerp(1.0 - _ShadowIntensity, 1.0, shadow);
return half4(sceneColor * shadowFactor, 1.0);
```

---

## 与 CSM 结合方案

`_SSSM_ShadowMask` 的 R 通道存储 shadow factor（0=阴影，1=照亮），可直接与 CSM 混合：

```hlsl
// 混合 CSM + SSSM
float cascadeShadow = MainLightRealtimeShadow(shadowCoord);
float ssShadow      = SAMPLE_TEXTURE2D_X(_SSSM_ShadowMask, sampler_LinearClamp, uv).r;

// 方案 A：最小值合并（保守，最准确）
float finalShadow = min(cascadeShadow, ssShadow);

// 方案 B：按距离 lerp（近距离信任 SSSM，远距离信任 CSM）
// float dist = LinearEyeDepth(rawDepth);
// float blend = saturate((dist - 5.0) / 30.0);
// float finalShadow = lerp(ssShadow, cascadeShadow, blend);
```

---

## 算法对比

| 维度 | CSM (Cascade) | SSR 反射追踪 | SSSM (本方案) |
|------|:-------------:|:------------:|:-------------:|
| 坐标系 | 光源空间 | 屏幕空间 | 屏幕空间 |
| 深度来源 | Shadow Map | Depth Buffer | Depth Buffer |
| 步进方向 | — | 反射方向 | **光源方向** |
| 检测目标 | 可见性查询 | 第一个命中点 | **任何遮挡物** |
| 覆盖范围 | 全场景 | 屏幕可见 | 屏幕可见 |
| 软化方式 | PCF/PCSS/VSM | Blur | Blur（逐步升级） |
| 漏光风险 | 低（Cascade 兜底） | 高 | 中 |

---

## 性能分析

| 配置 | 步数 | 模糊 | 相对开销 |
|:----:|:----:|:----:|:--------:|
| 性能模式 | 16 | off | 1×（基准） |
| 均衡模式 | 32 | off | 2× |
| 高质量模式 | 64 | on | 4–5× |
| 参考（SSR DDA 2D） | 64 + Binary 6 | on | ~8× |

**优化建议**：
1. 优先降低 Step Count — 性能随步数线性增长
2. 开启 Blur 可以用较少步数 + 模糊达到接近高步数的质量
3. Jitter 本身不增加步数开销，但单帧有噪声，需配合 TAA 或 temporal 累积

---

## 未来方向

- [ ] **PCSS 自适应滤波**：利用 Pass 0 输出的 G 通道（`avgOccluderDepth` 已预留），计算半影大小 → 自适应模糊半径
- [ ] **双边模糊**：在 Blur Pass 中加入 depth/normal edge-stopping，消除阴影渗漏
- [ ] **Temporal 累积**：跨帧复用 Jitter 结果，单帧步数可降至 8–16
- [ ] **Hi-Z 加速**：复用 SSR 的 Hi-Z 层级结构，大步跳过空白区域
- [ ] **CSM 深度结合**：Ray March 时融合 CSM 信息，超出屏幕时回退到 CSM
