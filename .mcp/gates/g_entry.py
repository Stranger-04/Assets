"""G0: 框架入口 — 确认 agent 在正确上下文中."""

ALLOWED_AGENTS = {"unity-developer", "meta-developer"}


def check(ctx: dict) -> dict:
    agent = ctx.get("agent", "").strip()
    if agent not in ALLOWED_AGENTS:
        return {
            "status": "DENIED",
            "error": "G0_FAILED",
            "hint": f"Agent 必须为 {ALLOWED_AGENTS} 之一。收到: '{agent}'。请先 Read 对应 agent 定义文件。",
        }
    return {"status": "OK", "agent": agent}
