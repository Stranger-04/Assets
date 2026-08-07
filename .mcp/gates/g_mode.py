"""G1: 模式确认."""

ALLOWED_MODES = {"Production", "Research", "Experiment", "Debug", "Minimal"}


def check(ctx: dict) -> dict:
    mode = ctx.get("mode", "").strip()
    if mode not in ALLOWED_MODES:
        return {
            "status": "DENIED",
            "error": "G1_FAILED",
            "hint": f"Mode 必须为 {sorted(ALLOWED_MODES)} 之一。收到: '{mode}'。",
        }
    return {"status": "OK", "mode": mode, "reason": ctx.get("reason", "")}
