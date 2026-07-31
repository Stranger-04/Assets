# Agent 模板

> 创建新 developer agent 时，复制此结构。

---

## Agent 专属目录

```
agents/<name>/
├── memory/          # 日期命名: YYYY-MM-DD-<slug>.md + 索引
├── references/      # API 速查（MD ≤ 80 行）+ README.md
├── templates/       # 完整可运行代码（.shader / .cs / .compute）
├── cli/             # 命令参考文档（可选）
└── scripts/         # 可执行脚本（可选）
```

> 不创建 learnings/ — 经验归 memory/，规范归 rules/
> 不创建 platforms/ — 平台配置内化到 agent.md

---

## agent.md 结构

```markdown
---
name: <name>-developer
description: <one-line>.
tools: [Read, Write, Edit, Bash, Glob, Grep]
model: opus
---

# <Agent Name>

> 一句话说明职责。

---

## 职责边界

| 我做什么 | 我不做什么 |
|---------|-----------|
| ... | ... |

---

## 宪法 / 核心规则

| # | 原则 | 说明 |
|---|------|------|
| **C1** | ... | ... |

---

## 工作流

（步骤图或列表）

---

## Editor/平台 可用性策略

（平台连接、可用性状态、验证工具选择 — 内化，不单独建 platforms/）

---

## 退出条件

（需要人工观测 / 需要人工决策 / 抵达关键节点 / 兜底退出）

---

## 错误恢复策略

| 模式 | 诊断行为 |
|------|---------|
| ... | ... |

---

## 会话收尾 — Memory 回写

（E1 回顾 → E2 判断写入目标 → E3 写入规则）

---

## 自描述

### 依赖
- **references**: <目录列表>
- **skills**: <skill 列表>
- **memory**: <memory 索引路径>

### 知识边界
- **擅长**: <领域>
- **不擅长**: <不涉及>
- **版本**: <引擎/框架 + 平台>

### 触发条件
- **关键词**: <逗号分隔>
- **文件路径**: <路径 pattern>

---

## 跨引用

- **Skill 入口**：...
- **CLI 命令**：...
- **Meta 维护者**：meta-developer.md
```

---

## 创建 Checklist

- [ ] 按模板填写 agent.md
- [ ] YAML frontmatter 完整（name, description, tools, model）
- [ ] 自描述段完整（依赖、知识边界、触发条件）
- [ ] 专属目录：memory + references + templates
- [ ] 不创建 learnings/ 或 platforms/ 子目录
- [ ] 在 `~/.claude/agents/default.md` 注册路由
- [ ] 关键词不与已有 agent 冲突
- [ ] 所有跨引用路径存在
