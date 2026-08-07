# Production Mode

> 门禁驱动的静态工作流。每个 [Gx] 节点是强制决策点：必须输出结构化决策后才能进入下一步。
> 业界对标：Tool Denial by Construction + Structured Output as Transition Contract。

---

## Process

```
用户需求
  │
  ├── ═══════════ [G0] 框架入口 ═══════════
  │     → 确认 agent 上下文
  │
  ├── ═══════════ [G1] 模式确认 ═══════════
  │     → 确认 mode + reason
  │
  ├── [P1] 知识预加载 → @capabilities/knowledge.md（全量）
  │
  ├── [P2] 方案设计
  │     ├── P2a: 读取目标文件
  │     ├── P2b: 规范符合度检查 → 输出检查清单表
  │     └── P2c: 修改计划
  │
  ├── ═══════════ [G2] 脚本决策 ═══════════
  │     → @capabilities/script-decision.md
  │
  ├── ═══════════ [G3] 文件放置 ═══════════
  │     → @capabilities/file-placement.md
  │
  ├── [P3] 代码生成
  │     ⚠️ 读取 [G2] Decision + [G3] TargetDir
  │     ⚠️ 写入前确认门禁通过
  │
  ├── ═══════════ Editor Required ═══════════
  │
  ├── [P4] 编译验证 → @capabilities/compile.md
  │
  ├── [P5] 场景操作 → @capabilities/scene-setup.md（使用 [G2] 决定的脚本）
  │
  ├── [P6] 运行时验证 → @capabilities/runtime.md
  │
  └── [P7] 退出 + 报告
```

---

## 门禁契约

| 门禁 | 输出格式 | 失败阻断 |
|------|---------|---------|
| **[G0]** | `Agent: <name>` | 不在框架内 → 加载 agent 后重试 |
| **[G1]** | `Mode: <mode> \| Reason: <why>` | 模式未确认 → 不执行后续 |
| **[G2]** | `Decision: USE \| CREATE reusable \| CREATE tmp` | 未输出 Decision → 不进入 P3 |
| **[G3]** | `FileType: <ext> \| Category: <cat> \| TargetDir: <path>` | 未确定路径 → 不写文件 |

> 每个 [Gx] 的输出是下一步的输入。跳过门禁 = 下一步无法执行。

---

## Red Flags

- Agent 未输出 [Gx] 格式就进入下一步 → 退回门禁
- [G2] Decision=USE 但 agent 写了新脚本 → 废弃新脚本，使用已有
- 同一错误连续 2 次 → 第 3 次前暂停
- `git stash --all` → 立即拦截
