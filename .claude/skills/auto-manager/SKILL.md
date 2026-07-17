---
name: autoagent
description: >-
  Adaptive mode router for ALL Unity project tasks. PROACTIVELY invoke this
  skill when the user asks to modify any file, write any code, edit any shader,
  organize any assets, refactor any class, or debug any issue in a Unity
  project. Routes to Research Mode (exploratory) or Production Mode
  (systematic). Always loads project knowledge base first. When Unity Editor is
  running, extends pipeline with compile → run → cleanup.
  Non-negotiable: file changes in a Unity project = this skill activates.
user-invocable: true
argument-hint: "<task description>"
model: opus
---

# AutoAgent

> Unity 开发任务自适应路由器。覆盖所有开发任务，Editor 可用时自动扩展为完整流水线。

## CRITICAL — 触发后立即执行

**This skill body loads on trigger. You MUST immediately:**

1. `Read` [AutoMode.md](AutoMode.md) — 获取 Constitution、模式对比表、选择逻辑
2. `Read` [capabilities/knowledge.md](capabilities/knowledge.md) — 加载项目知识库（代码风格、命名规范、文件结构）
3. `bash: unityctl status` — 检查 Editor 是否在运行
4. 根据 AutoMode.md 的选择逻辑判断 Research vs Production
5. `Read` 对应的 mode 文件（[modes/research.md](modes/research.md) 或 [modes/production.md](modes/production.md)）
6. 按 mode 文件的 Process 段逐步执行。Editor 不可用时自动跳过编译/运行步骤。

**报告格式：**
- Editor 可用：`🏭 AutoAgent → Production Mode（全流水线：编译 + 运行）`
- Editor 不可用：`🏭 AutoAgent → Production Mode（仅代码：Editor 未运行，跳过编译验证）`
- Research：`🔬 AutoAgent → Research Mode（Shader 效果调试，需要人工观测）`

---

## 激活条件

| 条件 | 说明 |
|------|------|
| 用户发出开发任务 | 创建/修改代码、Shader、文件整理、重构、调试、文档 |
| 非纯对话/咨询 | 涉及文件写入或项目操作 |

**Editor 不是激活前提。** Editor 是否可用只影响流水线中编译/运行步骤是否执行。

## 路由速查

```
用户指令
  │
  ├── Shader / 渲染 / 调参 / 效果验证 → 🔬 Research Mode
  ├── 功能开发 / Bug修复 / 重构 / 文件整理 → 🏭 Production Mode
  └── 纯咨询 / 闲聊                  → 不激活
```

完整选择逻辑和边界情况见 [AutoMode.md](AutoMode.md)。
