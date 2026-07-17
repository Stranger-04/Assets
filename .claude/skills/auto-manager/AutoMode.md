# AutoMode

> Unity 开发任务自适应系统的唯一真相源（Single Source of Truth）。
> 定义模式选择逻辑、项目最高原则、Editor 可用性策略、以及完整文件索引。

---

## Constitution（项目宪法）

以下原则优先级最高。任何 capability、rule、mode 与宪法冲突时，**宪法优先**。

| # | 原则 | 说明 |
|---|------|------|
| **C1** | 安全优先于速度 | `git stash --all` 永久禁止。任何删除操作必须先列清单、人工确认、再执行。 |
| **C2** | 不碰用户代码 | 清理/自动修复只作用于 `tmp/`、`Screenshots/`、场景测试物体。绝不动 `Assets/Mine/` 下的功能代码。 |
| **C3** | 渐进式自动化 | 先轻后重。轻操作可自动，重操作（删除文件、修改架构）必须人工确认。 |
| **C4** | 证据驱动 | 不凭"看起来对"下结论。编译通过看日志，运行效果看日志和返回值，错误诊断看堆栈。 |
| **C5** | 可回退 | 重大改动前必须备份。任何不可逆操作前必须留回退路径。 |
| **C6** | 模式优先 | 先判断 Research vs Production，再按模式规则执行。不在 Research 模式做 Production 的事（反之亦然）。 |
| **C7** | 知识优先 | 任何写代码的任务必须先加载知识库（capabilities/knowledge.md），确保代码风格、命名、文件结构符合项目规范。 |

---

## Editor 可用性策略

Editor 是否在运行**只影响流水线步骤，不影响 skill 是否激活**。

| Editor 状态 | 流水线行为 |
|------------|----------|
| ✅ 在运行 | 完整流水线：知识 → 代码 → 编译 → 运行 → 清理 |
| ❌ 未运行 | 精简流水线：知识 → 代码。跳过编译/运行/清理。报告末尾注明"Editor 未运行，未执行编译验证"。 |

检查命令：`unityctl status`

---

## 模式对比

| 维度 | 🔬 Research | 🏭 Production |
|------|------------|-------------|
| **场景** | Shader 调试、参数调优、效果验证、假设验证 | 功能开发、Bug 修复、重构、发版准备 |
| **关键词** | 调试、看看、试试、验证、排查、效果 | 实现、开发、添加、修复、重构 |
| **备份** | ❌ | ✅ 重大改动时 |
| **知识预加载** | 按需 | 全量 |
| **方案设计** | ❌ | ✅ 输出修改计划 |
| **编译** | 快速，报错即停 | 完整，自动修复循环 |
| **场景配置** | 手动/按需 | 自动 Roslyn |
| **观测** | 🔴 每步暂停 | 🟢 仅异常暂停 |
| **循环** | ✅ 无限循环 | ❌ 一次性 |
| **清理** | ❌ | ✅ 轻清理提示 |

---

## 选择逻辑

```
用户指令
  │
  ├── [Research 信号] 任一满足 → Research Mode
  │     ├── Shader / 渲染 / 视觉效果
  │     ├── GPU / Compute Shader
  │     ├── 参数调优（无确定目标值）
  │     └── 关键词：调试、看看、试试、验证假设、排查
  │
  └── [Production 信号] 任一满足 → Production Mode
        ├── 明确的输入→输出需求
        ├── 功能增删改 / Bug 修复 / 重构
        └── 关键词：实现、开发、添加、修复、重构
```

**边界：** 同时涉及两类 → 询问确认。先调试后发版 → 先 Research 再切换。用户要求全自动 → 强制 Production。

---

## 模式入口

- **Research Mode**：[modes/research.md](modes/research.md) — 组合 capabilities/，共用 rules/
- **Production Mode**：[modes/production.md](modes/production.md) — 组合 capabilities/，共用 rules/

---

## 扩展指南

新内容加入时，按以下决策树确定归属：

```
新内容
  │
  ├── 它是"怎么做一件事"的指令？
  │     ├── 单一操作、无分支逻辑 → capabilities/
  │     │     └── 例：编译、进入 Play Mode
  │     │
  │     └── 多步骤组合、有分支/条件 → modes/
  │           └── 例：研发流水线、生产流水线
  │
  ├── 它是"对不对/停不停"的判断标准？
  │     └── rules/
  │           └── 例：错误模式匹配表、退出条件、安全红线
  │
  └── 它是"查一下"的参考资料？
        └── references/
              └── 例：命令速查、Roslyn 食谱
```

### 各文件夹准入标准

| 文件夹 | 准入条件 | 反例（不应放入） |
|--------|---------|----------------|
| **capabilities/** | 单一工具，<150行，被至少一个 mode 引用 | 多步骤流程、纯参考文档 |
| **rules/** | 判断标准或约束规则，被至少一个 capability/mode 引用 | 操作步骤、代码模板 |
| **modes/** | 编排多个 capability，有明确流程和退出条件 | 单一工具、纯规则 |
| **references/** | 不参与自动化流程，仅供查阅 | 任何会被 mode 直接调用的内容 |

### 冲突裁决

1. 同时满足多个条件 → 按优先级：**modes > rules > capabilities > references**
2. 超过 150 行 → 考虑拆分，不要强行塞入
3. 无法明确分类 → 先放入 `capabilities/`，标记 `// TODO: classify`，后续整理时重新分类
4. 改动涉及 Constitution → 必须先更新本文 Constitution 段

### 新增文件 Checklist

- [ ] 按决策树确定了归属文件夹
- [ ] 若是 mode 文件，遵循统一 anatomy（When to Use / Process / Rationalizations / Red Flags / Verification）
- [ ] 在本文文件索引表中注册
- [ ] 所有引用文件的路径正确（相对路径）
- [ ] 检查是否与已有文件重复（先 grep 关键词）

---

## 文件索引

### capabilities/ — 原子能力

| 文件 | 职责 | 被哪些 mode 使用 |
|------|------|-----------------|
| [compile.md](capabilities/compile.md) | 编译验证 + 自动修复 | Research, Production |
| [runtime.md](capabilities/runtime.md) | Play Mode 进入/退出/日志 | Research, Production |
| [screenshot.md](capabilities/screenshot.md) | 截图留档（按需手动触发） | — |
| [scene-setup.md](capabilities/scene-setup.md) | Roslyn 场景配置 | Production |
| [backup.md](capabilities/backup.md) | 重大改动前备份 | Production |
| [knowledge.md](capabilities/knowledge.md) | MarkDowns 知识预加载 | Research, Production |
| [cleanup.md](capabilities/cleanup.md) | 轻/重清理系统 | Production |

### rules/ — 验证规则（所有 mode 共用）

| 文件 | 职责 |
|------|------|
| [error-patterns.md](rules/error-patterns.md) | 编译/运行时错误诊断表 |
| [exit-conditions.md](rules/exit-conditions.md) | 4 类退出条件 |
| [safety.md](rules/safety.md) | 安全红线 + 经验教训 |
| [completeness-gate.md](rules/completeness-gate.md) | 规范符合度检查清单（P2 强制门禁） |

### modes/ — 工作模式

| 文件 | 职责 |
|------|------|
| [research.md](modes/research.md) | 研发流水线（快速试错 + 频繁暂停） |
| [production.md](modes/production.md) | 生产流水线（全自动 + 自动修复） |

### references/ — 参考手册（不参与流程，按需查阅）

| 文件 | 职责 |
|------|------|
| [roslyn-recipes.md](references/roslyn-recipes.md) | Roslyn 脚本食谱 + 命令速查 |
