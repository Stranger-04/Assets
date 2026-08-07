"""Gate protocol: check(ctx) -> {status, ...}.

Each gate:
- Imports nothing from state_machine
- Only depends on validation/ modules for domain checks
- Returns structured dict: {"status": "OK"} or {"status": "DENIED", "error": ..., "hint": ...}
"""
