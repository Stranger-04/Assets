# knowledge — 知识库预加载

> 从 `Assets/MarkDowns/` 加载项目知识库，提取关键约束注入上下文。

---

## 预加载逻辑

```
AutoMode 激活
  │
  ├── [0.1] 扫描 Assets/MarkDowns/*.md
  │     └── ls Assets/MarkDowns/*.md
  │
  ├── [0.2] 按优先级读取
  │     ├── 高优先级（必读）：结构规范类 → ScriptStructure.md, ShaderStructure.md
  │     ├── 中优先级（按需）：模板类 → ScriptDocTemplate.md, ShaderDocTemplate.md
  │     └── 低优先级（相关时读）：领域知识类 → Unity 6 全屏后处理 Shader 差异.md 等
  │
  └── [0.3] 提取关键约束
        ├── 代码风格规范（命名、注释格式、文件结构）
        ├── Shader 语法差异（Unity 6 vs 旧版本的 API 变化）
        └── 模板要求（新建文件时应遵循的文档格式）
```

## 读取策略

| 条件 | 行为 |
|------|------|
| MarkDowns 文件夹不存在 | 跳过，不报错 |
| 文件已在当前会话中读过 | 跳过（避免重复消耗 context） |
| 文件 > 500 行 | 先读前 100 行确认内容，再决定是否全读 |
| 用户指令明确涉及 Shader | 必读 ShaderStructure.md + ShaderDocTemplate.md |
| 用户指令明确涉及 C# 脚本 | 必读 ScriptStructure.md + ScriptDocTemplate.md |
| 用户指令涉及全屏后处理 | 必读 Unity 6 全屏后处理 Shader 差异.md |

## 约束应用

读取完毕后，后续所有代码生成和修改必须遵循 MarkDowns 中定义的规范：

1. **脚本结构**：遵循 `ScriptStructure.md` 中的文件组织方式
2. **Shader 结构**：遵循 `ShaderStructure.md` 中的语法和 API 用法（特别注意 Unity 6 差异）
3. **文档模板**：新增文件时按 `ScriptDocTemplate.md` / `ShaderDocTemplate.md` 格式添加头部注释

## 参考实现跟进 ⚠️

MarkDowns 规范文件头部可能声明了参考实现。**如果当前任务的目标文件类型匹配参考实现的类型，必须读取至少 1 个参考实现文件。**

```
读取 MarkDowns
  │
  ├── 检查文件头部是否有 "> 参考实现：" 块
  │     │
  │     ├── 有 → 匹配任务类型 → 读取同类型参考
  │     │     ├── 全屏后处理 Shader → 读 SSSM.shader 或 SSO.shader
  │     │     ├── 普通 Shader → 读 PBRToon.shader
  │     │     └── C# Feature → 读 SSSMFeature.cs 或 SSOFeature.cs
  │     │
  │     └── 没有 → 跳过
  │
  └── 目的：规范是抽象规则，参考实现是具体范例。两者结合才能正确理解"好的代码应该长什么样"。
```

## .claude/references/ — Harness 参考库

> `.claude/references/` 是 Harness 的内置参考库。MD 文件为目录索引，实际内容（.hlsl / .shader / .compute / .cs）带有详细注释。
> 所有 Shader / C# 开发必须优先查阅此目录。

```
写 Shader / Compute
  │
  ├── [R1] 先读 references/urp-shader-lib/README.md（索引）
  │     ├── blit-fullscreen.md        → Unity 6 Blitter 全屏 Shader 模式
  │     ├── hlsl-includes.md          → include + CBUFFER 速查
  │     ├── blit-fullscreen.md         → Unity 6 vs 2022 差异
  │     └── compute-shader.md          → Compute Shader + Metal
  │
  ├── [R2] 复制模板: templates/
  │     ├── fullscreen-postprocess.shader  → 全屏后处理 (基于 Blit.hlsl)
  │     ├── compute-template.compute       → Compute Shader
  │     └── urp-renderpass.cs              → C# RenderGraph Pass
  │
  └── [R3] 抄 API: references/unity6-api/
        ├── render-graph.md           → RecordRenderGraph 标准写法
        ├── blitter-api.md            → Blitter.BlitTexture API
        ├── compute-shader-api.md     → ComputeShader C# dispatch
        ├── rthandle-api.md           → RTHandle 生命周期
        └── volume-component.md       → VolumeComponent 参数定义
```

### 查阅策略

| 条件 | 行为 |
|------|------|
| 写全屏后处理 Shader | 必读 `blit-fullscreen.md` + 复制模板 `fullscreen-postprocess.shader` |
| 写 Compute Shader | 必读 `compute-shader.md` + 复制模板 `compute-template.compute` |
| 写 C# RenderGraph Pass | 必读 `render-graph.md` + 复制模板 `urp-renderpass.cs` |
| Metal 平台问题 | 查阅 `platform/metal-notes.md` |
| API 签名不确定 | 查阅 `unity6-api/` 子目录，不凭记忆 |

### references + templates vs assets/MarkDowns/

| 维度 | references + templates (Harness 层) | MarkDowns/ (项目知识层) |
|------|-------------------------------------|------------------------|
| 维护者 | Harness (跟随引擎版本) | 用户 (项目策略) |
| 内容 | 引擎 API 速查 + 平台差异 + 可运行模板 | 代码风格、命名规范、文档格式 |
| 更新频率 | 引擎升级时 | 项目策略调整时 |
| 确定性 | 高 — 这是"怎么写才对" | 中 — 这是"怎么写更好" |

---

## 模式差异

| 模式 | 知识加载策略 |
|------|-------------|
| 研发模式 | 按需加载（涉及 Shader → 读 Shader 相关；涉及 C# → 读 Script 相关） |
| 生产模式 | 全量预加载（自动扫描并读取所有高优先级文件） |
