# Reference Templates

> 项目中验证通过的完整 shader / compute / C# 模板。
> 这些文件是实际可运行的代码，带有详细注释说明每个关键决策的原因。

---

## 索引

| 文件 | 内容 | 对应 note |
|------|------|----------|
| [post-process-template.shader](post-process-template.shader) | Unity 6 全屏后处理 shader 模板 | urp-shader-lib/blit-fullscreen.md |
| [compute-template.compute](compute-template.compute) | Compute Shader 模板 (Metal 兼容) | urp-shader-lib/compute-shader.md |
| [render-pass-template.cs](render-pass-template.cs) | C# RenderGraph Pass 模板 | unity6-api/render-graph.md |
| [volume-template.cs](volume-template.cs) | VolumeComponent 模板 | unity6-api/volume-component.md |

## 使用方式

从模板复制 → 修改 pass name 和参数 → 按注释中的步骤定制。
每个模板中的 `⚠️` 标记表示必须根据项目调整的位置。
