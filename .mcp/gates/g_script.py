"""G2: 脚本决策 — USE 已有 / CREATE reusable / CREATE tmp.

依赖: validation/script_library
"""

from validation.script_library import validate_decision, list_scripts


def check(ctx: dict) -> dict:
    decision = ctx.get("decision", "").strip()
    if not decision:
        available = [s["name"] for s in list_scripts()]
        return {
            "status": "DENIED",
            "error": "G2_NO_DECISION",
            "hint": f"请提供 decision。可用脚本: {available}。格式: 'USE scene-query.cs'",
        }
    return validate_decision(decision)
