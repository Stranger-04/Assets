"""Unity Gate MCP Server — 配方驱动门禁系统.

每个门禁独立，可随机调用。配方只定义顺序列表。
新增 Mode = 新增配方。新增 Gate = 新增 g_xxx.py + 注册到 gate_center。

工具:
  gate_set_recipe(name)              — 选择配方 (Production/Research/Experiment/...)
  gate_pass(gate_id, **context)      — 通过指定门禁
  gate_status()                       — 查看当前状态
  gate_list()                         — 列出所有门禁 + 配方
  gate_reset()                        — 重置
  script_list()                       — 列出脚本库
  write_gated(path, content)          — 门禁校验后写入
"""

import json, os, asyncio
from mcp.server import MCPServer
from mcp.server.stdio import stdio_server

from gate_center import state, GATE_REGISTRY, RECIPES
from validation.script_library import list_scripts
from validation.project_paths import validate_path

server = MCPServer(name="unity-gate", version="0.3.0")


# ═══ gate_set_recipe — 选择配方 ═══

@server.tool()
async def gate_set_recipe(name: str) -> str:
    """选择配方 (Mode)。新任务第一步。

    Args:
        name: "Production" | "Research" | "Experiment" | "Debug" | "Minimal"
    """
    return json.dumps(state.set_recipe(name), ensure_ascii=False)


# ═══ gate_pass — 通过指定门禁 ═══

@server.tool()
async def gate_pass(gate_id: str, agent: str = "", mode: str = "", reason: str = "",
                    decision: str = "", file_type: str = "", category: str = "",
                    effect: str = "", query: str = "", summary: str = "",
                    plan_summary: str = "") -> str:
    """通过指定门禁。传入门禁需要的上下文参数。

    各门禁所需参数:
      g_entry: agent="unity-developer"
      g_mode:  mode="Production", reason="功能开发"
      g_script: decision="USE scene-query.cs"
      g_file:  file_type=".shader", category="PostProcess", effect="Kuwahara"
      g_web_search: query="搜索关键词", summary="搜索结果摘要"
      g_plan:  plan_summary="方案描述..."

    Args:
        gate_id: 门禁 ID (如 "g_entry", "g_mode", "g_script", "g_file", "g_web_search", "g_plan")
    """
    kwargs = {k: v for k, v in locals().items()
              if k not in ("gate_id",) and v}  # 跳过空值
    return json.dumps(state.pass_gate(gate_id, **kwargs), ensure_ascii=False)


# ═══ gate_status / gate_list / gate_reset ═══

@server.tool()
async def gate_status() -> str:
    """查看当前配方和门禁通过状态."""
    return json.dumps({
        "recipe": state.recipe,
        "passed": sorted(state.passed),
        "remaining": state.remaining,
        "contexts": {k: v for k, v in state.contexts.items()},
    }, ensure_ascii=False)


@server.tool()
async def gate_list() -> str:
    """列出所有可用门禁和配方."""
    return json.dumps({
        "gates": {gid: {"name": d["name"], "requires": d["requires"]}
                  for gid, d in GATE_REGISTRY.items()},
        "recipes": RECIPES,
    }, ensure_ascii=False)


@server.tool()
async def gate_reset() -> str:
    """重置门禁状态（新任务开始）."""
    state.reset()
    return json.dumps({"status": "OK", "message": "已重置。请 gate_set_recipe(name) 开始新任务。"})


# ═══ script_list — 脚本库 ═══

@server.tool()
async def script_list() -> str:
    """列出 scripts/roslyn/ 中的所有可用脚本."""
    scripts = list_scripts()
    return json.dumps({"scripts": scripts, "count": len(scripts)}, ensure_ascii=False)


# ═══ write_gated — 带门禁写入 ═══

@server.tool()
async def write_gated(path: str, content: str) -> str:
    """门禁校验后写入文件。配方门禁全部通过后才放行。

    Quick 模式下跳过此工具，直接用原生 Write。

    Args:
        path: 目标文件路径（相对于项目根目录）
        content: 文件内容
    """
    path_check = validate_path(path)
    if path_check["status"] != "OK":
        return json.dumps(path_check, ensure_ascii=False)

    gate_check = state.can_write()
    if gate_check["status"] != "OK":
        return json.dumps(gate_check, ensure_ascii=False)

    full_path = path_check.get("full_path", path)
    try:
        os.makedirs(os.path.dirname(full_path), exist_ok=True)
        with open(full_path, "w", encoding="utf-8") as f:
            f.write(content)
        return json.dumps({
            "status": "OK", "written": path,
            "bytes": len(content.encode("utf-8")),
            "recipe": state.recipe, "passed": sorted(state.passed),
        }, ensure_ascii=False)
    except Exception as e:
        return json.dumps({"status": "ERROR", "error": str(e),
                           "hint": "文件写入失败。"})


# ═══ main ═══

async def main():
    try:
        async with stdio_server() as (read_stream, write_stream):
            await server.run(read_stream, write_stream)
    except Exception as e:
        # stdio server 需要 MCP client 连接 — 直接运行会报错是正常的
        import sys
        print(f"MCP server stopped: {e}", file=sys.stderr)
        print("This server is started automatically by Claude Code via .mcp.json.", file=sys.stderr)
        print("To test: uv run python tests/test_recipes.py", file=sys.stderr)


if __name__ == "__main__":
    asyncio.run(main())
