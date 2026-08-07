"""G_WEB: 联网搜索门禁.

Experiment Mode 专用。要求 agent 执行 WebSearch 并将结果摘要传入。
不实际执行搜索 — agent 自行调 WebSearch 后把结果传入本门禁。
"""


def check(ctx: dict) -> dict:
    query = ctx.get("query", "").strip()
    summary = ctx.get("summary", "").strip()

    if not query:
        return {
            "status": "DENIED",
            "error": "G_WEB_NO_QUERY",
            "hint": "请提供搜索关键词 (query)。示例: gate_pass('g_web', query='Unity URP compute shader metal thread group')",
        }

    if not summary:
        return {
            "status": "DENIED",
            "error": "G_WEB_NO_SUMMARY",
            "hint": "请执行 WebSearch 后将结果摘要传入 (summary)。门禁不自动搜索，只验证搜索已完成。",
        }

    return {
        "status": "OK",
        "query": query,
        "summary": summary[:500],  # 截断保护
    }
