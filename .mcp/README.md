# Unity Gate MCP Server

> 配方驱动的门禁系统。Claude Code 通过 `.mcp.json` 自动加载。

---

## 架构

```
Gate Center (gate_center.py)      ← 注册表 + 配方表 + 状态追踪
  └── RECIPES (配方)               ← 门禁 ID 有序列表
        └── Gates (gates/*.py)     ← check(ctx) → pass | fail
```

三层解耦：**中心不在意门禁内容，配方不在意门禁实现，门禁只在意自身逻辑。**

---

## 工具

| 工具 | 作用 |
|------|------|
| `gate_set_recipe(name)` | 选择配方: Production / Research / Experiment |
| `gate_pass(gate_id, **ctx)` | 通过指定门禁 |
| `gate_status()` | 查看当前状态 |
| `gate_list()` | 列出所有门禁 + 配方 |
| `gate_reset()` | 重置 |
| `script_list()` | 列出 scripts/roslyn/ |
| `write_gated(path, content)` | 配方完整才放行写入 |

## 配方

| 配方 | 门禁链 |
|------|--------|
| Production | g_entry → g_mode → g_script → g_file |
| Research | g_entry → g_mode |
| Experiment | g_entry → g_mode → g_web_search → g_plan → g_script → g_file |
| Debug | g_entry → g_mode → g_script |
| Minimal | g_entry |

## 门禁

| ID | 名称 | 前置 | 参数 |
|----|------|------|------|
| g_entry | 框架入口 | — | agent |
| g_mode | 模式确认 | g_entry | mode, reason |
| g_script | 脚本决策 | g_mode | decision |
| g_file | 文件放置 | g_mode | file_type, category, effect |
| g_web_search | 联网搜索 | g_mode | query, summary |
| g_plan | 方案设计 | g_web_search | plan_summary |

## 使用

```bash
# 测试
uv run python tests/test_recipes.py

# 注册 (.mcp.json 已配置，Claude Code 自动加载)
```
