# Research Mode — 研发流水线

> 适用于 Shader 调试、参数调优、效果验证、假设验证等探索性任务。
> 核心特征：**频繁暂停等待人工观测，快速试错循环**。

---

## When to Use

以下任一信号触发：

- 指令涉及 Shader / 渲染 / 视觉效果
- GPU / Compute Shader 相关
- 关键词：调试、看看效果、试试、验证、排查、调参数
- 无明确的目标值（如"调好看一点"）

---

## Process

```
用户假设/指令
  │
  ├── [R1] 知识预加载 ─── @capabilities/knowledge.md （按需加载）⚠️ 必须执行
  │
  ├── [R2] 快速编辑 ─── 直接修改代码，遵循 knowledge.md 加载的规范
  │
  ├── ═══════════════ Editor Required ═══════════════
  │     ↓ 以下步骤仅在 unityctl status = connected 时执行 ↓
  │
  ├── [R3] 编译验证 ─── @capabilities/compile.md （快速模式：报错即停）
  │     ├── 编译失败 → 报告错误 → ⏸️ 暂停（不自动修复）
  │     └── 编译通过 → 继续
  │
  ├── [R4] 进入 Play Mode ─── @capabilities/runtime.md
  │
  ├── [R5] 观测 ─── ⏸️ 暂停，等待人工看图/看日志
  │     ├── 效果符合预期 → 继续 [R6]
  │     └── 效果不符合 → 回到 [R2] 重新修改
  │
  └── [R6] 退出 Play Mode ─── 报告结果
  │
  ═══════════════ Editor Required ═══════════════
```

**Editor 不可用时：** 执行 [R1]-[R2] 后直接报告"代码已修改，Editor 未运行，无法验证效果"。

## Key Behaviors

| 维度 | 行为 |
|------|------|
| 备份 | ❌ 跳过 |
| 知识预加载 | 按需（Shader→读 ShaderStructure，C#→读 ScriptStructure） |
| 方案设计 | ❌ 跳过（假设驱动） |
| 编译失败 | 报告 → ⏸️ 暂停，不自动修复 |
| 运行时错误 | 报告诊断建议 → ⏸️ 暂停 |
| 每步运行后 | ⏸️ 强制暂停，等待人工确认 |
| 循环 | ✅ 支持无限循环 |
| 清理 | ❌ 不触发 |

## Rationalizations（Agent 不得跳过）

| Agent 可能的借口 | 为什么不能跳过 |
|-----------------|--------------|
| "编译通过了，效果应该没问题" | 渲染效果无法通过编译验证，必须人工看图 |
| "我根据日志推断画面应该是正确的" | Shader 的视觉效果不能用日志推断 |
| "这个改动很小，不用暂停确认" | 微小的 Shader 参数变化可能导致完全不同的画面 |
| "多改几处一起跑效率更高" | 一次改一处才能定位哪个改动导致了效果变化 |

## Red Flags

- Agent 尝试在 Research Mode 下自动修复编译错误 → 立即停止，等待人工
- Agent 跳过观测步骤直接进入下一轮修改 → 提醒"必须先确认画面效果"
- Agent 建议"批量修改后一起验证" → 拒绝，Research Mode 每次只改一处

## Verification

| 阶段 | 验证方式 | 通过标准 |
|------|---------|---------|
| 编译 | `unityctl asset refresh` 输出 | `compilation succeeded` |
| 运行 | `unityctl logs -n 20` | 无 `Exception`、无 `Error` |
| 效果 | 人工看图确认 | 用户明确回复"符合预期"或"可以了" |
| 截图 | `Screenshots/` 文件存在 | 文件时间戳在本次会话内 |

## Exit

- 人工确认效果符合预期
- 人工决定放弃当前方向
- 触发兜底退出（@../rules/exit-conditions.md）
