# Unity 6 C# API Reference

> Unity 6 URP 17+ 渲染开发常用 C# API。本文件为目录索引。

---

## 索引

> 完整可运行模板：`templates/` 目录。这里只做 API 速查。

| 文件 | 内容 |
|------|------|
| [render-graph.md](render-graph.md) | RenderGraph API 速查 — RecordRenderGraph, AddRasterPass, AddComputePass |
| [blitter-api.md](blitter-api.md) | Blitter.BlitTexture 签名和用法 |
| [compute-shader-api.md](compute-shader-api.md) | ComputeShader C# — FindKernel, Dispatch, SetTexture |
| [rthandle-api.md](rthandle-api.md) | RTHandle 生命周期管理 |
| [volume-component.md](volume-component.md) | VolumeComponent 参数类型速查 |

### 模板文件

| 模板 | 用途 |
|------|------|
| `templates/fullscreen-postprocess.shader` | 全屏后处理 shader（基于 Blit.hlsl） |
| `templates/compute-template.compute` | Compute shader（Metal 兼容） |
| `templates/urp-renderpass.cs` | C# RenderGraph Pass |
| `templates/volume-template.cs` | VolumeComponent |

## 关键版本差异速查

| 功能 | Unity 2022 (旧) | Unity 6 (新) |
|------|----------------|-------------|
| 渲染管线入口 | `ScriptableRenderPass.Execute()` | **`RecordRenderGraph()`** |
| 纹理创建 | `RTHandle.Alloc()` | `RenderingUtils.ReAllocateIfNeeded()` |
| Blit | `cmd.Blit()` | **`Blitter.BlitTexture()`** 或 `Blitter.BlitCameraTexture()` |
| 临时 RT | `RenderTexture.GetTemporary()` | RenderGraph 自动管理（不需要手动获取） |
| Pass 数据传递 | `ref RenderingData` | **`ContextContainer`** |
| 纹理导入 | `renderPass.ConfigureInput()` | `context.GetTexture()` 或 `passData.source` |
