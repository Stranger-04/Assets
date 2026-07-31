---
name: unity6-postprocessing-shader-differences
description: Unity 6 URP 17+ 全屏后处理 shader 与 Unity 2022 的所有关键差异总结
metadata: 
  node_type: memory
  type: reference
  originSessionId: dbf8ebc6-59e1-48b5-a4ad-3d8c27c51e75
---

# Unity 6 URP 17+ 全屏后处理 Shader 差异总结

以下基于将旧版 Kuwahara 后处理迁移到 Unity 6 的实战经验，对比新管线与 Unity 2022 URP 的所有关键差异。

---

## 1. Vertex Shader — 全屏绘制方式彻底改变

| Unity 2022 (旧) | Unity 6 (新) |
|---|---|
| 用自定义 `Vert`，接受 `POSITION` 语义，通过 `TransformObjectToHClip(input.positionOS)` 将对象空间顶点变换到裁剪空间 | 用 `Blit.hlsl` 提供的 `Vert`，接受 `SV_VertexID` 语义，通过 `GetFullScreenTriangleVertexPosition(input.vertexID)` 直接生成全屏三角形 |

```hlsl
// 旧（不再工作）
struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
Varyings Vert(Attributes input) {
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
}

// 新（必须使用 Blit.hlsl）
// 不需要自定义 Attributes/Varyings/Vert，Blit.hlsl 已经提供
// #include "Blit.hlsl" 即可
// 然后 #pragma vertex Vert
```

**原因**：`Blitter.BlitCameraTexture` 内部用 `DrawTriangle` 绘制一个**全屏三角形**（3 个顶点，通过 `SV_VertexID` 索引），而不是旧版 `cmd.Blit` 的 4 顶点全屏四边形。旧的 `POSITION` 语义无法匹配这个绘制方式。

---

## 2. 纹理绑定 — `_MainTex` → `_BlitTexture`

| Unity 2022 (旧) | Unity 6 (新) |
|---|---|
| `cmd.Blit(source, dest, material)` 自动将 source 设置为 `_MainTex` | `Blitter.BlitCameraTexture(cmd, source, dest, material, pass)` 通过 `MaterialPropertyBlock` 将 source 设置为 `_BlitTexture` |

```hlsl
// 旧
TEXTURE2D_X(_MainTex);
float3 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv).rgb;

// 新
// Blit.hlsl 已经声明 TEXTURE2D_X(_BlitTexture);
float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
```

**例外**：如果需要在 shader 中使用 `_MainTex`（如 SSO 的 Composite Pass），必须在 C# 中**手动设置全局纹理**：
```csharp
cmd.SetGlobalTexture("_MainTex", data.tempMain);
```

---

## 3. 必须包含 Blit.hlsl

旧版 shader 通常只包含 `Core.hlsl` 即可。新管线**必须**包含：

```hlsl
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
```

这个文件提供了 shader 层所需的一切：

| 提供者 | 内容 |
|---|---|
| `TEXTURE2D_X(_BlitTexture)` | 输入纹理声明（第14行） |
| `float4 _BlitScaleBias` | 缩放偏移参数 |
| `Vert(Attributes input)` | 正确的全屏三角形顶点函数（第40行） |
| `Attributes` / `Varyings` | 正确的输入输出结构体 |
| `sampler_LinearClamp` 等 | 通过 `GlobalSamplers.hlsl` 引入的全局采样器 |

---

## 4. Varyings 命名变化

Blit.hlsl 中定义：

```hlsl
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 texcoord   : TEXCOORD0;    // ← 注意是 texcoord，不是 uv
    UNITY_VERTEX_OUTPUT_STEREO
};
```

所以 Fragment 函数中必须用 `input.texcoord` 而不是 `input.uv`：

```hlsl
// 旧
half4 Frag(Varyings input) : SV_Target { float2 uv = input.uv; }

// 新
half4 Frag(Varyings input) : SV_Target { float2 uv = input.texcoord; }
```

---

## 5. Render States 必须显式设置

旧版 `cmd.Blit` 内部会设置渲染状态，新版 `Blitter.BlitCameraTexture` + `DrawTriangle` 则需要在 Pass 中**显式声明**：

```hlsl
Pass
{
    ZWrite Off
    ZTest Always
    Cull Off
    Blend One Zero
    // ...
}
```

**不设置这些会导致深度测试不通过或混合错误**，整个 Pass 输出黑色。

---

## 6. SubShader Tag

建议加上管线标签，避免与 Built-in 渲染管线冲突：

```hlsl
Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
```

---

## 7. C# 侧 — RenderGraph 替代 Execute

| 方面 | Unity 2022 | Unity 6 |
|---|---|---|
| Pass 方法 | `Execute(ScriptableRenderContext, ref RenderingData)` | `RecordRenderGraph(RenderGraph, ContextContainer)` |
| 资源获取 | `renderingData.cameraData.renderer.cameraColorTargetHandle` | `frameData.Get<UniversalResourceData>().activeColorTexture` |
| 临时 RT | `RTHandle` + `ReAllocateHandleIfNeeded` | `TextureHandle` + `UniversalRenderer.CreateRenderGraphTexture` |
| 资源释放 | 手动 `tempRT?.Release()` | RenderGraph 自动管理 |
| Blit 调用 | `cmd.Blit(source, dest, material)` | `Blitter.BlitCameraTexture(cmd, source, dest, material, pass)` |
| 缓冲区分配 | `CommandBufferPool.Get()` | `RenderGraph.AddUnsafePass<T>()` 内通过 `CommandBufferHelpers.GetNativeCommandBuffer()` 获取 |
| 属性传递 | 直接设置 | 通过 `PassData` 类传入 `SetRenderFunc<T>` |

---

## 8. Properties 区块 — 保留但仅为 Inspector 显示

`Properties` 区块中的 `_MainTex` 等属性**不再影响纹理绑定**，仅用于材质 Inspector 面板的 UI 显示。实际纹理绑定由 `Blitter` 的 `MaterialPropertyBlock` 或 `cmd.SetGlobalTexture` 驱动：

```hlsl
Properties
{
    _MainTex ("Base Color", 2D) = "white" {}   // 仅用于 Inspector 显示
    _Radius ("Radius", Range(1,10)) = 5
}
```

---

## 快速检查清单

如果全屏后处理 Pass 输出黑色，按以下顺序排查：

1. [ ] **Vertex Shader**: 使用 `Blit.hlsl` 的 `Vert`（`SV_VertexID` 全屏三角形）？
2. [ ] **纹理名**: 使用 `_BlitTexture` 而非 `_MainTex`？
3. [ ] **包含 Blit.hlsl**: 在 HLSLINCLUDE 中添加了 `#include "Blit.hlsl"`？
4. [ ] **texcoord**: Fragment 中用的是 `input.texcoord` 而非 `input.uv`？
5. [ ] **Render States**: Pass 中设置了 `ZWrite Off, ZTest Always, Cull Off`？
6. [ ] **C# RecordRenderGraph**: 使用了 `Blitter.BlitCameraTexture` 而非 `cmd.Blit`？
7. [ ] **TextureHandle**: 使用了 `TextureHandle` 而非 `RTHandle`？
