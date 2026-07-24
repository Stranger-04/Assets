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
│  │   .claude/memory/MEMORY.md                      │      │
│  │   .claude/learnings/                            │      │
│  │   会话启动加载 → 会话结束更新                     │      │
│  ↓                                                  │      │
│  [2] Agent ───────────────────────────────────┐    │      │
│  │   .claude/agents/unity-developer.md         │    │      │
│  │   宪法 C1-C7 + 模式选择 + 退出条件           │    │      │
│  ↓                                             │    │      │
│  [3] Platform ───────────────────────────┐    │    │      │
│  │   .claude/platforms/unity-editor.md    │    │    │      │
│  │   Editor 可用性 → 流水线深度            │    │    │      │
│  ↓                                        │    │    │      │
│  [4] Skill ────────────────────────┐     │    │    │      │
│  │   .claude/skills/auto-manager/   │     │    │    │      │
│  │   过程性知识 + 工作流编排          │     │    │    │      │
│  ↓                                   │     │    │    │      │
│  [5] CLI ────────────────────┐      │     │    │    │      │
│  │   .claude/cli/unityctl.md  │      │     │    │    │      │
│  │   .claude/cli/roslyn.md    │      │     │    │    │      │
│  ↓                             │      │     │    │    │      │
│  [6] Script ───────────┐      │      │     │    │    │      │
│  │   .claude/scripts/   │      │      │     │    │    │      │
│  │   roslyn/*.cs        │      │      │     │    │    │      │
│  ↓                       │      │      │     │    │    │      │
│  [7] → Memory ───────────┘      │      │     │    │    │      │
│   learnings/ 更新                │      │     │    │    │      │
│   MEMORY.md 追加                 │      │     │    │    │      │
│   sessions/ FTS5 索引            │      │     │    │    │      │
└──────────────────────────────────────────────────────────┘
```

---

## 宪法 (C1-C7)

以下原则优先级最高。任何 skill、rule、mode 与宪法冲突时，**宪法优先**。

| # | 原则 | 说明 |
|---|------|------|
| **C1** | 安全优先于速度 | `git stash --all` 永久禁止。任何删除操作必须先列清单、人工确认、再执行。 |
| **C2** | 不碰用户代码 | 清理/自动修复只作用于 `tmp/`、`Screenshots/`、场景测试物体。绝不动 `Assets/Mine/` 下的功能代码。 |
| **C3** | 渐进式自动化 | 先轻后重。轻操作可自动，重操作必须人工确认。 |
| **C4** | 证据驱动 | 不凭"看起来对"下结论。编译通过看日志，运行效果看日志和返回值，错误诊断看堆栈。 |
| **C5** | 可回退 | 重大改动前必须备份。任何不可逆操作前必须留回退路径。 |
| **C6** | 模式优先 | 先判断 Research vs Production，再按模式规则执行。不在 Research 模式做 Production 的事。 |
| **C7** | 知识优先 | 任何写代码的任务必须先加载知识库，确保代码风格、命名、文件结构符合项目规范。 |

详细宪法 + 模式选择逻辑 + 退出条件 + 完整性门禁 → [agents/unity-developer.md](.claude/agents/unity-developer.md)

---

## 快速参考

- **项目根目录**：`/Users/xiaokangji/Unity/Lab`
- **关键代码目录**：`Assets/Mine/Shaders/`、`Assets/Mine/Scripts/`
- **知识库**：`Assets/MarkDowns/`（项目开发规范）
- **Editor 检查**：`unityctl status`
- **Memory**：[.claude/memory/MEMORY.md](.claude/memory/MEMORY.md)
- **经验教训**：[.claude/learnings/](.claude/learnings/)
