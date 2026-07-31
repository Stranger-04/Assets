---
paths:
  - "Assets/Mine/Scripts/**"
  - "**/*Feature.cs"
  - "**/*Pass.cs"
---
# C# RenderPass 开发规范

## Unity 6 必须项

- 主入口用 `RecordRenderGraph(RenderGraph, ContextContainer)`，不用旧版 `Execute()`
- Blit 用 `Blitter.BlitTexture()`，不用 `cmd.Blit()`
- 临时 RT 由 RenderGraph 自动管理，不手动 `GetTemporary()`
- 资源通过 `frameData.Get<UniversalResourceData>()` 获取

## PassData

- 每个 pass 必须定义独立的 `class PassData { }`
- PassData 中存放 material、texture handles、参数

## VolumeComponent

- 用 `[VolumeComponentMenuForRenderPipeline]`，非旧版 `[VolumeComponentMenu]`
- 实现 `IPostProcessComponent` 接口
- `IsTileCompatible() => false`（后处理通常不支持分块）

## 项目约定

- Feature 文件放在 `Assets/Mine/Scripts/` 下对应效果目录
- 错误处理：同一个错误 3 次后兜底退出，不无限重试

## 常见错误诊断

| 错误码 | 含义 | 诊断 |
|--------|------|------|
| `CS0246` | 找不到类型 | 检查 using / 命名空间引用 |
| `CS0103` | 名称不存在 | 检查变量/方法声明拼写 |
| `CS1061` | 类型不包含方法 | 检查 API 是否 Unity 6 版本 |
| `NullReferenceException` | 空引用 | 检查序列化 / domain reload 后初始化 |
| `FindKernel` 返回 -1 | kernel 名不匹配 | 检查 compute 文件中 `#pragma kernel` 声明 |
- 错误处理：同一个错误 3 次后兜底退出，不无限重试
