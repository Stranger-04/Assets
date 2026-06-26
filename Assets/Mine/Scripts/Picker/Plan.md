# Picker — GPU Picker + Screen‑Space Outline 计划

## 概览

```
┌─────────────────────────────────────────────────────────────────┐
│                        Picker 系统                               │
│                                                                  │
│   Part A: GPU Picker              Part B: Outline Drawer         │
│   ┌──────────────────┐            ┌──────────────────┐          │
│   │ MRT Render Pass   │            │ Mask Render Pass  │          │
│   │  → ObjID RT       │            │  → OutlineMask RT │          │
│   │  → Depth RT       │            │                    │          │
│   │  → Normal RT      │            │ Composite Pass     │          │
│   │                    │            │  → 四邻采样描边    │          │
│   │ Readback Pass      │            │  → Blit to Screen │          │
│   │  → 单像素匹配      │            └──────────────────┘          │
│   │  → 输出 ObjID     │                                           │
│   └──────────────────┘                                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Part A — GPU Picker（屏幕空间点击选物）

### A.1 整体流程

```
鼠标点击/悬停
    │
    ▼
┌──────────────────────────────────────────────┐
│  Step 1: MRT Pass（每帧 / 按需）              │
│                                              │
│  用 Replacement Shader 批量绘制所有可选物体    │
│  输出 3 张 RT（MRT）：                        │
│    • RT0: ObjectID   (R8_UInt 或 R32_UInt)   │
│    • RT1: Depth      (R32_Float)             │
│    • RT2: Normal     (RGB8_UNorm)            │
│                                              │
│  绘制方式：cmd.DrawMesh / DrawRenderers       │
│           每个物体的 ID 通过 MaterialPropertyBlock 传入 │
└──────────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────────┐
│  Step 2: Readback Pass                       │
│                                              │
│  用 Camera 渲染鼠标所在 1×1 像素到一个小 RT    │
│  （或直接用 ComputeShader / AsyncGPUReadback  │
│   读回 MRT 中鼠标坐标处的 ID 值）             │
│                                              │
│  匹配逻辑：                                   │
│    pixelCoord = mousePosition                │
│    id = ObjID_RT[pixelCoord]                 │
│    if id != 0 → 命中物体 id                   │
└──────────────────────────────────────────────┘
    │
    ▼
  输出: selectedObjectID (int)
```

### A.2 需要创建的文件

| 文件 | 作用 |
|---|---|
| `Picker.shader` | MRT 输出 Shader（包含 ObjectID / Depth / Normal 三个 Pass，或一个 MRT Pass） |
| `PickerPass.cs` | RenderGraph RenderPass，负责 MRT 绘制 |
| `PickerFeature.cs` | URP RendererFeature，注册 PickerPass |
| `PickerReadback.cs` | MonoBehaviour，处理鼠标输入 + AsyncGPUReadback 读回 ID |

### A.3 MRT Shader 设计

```
Pass "MRT"
  → SV_Target0: objectID (uint, 0 = 背景/不可选)
  → SV_Target1: depth (float, linear01)
  → SV_Target2: normal (float3, world space, 编码)
```

**MRT 使用 `SV_Target0` / `SV_Target1` / `SV_Target2` 多 RenderTarget 语义。**

每个可选物体需要一个唯一 ID，通过 `MaterialPropertyBlock.SetInt("_ObjectID", id)` 传入。
Stencil 已有 Stencil Ref=1 的体系（Card），可以复用 Stencil 过滤或新开一套。

### A.4 GPU Readback 策略

使用 `AsyncGPUReadback.Request` 读取鼠标坐标处 1 像素：

```csharp
// 鼠标坐标 → 像素坐标
Vector2 pixelCoord = Input.mousePosition;
// 若 MRT_RT 尺寸与屏幕一致，直接采样该坐标
AsyncGPUReadback.Request(MRT_RT, 0, (int)pixelCoord.x, (int)pixelCoord.y, 1, 1, 0, TextureFormat.R8, callback);
```

回调中解析 `objectID`，更新 `selectedObjectID`。

---

## Part B — Outline Drawer（选中物体屏幕空间描边）

### B.1 整体流程

```
selectedObjectID (来自 Part A)
    │
    ▼
┌──────────────────────────────────────────────┐
│  Step 1: Mask Pass                           │
│                                              │
│  用 Replacement Shader 只绘制选中物体          │
│  输出到 R8 RT：                               │
│    • 物体区域 = 1                             │
│    • 背景     = 0                             │
│                                              │
│  通过 MaterialPropertyBlock 传入 _SelectedID  │
│  Frag 中 if (id == _SelectedID) discard;     │
│  输出 float4(1,0,0,0) → RT 中 = 1            │
└──────────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────────┐
│  Step 2: Outline Composite Pass（全屏后处理） │
│                                              │
│  输入：_BlitTexture = MaskRT, _CameraColorTexture │
│                                              │
│  Frag 逻辑：                                  │
│    center = sample(MaskRT, uv)               │
│    if center > 0.5:                          │
│        // 物体内部，直接输出原色               │
│        return cameraColor                    │
│                                              │
│    // 非物体区域，检测四邻                     │
│    up    = sample(MaskRT, uv + (0,  texelSize.y)) │
│    down  = sample(MaskRT, uv + (0, -texelSize.y)) │
│    left  = sample(MaskRT, uv + (-texelSize.x, 0)) │
│    right = sample(MaskRT, uv + ( texelSize.x, 0)) │
│                                              │
│    edge = up + down + left + right            │
│    if edge > 0:                              │
│        return _OutlineColor                  │
│    else:                                     │
│        return cameraColor                    │
│                                              │
│  texelSize = 1.0 / RT尺寸                     │
│  描边宽度通过 _OutlineWidth 控制（乘 texelSize）│
└──────────────────────────────────────────────┘
    │
    ▼
  输出到 CameraColorTarget（屏幕）
