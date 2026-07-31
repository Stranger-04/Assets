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
│  │   tmp/.reusable/     │      │      │     │    │    │      │
│  │   query_scene.cs     │      │      │     │    │    │      │
│  │   organize_scene.cs  │      │      │     │    │    │      │
│  ↓                       │      │      │     │    │    │      │
│  [7] → Memory ───────────┘      │      │     │    │    │      │
│   memory/ 创建 dated 文件 + 更新索引         │    │    │      │
│   rules/ 追加新错误模式 (grep 去重)           │    │    │      │
└──────────────────────────────────────────────────────────┘
```

---

## 宪法 (C1-C7)

宪法是项目的最高原则。唯一权威来源：[agents/unity-developer.md](.claude/agents/unity-developer.md) — C1-C7 + 模式选择 + 退出条件 + 完整性门禁。

---

## 快速参考

- **项目根目录**：`/Users/xiaokangji/Unity/Lab`
- **关键代码目录**：`Assets/Mine/Shaders/`、`Assets/Mine/Scripts/`
- **知识库**：`Assets/MarkDowns/`（项目开发规范）
- **Editor 检查**：`unityctl status`
- **Memory**：[.claude/agents/unity-developer/memory/MEMORY.md](.claude/agents/unity-developer/memory/MEMORY.md)
- **开发规范**：[.claude/rules/](.claude/rules/)

## Agent 路由

| 任务类型 | Agent | 触发关键词 |
|---------|-------|-----------|
| Unity 开发（Shader、C#、渲染） | [unity-developer](.claude/agents/unity-developer.md) | Shader, HLSL, Compute, RenderGraph, URP, Material |
| .claude 体系维护 | [meta-developer](.claude/agents/meta-developer.md) | agent, skill, reference, .claude, 体系, 结构, 维护 |

全局路由表在 `~/.claude/agents/default.md`。
