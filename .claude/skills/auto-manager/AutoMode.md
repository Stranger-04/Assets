# AutoMode

> Unity 开发任务自适应系统的唯一真相源（Single Source of Truth）。
> 定义模式选择逻辑、Editor 可用性策略、以及完整文件索引。

---

## 宪法

宪法已提取到 agent 层：**[../../agents/unity-developer.md](../../agents/unity-developer.md)** — C1-C7 + 退出条件 + 完整性门禁。

---

## 宪法 + 模式 + 平台

全部提取到 agent 层，AutoMode 不再重复维护：

| 内容 | 唯一来源 |
|------|---------|
| C1-C7 宪法 | [../../agents/unity-developer.md](../../agents/unity-developer.md) |
| 模式选择逻辑 + 对比表 | [../../agents/unity-developer.md](../../agents/unity-developer.md) |
| Editor 可用性策略 | [../../agents/unity-developer.md](../../agents/unity-developer.md) |
| 退出条件 + 完整性门禁 | [../../agents/unity-developer.md](../../agents/unity-developer.md) |

---

## 模式入口

- **Research Mode**：[modes/research.md](modes/research.md) — Shader 调试、参数调优、人工逐步观测
- **Production Mode**：[modes/production.md](modes/production.md) — 功能开发、Bug 修复、有模板一条龙
- **Experiment Mode**：[modes/experiment.md](modes/experiment.md) — 无模板、WebSearch 方案、自主迭代直到目标

---

## 扩展指南

新内容加入时，按以下决策树确定归属：

```
新内容
  │
  ├── 它是"怎么做一件事"的指令？
  │     ├── 单一操作、无分支逻辑 → capabilities/
  │     │     └── 例：编译、进入 Play Mode
  │     │
  │     └── 多步骤组合、有分支/条件 → modes/
  │           └── 例：研发流水线、生产流水线
  │
  ├── 它是"对不对/停不停"的判断标准？
  │     └── agents/ (宪法、退出条件、完整性门禁)
  │     └── rules/ (路径限定的开发规范 + 错误诊断)
  │
  └── 它是"查一下"的参考资料？
        ├── CLI 命令速查 → ../../agents/unity-developer/cli/unityctl.md
        ├── Roslyn 脚本模板 → ../../agents/unity-developer/cli/roslyn.md
        ├── Roslyn 可执行脚本 → ../../agents/unity-developer/scripts/roslyn/
        └── 项目知识库 → Assets/MarkDowns/
```

### 各文件夹准入标准

| 文件夹 | 准入条件 | 反例 |
|--------|---------|------|
| **capabilities/** | 单一工具，<150行，被至少一个 mode 引用 | 多步骤流程、纯参考文档 |
| **modes/** | 编排多个 capability，有明确流程和退出条件 | 单一工具、纯规则 |
| **agents/** | 宪法、决策规则、质量门禁 | 操作步骤、代码模板 |
| **rules/**    | 路径限定开发规范 + 错误诊断 | 操作步骤 |
| **cli/** | 命令参考文档 | 可执行代码 |
| **scripts/** | 可执行代码（Roslyn C#、Shell） | 文档 |

### 冲突裁决

1. 同时满足多个条件 → 按优先级：**modes > capabilities > agents > rules > cli > scripts**
2. 超过 150 行 → 考虑拆分
3. 无法明确分类 → 先放入 `capabilities/`，标记 `// TODO: classify`
4. 改动涉及 Constitution → 必须先更新 `../../agents/unity-developer.md`

---

## 文件索引

### capabilities/ — 原子能力

| 文件 | 职责 | 被哪些 mode 使用 |
|------|------|-----------------|
| [compile.md](capabilities/compile.md) | 编译验证 + 自动修复 | Research, Production, Experiment |
| [runtime.md](capabilities/runtime.md) | Play Mode 进入/退出/日志 | Production, Experiment |
| [screenshot.md](capabilities/screenshot.md) | 截图留档（按需手动触发） | — |
| [scene-setup.md](capabilities/scene-setup.md) | Roslyn 场景配置 | Production |
| [backup.md](capabilities/backup.md) | 重大改动前备份 | Production, Experiment |
| [knowledge.md](capabilities/knowledge.md) | MarkDowns 知识预加载 | Research, Production, Experiment |
| [cleanup.md](capabilities/cleanup.md) | 轻/重清理系统 | Production, Experiment |
| [web-search.md](capabilities/web-search.md) | WebSearch + Plan 方案设计 | Experiment |
| [script-decision.md](capabilities/script-decision.md) | 脚本决策分支（[G2]） | Production |
| [file-placement.md](capabilities/file-placement.md) | 文件放置分支（[G3]） | Production |

### modes/ — 工作模式

| 文件 | 职责 |
|------|------|
| [research.md](modes/research.md) | 研发流水线（快速试错 + 频繁暂停） |
| [production.md](modes/production.md) | 生产流水线（全自动 + 自动修复） |
| [experiment.md](modes/experiment.md) | 实验流水线（WebSearch + Plan + 自主迭代） |

### 跨层资源

| 层 | 路径 | 职责 |
|----|------|------|
| **Agent** | [../../agents/unity-developer.md](../../agents/unity-developer.md) | C1-C7 宪法 + 模式选择 + 退出条件 + 完整性门禁 |
| **Platform** | [../../agents/unity-developer.md](../../agents/unity-developer.md) | Editor Bridge + 可用性策略 |
| **CLI** | [../../agents/unity-developer/cli/unityctl.md](../../agents/unity-developer/cli/unityctl.md) | unityctl 完整命令参考 |
| **CLI** | [../../agents/unity-developer/cli/roslyn.md](../../agents/unity-developer/cli/roslyn.md) | Roslyn 脚本食谱 + 命令速查 |
| **Scripts** | [../../agents/unity-developer/scripts/roslyn/](../../agents/unity-developer/scripts/roslyn/) | 可复用 Roslyn C# 脚本 |
| **Learnings** | [../../rules/shader-development.md](../../rules/shader-development.md) | 编译/运行时错误诊断表 |
| **Learnings** | [../../agents/unity-developer/memory/2025-06-15-safety-lessons.md](../../agents/unity-developer/memory/2025-06-15-safety-lessons.md) | 安全红线 + 经验教训 |
