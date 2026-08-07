"""G_PLAN: 方案设计门禁.

Experiment Mode 专用。要求 agent 在搜索后输出修改计划。
"""


def check(ctx: dict) -> dict:
    plan = ctx.get("plan_summary", "").strip()

    if not plan:
        return {
            "status": "DENIED",
            "error": "G_PLAN_NO_PLAN",
            "hint": "请提供方案摘要 (plan_summary)。格式: '修改涉及 XX 文件，步骤: 1. ... 2. ...'",
        }

    # 太短的方案视为无效
    if len(plan) < 20:
        return {
            "status": "DENIED",
            "error": "G_PLAN_TOO_SHORT",
            "hint": f"方案太短 ({len(plan)} 字符)。至少 20 字符。请详细说明修改计划和涉及文件。",
        }

    return {
        "status": "OK",
        "plan_summary": plan[:1000],
    }
