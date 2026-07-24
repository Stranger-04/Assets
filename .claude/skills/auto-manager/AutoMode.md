# AutoMode

> Unity 开发任务自适应系统的唯一真相源（Single Source of Truth）。
> 定义模式选择逻辑、Editor 可用性策略、以及完整文件索引。

---

## 宪法

宪法已提取到 agent 层：**[../../agents/unity-developer.md](../../agents/unity-developer.md)** — C1-C7 + 退出条件 + 完整性门禁。

---

## Editor 可用性策略

Editor 是否在运行**只影响流水线步骤，不影响 skill 是否激活**。

| Editor 状态 | 流水线行为 |
|------------|----------|
| ✅ 在运行 | 完整流水线：知识 → 代码 → 编译 → 运行 → 清理 |
| ❌ 未运行 | 精简流水线：知识 → 代码。跳过编译/运行/清理。报告末尾注明"Editor 未运行，未执行编译验证"。 |

检查命令：`unityctl status`
详细平台配置：[../../platforms/unity-editor.md](../../platforms/unity-editor.md)

---

## 模式对比

| 维度 | 🔬 Research | 🏭 Production |
|------|------------|-------------|
| **场景** | Shader 调试、参数调优、效果验证、假设验证 | 功能开发、Bug 修复、重构、发版准备 |
| **关键词** | 调试、看看、试试、验证、排查、效果 | 实现、开发、添加、修复、重构 |
| **备份** | ❌ | ✅ 重大改动时 |
| **知识预加载** | 按需 | 全量 |
| **方案设计** | ❌ | ✅ 输出修改计划 |
| **编译** | 快速，报错即停 | 完整，自动修复循环 |
| **场景配置** | 手动/按需 | 自动 Roslyn |
| **观测** | 🔴 每步暂停 | 🟢 仅异常暂停 |
| **循环** | ✅ 无限循环 | ❌ 一次性 |
| **清理** | ❌ | ✅ 轻清理提示 |

---

## 选择逻辑

```
用户指令
  │
  ├── [Research 信号] 任一满足 → Research Mode
  │     ├── Shader / 渲染 / 视觉效果
  │     ├── GPU / Compute Shader
  │     ├── 参数调优（无确定目标值）
  │     └── 关键词：调试、看看、试试、验证假设、排查
  │
  └── [Production 信号] 任一满足 → Production Mode
        ├── 明确的输入→输出需求
        ├── 功能增删改 / Bug 修复 / 重构
        └── 关键词：实现、开发、添加、修复、重构
```

**边界：** 同时涉及两类 → 询问确认。先调试后发版 → 先 Research 再切换。用户要求全自动 → 强制 Production。

---

## 模式入口

- **Research Mode**：[modes/research.md](modes/research.md) — 组合 capabilities/，共用规则
- **Production Mode**：[modes/production.md](modes/production.md) — 组合 capabilities/，共用规则

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
  │     └── learnings/ (安全红线、错误模式 — 跨会话经验)
  │
  └── 它是"查一下"的参考资料？
        ├── CLI 命令速查 → ../../cli/unityctl.md
        ├── Roslyn 脚本模板 → ../../cli/roslyn.md
        ├── Roslyn 可执行脚本 → ../../scripts/roslyn/
        └── 项目知识库 → Assets/MarkDowns/
```

### 各文件夹准入标准

| 文件夹 | 准入条件 | 反例 |
|--------|---------|------|
| **capabilities/** | 单一工具，<150行，被至少一个 mode 引用 | 多步骤流程、纯参考文档 |
| **modes/** | 编排多个 capability，有明确流程和退出条件 | 单一工具、纯规则 |
| **agents/** | 宪法、决策规则、质量门禁 | 操作步骤、代码模板 |
| **learnings/** | 跨会话累积经验（错误模式、安全红线） | 任务特定规则 |
| **cli/** | 命令参考文档 | 可执行代码 |
| **scripts/** | 可执行代码（Roslyn C#、Shell） | 文档 |

### 冲突裁决

1. 同时满足多个条件 → 按优先级：**modes > capabilities > agents > learnings > cli > scripts**
2. 超过 150 行 → 考虑拆分
3. 无法明确分类 → 先放入 `capabilities/`，标记 `// TODO: classify`
4. 改动涉及 Constitution → 必须先更新 `../../agents/unity-developer.md`

---

## 文件索引

### capabilities/ — 原子能力

| 文件 | 职责 | 被哪些 mode 使用 |
|------|------|-----------------|
| [compile.md](capabilities/compile.md) | 编译验证 + 自动修复 | Research, Production |
| [runtime.md](capabilities/runtime.md) | Play Mode 进入/退出/日志 | Research, Production |
| [screenshot.md](capabilities/screenshot.md) | 截图留档（按需手动触发） | — |
| [scene-setup.md](capabilities/scene-setup.md) | Roslyn 场景配置 | Production |
| [backup.md](capabilities/backup.md) | 重大改动前备份 | Production |
| [knowledge.md](capabilities/knowledge.md) | MarkDowns 知识预加载 | Research, Production |
| [cleanup.md](capabilities/cleanup.md) | 轻/重清理系统 | Production |

### modes/ — 工作模式

| 文件 | 职责 |
|------|------|
| [research.md](modes/research.md) | 研发流水线（快速试错 + 频繁暂停） |
| [production.md](modes/production.md) | 生产流水线（全自动 + 自动修复） |

### 跨层资源

| 层 | 路径 | 职责 |
|----|------|------|
| **Agent** | [../../agents/unity-developer.md](../../agents/unity-developer.md) | C1-C7 宪法 + 模式选择 + 退出条件 + 完整性门禁 |
| **Platform** | [../../platforms/unity-editor.md](../../platforms/unity-editor.md) | Editor Bridge + 可用性策略 |
| **CLI** | [../../cli/unityctl.md](../../cli/unityctl.md) | unityctl 完整命令参考 |
| **CLI** | [../../cli/roslyn.md](../../cli/roslyn.md) | Roslyn 脚本食谱 + 命令速查 |
| **Scripts** | [../../scripts/roslyn/](../../scripts/roslyn/) | 可复用 Roslyn C# 脚本 |
| **Learnings** | [../../learnings/error-patterns.md](../../learnings/error-patterns.md) | 编译/运行时错误诊断表 |
| **Learnings** | [../../learnings/safety.md](../../learnings/safety.md) | 安全红线 + 经验教训 |
