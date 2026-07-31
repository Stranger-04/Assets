# Meta Developer References

> meta-developer 的领域知识库。内容是关于如何设计、创建、维护 `.claude` 体系本身。
> 对标 unity-developer 的 `references/urp-shader-lib/`。

---

## 索引

### 体系设计（自有）
| 文件 | 内容 | 用途 |
|------|------|------|
| [agent-template.md](agent-template.md) | 创建新 agent 的模板和规范 | 新增 developer agent 时对照 |
| [manifest-spec.md](manifest-spec.md) | Agent 自描述段的格式规范 | meta 读取 manifest 的契约 |
| [layer-conventions.md](layer-conventions.md) | .claude 7 层架构的每一层约定 | 保持一致性的参考 |
| [cross-reference-rules.md](cross-reference-rules.md) | 跨引用路径规则 | 避免断链 |
| [harness-patterns.md](harness-patterns.md) | Harness 设计模式速查 | 设计新 agent/skill 时的原则 |

### 外部参考（标准）
| 文件 | 来源 | 用途 |
|------|------|------|
| [claude-code-structure.md](claude-code-structure.md) | [Claude Code Docs](https://code.claude.com/docs/en/claude-directory) | .claude 目录 + agent/skill/rule 格式规范 |
| [hermes-agent-skills.md](hermes-agent-skills.md) | [Hermes Agent Docs](https://hermes-agent.nousresearch.com/docs/developer-guide/creating-skills) | Skill 目录结构 + 渐进式披露 + 设计原则 |