```

### B.2 需要创建的文件

| 文件 | 作用 |
|---|---|
| `OutlineMask.shader` | Replacement Shader，只绘制选中物体 → Mask RT（0/1） |
| `OutlineComposite.shader` | 全屏后处理 Shader，四邻采样描边 + 混合 |
| `OutlinePass.cs` | RenderGraph Pass：Mask 绘制 + Composite Blit |
| `OutlineFeature.cs` | URP RendererFeature，注册 OutlinePass |

### B.3 OutlineComposite Shader 关键点（Unity 6）

- 使用 `Blit.hlsl` 的 `Vert` + `_BlitTexture`
- `_BlitTexture` 实际上是 MaskRT（通过 `MaterialPropertyBlock` 设置）
- Camera Color 通过 `cmd.SetGlobalTexture("_CameraColorTexture", ...)` 传入
- Pass 中显式设置 `ZWrite Off / ZTest Always / Cull Off`
- 描边颜色、宽度通过 `CBUFFER` 或 `MaterialPropertyBlock` 传入

### B.4 描边变体扩展

基础四邻采样之外，可扩展：

| 变体 | 描述 |
|---|---|
| **八邻采样** | 加入对角线方向，描边更圆润 |
| **Sobel** | 3×3 Sobel 算子，边缘更连续 |
| **Jump Flood** | 多 Pass 距离场，支持可变宽度描边 |
| **Soft Outline** | 用 smoothstep 替代硬边，抗锯齿描边 |

---

## 实现顺序

```
Phase 1: GPU Picker
  ├── 1.1 创建 Picker.shader（MRT Pass）
  ├── 1.2 创建 PickerPass.cs + PickerFeature.cs
  ├── 1.3 创建 PickerReadback.cs（鼠标输入 + AsyncGPUReadback）
  └── 1.4 测试：点击物体能在 Console 输出 ID

Phase 2: Outline Drawer
  ├── 2.1 创建 OutlineMask.shader（Replacement，Mask RT）
  ├── 2.2 创建 OutlineComposite.shader（全屏后处理，四邻采样）
  ├── 2.3 创建 OutlinePass.cs + OutlineFeature.cs
  └── 2.4 测试：选中物体显示描边

Phase 3: 整合 + 优化
  ├── 3.1 两者联动（Picker 选中 → Outline 显示）
  ├── 3.2 只在鼠标按下时触发 Picker（而非每帧）
  ├── 3.3 添加描边颜色 / 宽度可调参数
  └── 3.4 性能优化（MRT 仅在需要时重建）
```

---

## RenderGraph Pass 结构参考（Unity 6 URP 17+）

```csharp
class PickerPass : ScriptableRenderPass
{
    // PassData 类
    class PassData { /* TextureHandle, Material, ... */ }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData   = frameData.Get<CameraData>();

        // 创建临时 RT
        var objIDDesc = new RenderTextureDescriptor(cameraData.cameraTargetDescriptor)
        {
            colorFormat     = RenderTextureFormat.R8,
            depthBufferBits = 0
        };
        var objIDHandle = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, objIDDesc, "_PickerObjID", false);

        // UnsafePass 绘制
        renderGraph.AddUnsafePass("Picker MRT", new PassData { ... },
            (passData, cmd, ctx) =>
            {
                // cmd.DrawRenderers(...)
            });

        // Blit 需要 Blitter
        Blitter.BlitCameraTexture(cmd, source, dest, material, pass);
    }
}
```

---

## 关键注意事项

1. **MRT 的 RT 格式要一致**：三个 RT 尺寸必须相同（通常 = 屏幕分辨率）
2. **AsyncGPUReadback 延迟**：Request 在 1-2 帧后回调，鼠标快速移动时需处理过期
3. **Replacement Shader**：MRT Pass 和 Mask Pass 都可能用到，注意 ShaderTagId 匹配
4. **Unity 6 全屏后处理**：OutlineComposite 必须遵循 `Blit.hlsl` 规范（`_BlitTexture`、`Vert`、`texcoord`）
5. **Stencil 协同**：现有 Card 使用 Stencil Ref=1，Picker 绘制时注意 Stencil 状态不要冲突
6. **深度测试**：MRT 绘制时需要 ZTest LEqual，确保遮挡关系正确
