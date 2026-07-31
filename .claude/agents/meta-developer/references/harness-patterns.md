# Harness Design Patterns

> meta-developer 创建任何新内容时必须对照的原则。

---

## 核心原则

| # | 原则 | 检查方式 |
|---|------|---------|
| **P1** | **MD 做索引，文件做内容** | MD ≤ 80 行；代码块 > 20 行 → 提取为独立文件 |
| **P2** | **Skill 引用 Script，不包含 Script** | skill/capability 中无完整代码；代码在 scripts/ |
| **P3** | **Reference + Template 配对** | reference 做 API 速查；template 放可运行代码 |
| **P4** | **每次改动后精简去重** | grep 重复关键词 → 合并 → 拆分过大的 MD |
| **P5** | **渐进式披露** | 索引 → 概要 → 细节；不要一次性塞入上下文 |
| **P6** | **AI-friendly 格式** | 用模型"天然见过"的标准格式和开源协议 |

---

## 文件放置决策

```
新内容
  │
  ├── 是"如何组织/规范"的索引？     → MD 文件（≤ 80 行）
  ├── 是"可运行的完整代码"？        → templates/ 或 scripts/
  ├── 是"某领域的 API 速查"？      → references/<domain>/
  ├── 是"编辑文件时加载的规则"？    → rules/（带 paths: frontmatter）
  ├── 是"项目事实/决策记录"？       → memory/YYYY-MM-DD-<slug>.md
  ├── 是"怎么做一件事"的指令？      → skills/<skill>/capabilities/
  └── 是"多步骤编排"？             → skills/<skill>/modes/

特殊情况：
  ├── 内容只服务一个 agent → 放 agents/<name>/
  └── 跨 agent 共享         → 顶层（CLAUDE.md, rules/, skills/）
```

---

## 目录数量控制

| 条件 | 动作 |
|------|------|
| 目录下 ≤ 2 个文件 | 考虑内化到父级（如 platforms/ 2 文件 → 并入 agent.md） |
| MD 文件 > 80 行 | 拆分：提取代码到文件，MD 只留索引 |
| 同一概念在 3+ 文件出现 | 合并去重 |
| 两个文件内容重叠 > 50% | 合并为一个 |

---

## 反模式

| 反模式 | 正确做法 |
|--------|---------|
| MD 包含 200 行代码 | 代码 → templates/xxx.cs，MD 只写 "模板: templates/xxx.cs" |
| reference 和 template 混在一个文件 | 分离：reference.md (索引) + template.xxx (代码) |
| 新建目录只放 1-2 个文件 | 内化到父级 |
| 多个文件重复同一概念 | 合并，只保留一处权威来源 |
| learnings/ 作为独立目录 | 错误 → rules/，经验 → memory/ |
| platforms/ 作为独立目录 | 并入 agent.md |
