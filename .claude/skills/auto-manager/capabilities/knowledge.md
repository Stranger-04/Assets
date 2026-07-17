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

## 模式差异

| 模式 | 知识加载策略 |
|------|-------------|
| 研发模式 | 按需加载（涉及 Shader → 读 Shader 相关；涉及 C# → 读 Script 相关） |
| 生产模式 | 全量预加载（自动扫描并读取所有高优先级文件） |
