# .claude 架构约定

> 当前架构的各层文件、命名、引用规则。meta-developer 创建/修改时必须遵守。

---

## 层级总览

```
.claude/
├── CLAUDE.md                    # 项目身份 + 宪法引用（不重复内容）
├── settings.json / .local.json  # 权限 + 模型配置
│
├── rules/                       # 路径限定开发规范（path-scoped loading）
│
├── skills/                      # 过程性知识（Claude Code 要求顶级）
│   └── <skill>/SKILL.md         #   入口 → capabilities/（原子操作）→ modes/（编排）
│
├── scripts/hooks/               # 生命周期钩子
│
└── agents/
    ├── <name>.md                 # agent 定义（YAML frontmatter + 宪法 + 自描述）
    └── <name>/                   # agent 专属资源
        ├── memory/               #   日期命名: YYYY-MM-DD-<slug>.md + MEMORY.md 索引
        ├── references/           #   API 速查（MD ≤ 80 行）+ README.md 索引
        ├── templates/            #   完整可运行代码（.shader / .compute / .cs）
        ├── cli/                  #   命令参考文档
        └── scripts/              #   可执行脚本（Roslyn .cs 等）
```

---

## 各层约定

### Agent 层

| 约定 | 说明 |
|------|------|
| 文件命名 | `<domain>-developer.md` 或 `<domain>-maintainer.md` |
| **YAML frontmatter** | 必须有 `name`、`description`、`tools`、`model` |
| 必须有 | 职责边界、工作流、会话收尾、**自描述段**、跨引用 |
| 自描述段 | `## 自描述` 开头，含依赖、知识边界、触发条件 |
| 路由注册 | 全局 `~/.claude/agents/default.md` |

### Reference 层 — P1: MD 做索引，文件做内容

| 约定 | 说明 |
|------|------|
| 每个子目录 | 必须有 README.md 索引 |
| MD 文件 | ≤ 80 行，只放 API 速查表 + 差异对照 |
| 完整代码 | 放 templates/（.shader / .compute / .cs），不放 references/ |
| 内容去重 | 同一概念只在一处出现 |

### Template 层 — 与 Reference 配对

| 约定 | 说明 |
|------|------|
| 文件类型 | 可运行的 .shader / .compute / .cs |
| 注释格式 | `// ⚠️ 替换` 标记需自定义的位置 |
| 来源标注 | 头部注释写明参考的 Unity 官方文件路径 |
| Metal 兼容 | 所有模板默认 Metal 兼容 |

### Skill 层 — P2: 引用 Script，不包含 Script

| 约定 | 说明 |
|------|------|
| 目录结构 | `SKILL.md`（入口）+ `capabilities/`（原子）+ `modes/`（编排） |
| 不包含代码 | capability 中超过 10 行代码 → 提取到 scripts/ |
| 渐进式披露 | SKILL.md 只写触发+路由，细节在 capabilities/ |

### Rules 层

| 约定 | 说明 |
|------|------|
| paths: frontmatter | 必须声明匹配的文件路径 pattern |
| 内容 | 编辑该类型文件时的规范约束 + 错误诊断 |
| 不重复 | 不与 references/ 中的 API 速查表重复 |

### Memory 层

| 约定 | 说明 |
|------|------|
| 文件命名 | `YYYY-MM-DD-<slug>.md` |
| 索引文件 | `MEMORY.md` / `memory.md` 列出所有日期文件 |
| 内容归属 | 每个 agent 的 memory 只记录自己领域的内容 |

---

## 反模式速查

| 反模式 | 正确 |
|--------|------|
| 目录下只有 1-2 个文件 | 内化或合并 |
| MD 超过 80 行 | 提取代码到文件，MD 只留索引 |
| references/ 放完整代码 | 代码 → templates/ |
| capabilities/ 中嵌入 C# 代码 | 代码 → scripts/roslyn/ |
| 同一概念在 3 处出现 | 合并为 1 处权威来源 |
| 新建 `learnings/` 目录 | 经验 → memory/，规范 → rules/ |
| 新建 `platforms/` 目录 | 内容 → agent.md 内 Editor 段 |
