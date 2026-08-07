# Unity Lab — 项目身份

> Unity 渲染实验与 Shader 研发项目。本文件是项目级 CLAUDE.md，全局路由文件会导航到此。

---

## 项目概述

Unity 6 URP 17+ 渲染技术实验室。研究方向：PCSS 软阴影、Boids 群集模拟、体积云渲染、全屏后处理特效、CelToon 卡通渲染。

## 技术栈

- **引擎**：Unity 6 (6000.x) + Universal Render Pipeline 17+
- **Shader**：HLSL / ShaderGraph / Compute Shader
- **自动化**：unityctl（Editor 远程控制）
- **脚本执行**：Roslyn（C# 运行时注入）
- **平台**：macOS / Metal

---

## 循环架构 (memory → agent → platform → skill → CLI → script → memory)

```
┌──────────────────────────────────────────────────────────┐
│  [1] Memory ←─────────────────────────────────────┐      │
│  │   .claude/agents/unity-developer/memory/        │      │
│  │   .claude/rules/                                │      │
│  │   会话启动加载 → 会话结束更新 (E1-E4)             │      │
│  ↓                                                  │      │
│  [2] Agent ───────────────────────────────────┐    │      │
│  │   .claude/agents/unity-developer.md         │    │      │
│  │   宪法 C1-C7 + 模式选择 + 退出条件           │    │      │
│  │   .claude/agents/meta-developer.md          │    │      │
│  │   体系维护 + 精简去重 (P1-P3)                │    │      │
│  ↓                                             │    │      │
│  [3] Platform ───────────────────────────┐    │    │      │
│  │   agent.md 内 Editor 可用性策略         │    │    │      │
│  │   Editor 状态 → 流水线深度              │    │    │      │
│  ↓                                        │    │    │      │
│  [4] Skill ────────────────────────┐     │    │    │      │
│  │   .claude/skills/auto-manager/   │     │    │    │      │
│  │   过程性知识 + 工作流编排          │     │    │    │      │
│  ↓                                   │     │    │    │      │
│  [5] CLI ────────────────────┐      │     │    │    │      │
│  │   unity-developer/cli/unityctl.md  │     │    │    │      │
│  │   unity-developer/cli/roslyn.md    │     │    │    │      │
│  ↓                             │      │     │    │    │      │
│  [6] Script ───────────┐      │      │     │    │    │      │
│  │   agents/unity-developer/scripts/roslyn/     │      │      │     │    │    │      │
│  │   query_scene.cs     │      │      │     │    │    │      │
│  │   organize_scene.cs  │      │      │     │    │    │      │
│  ↓                       │      │      │     │    │    │      │
│  [7] → Memory ───────────┘      │      │     │    │    │      │
│   memory/ 创建 dated 文件 + 更新索引         │    │    │      │
│   rules/ 追加新错误模式 (grep 去重)           │    │    │      │
└──────────────────────────────────────────────────────────┘
```

---

## 入口门禁 [G0]

> 任何文件写入操作前必须通过此门禁。

**OUTPUT 格式：**
```
## G0: Framework Check
Agent: unity-developer | meta-developer
Action: proceed | load agent first
```

- 涉及 `Assets/Mine/` 写入 → Agent 必须为 `unity-developer`，否则先加载。写入走 MCP `write_gated` 或原生 Write。
- 涉及 `.claude/` 写入 → Agent 必须为 `meta-developer`，否则先加载
- 纯咨询/只读 → 跳过 G0
- MCP 工具发现：`gate_list` 查看所有门禁和配方
- 🟢 **Quick 通道**：完全跳过 MCP，原生 Write/Edit 直写。以完成为优先。

---

## 宪法 (C1-C7)

宪法是项目的最高原则。唯一权威来源：[agents/unity-developer.md](agents/unity-developer.md) — C1-C7 + 模式选择 + 退出条件 + 完整性门禁。

---

## 快速参考

- **项目根目录**：`/Users/xiaokangji/Unity/Lab`
- **关键代码目录**：`Assets/Mine/Shaders/`、`Assets/Mine/Scripts/`
- **知识库**：`Assets/MarkDowns/`（项目开发规范）
- **Editor 检查**：`unityctl status`
- **Memory**：[agents/unity-developer/memory/MEMORY.md](agents/unity-developer/memory/MEMORY.md)
- **开发规范**：[.claude/rules/](.claude/rules/)

## Agent 路由

| 任务类型 | Agent | 触发关键词 |
|---------|-------|-----------|
| Unity 开发（Shader、C#、渲染） | [unity-developer](agents/unity-developer.md) | Shader, HLSL, Compute, RenderGraph, URP, Material |
| .claude 体系维护 | [meta-developer](agents/meta-developer.md) | agent, skill, reference, .claude, 体系, 结构, 维护 |

全局路由表在 `~/.claude/agents/default.md`。

> **架构说明**：`rules/` 和 `skills/` 虽全为 Unity 内容，但因 Claude Code 的路径限定加载（`paths:` frontmatter）和 Skill 发现机制要求它们必须在顶级 `.claude/` 下，无法移入 `agents/unity-developer/`。将来加入非 Unity agent 时，其 rules 和 skills 将共存于同名顶级目录。
