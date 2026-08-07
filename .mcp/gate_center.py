"""Gate Center — 门禁注册中心.

只维护: 门禁注册表 + 配方表 + 状态追踪。
Gate 之间无耦合 — 可随机调用，配方只定义顺序。
"""

from __future__ import annotations
from dataclasses import dataclass, field
from importlib import import_module
from typing import Optional


# ═══ 注册表 — 门禁元信息（Gate 模块路径）═══

GATE_REGISTRY: dict[str, dict] = {
    "g_entry":     {"name": "框架入口", "module": "gates.g_entry",
                    "requires": [], "context_keys": ["agent"]},
    "g_mode":      {"name": "模式确认", "module": "gates.g_mode",
                    "requires": ["g_entry"], "context_keys": ["mode", "reason"]},
    "g_script":    {"name": "脚本决策", "module": "gates.g_script",
                    "requires": ["g_mode"], "context_keys": ["decision"]},
    "g_file":      {"name": "文件放置", "module": "gates.g_file",
                    "requires": ["g_mode"], "context_keys": ["file_type", "category", "effect"]},
    "g_web_search":{"name": "联网搜索", "module": "gates.g_web_search",
                    "requires": ["g_mode"], "context_keys": ["query", "summary"]},
    "g_plan":      {"name": "方案设计", "module": "gates.g_plan",
                    "requires": ["g_web_search"], "context_keys": ["plan_summary"]},
}


# ═══ 配方表 — 有序的 Gate ID 列表 ═══

RECIPES: dict[str, list[str]] = {
    "Production":  ["g_entry", "g_mode", "g_script", "g_file"],
    "Research":    ["g_entry", "g_mode"],
    "Experiment":  ["g_entry", "g_mode", "g_web_search", "g_plan", "g_script", "g_file"],
    "Debug":       ["g_entry", "g_mode", "g_script"],
    "Minimal":     ["g_entry"],
    "Quick":       [],   # 绿色通道 — 无门禁，以完成为优先
}


# ═══ 惰性加载 Gate 模块 ═══

_gate_cache: dict[str, object] = {}

def _load_gate(gate_id: str):
    if gate_id not in _gate_cache:
        _gate_cache[gate_id] = import_module(GATE_REGISTRY[gate_id]["module"])
    return _gate_cache[gate_id]


# ═══ SessionState ═══

@dataclass
class SessionState:
    recipe: Optional[str] = None
    passed: set[str] = field(default_factory=set)
    contexts: dict = field(default_factory=dict)

    @property
    def remaining(self) -> list[str]:
        if not self.recipe:
            return []
        return [g for g in RECIPES.get(self.recipe, []) if g not in self.passed]

    def set_recipe(self, name: str) -> dict:
        if name not in RECIPES:
            return {"status": "DENIED", "error": "INVALID_RECIPE",
                    "hint": f"Recipe 必须为 {list(RECIPES.keys())} 之一。"}
        self.recipe = name
        self.passed.clear()
        self.contexts.clear()
        return {"status": "OK", "recipe": name, "gates": RECIPES[name],
                "names": {g: GATE_REGISTRY[g]["name"] for g in RECIPES[name]}}

    def pass_gate(self, gate_id: str, **kwargs) -> dict:
        if gate_id not in GATE_REGISTRY:
            return {"status": "DENIED", "error": "INVALID_GATE",
                    "hint": f"Gate 必须为 {list(GATE_REGISTRY.keys())} 之一。收到: '{gate_id}'"}
        if not self.recipe:
            return {"status": "DENIED", "error": "NO_RECIPE",
                    "hint": "请先 gate_set_recipe(name)。"}
        if gate_id not in RECIPES[self.recipe]:
            return {"status": "DENIED", "error": "GATE_NOT_IN_RECIPE",
                    "hint": f"'{gate_id}' 不在配方 '{self.recipe}' 中。配方: {RECIPES[self.recipe]}"}

        # 检查前置
        for req in GATE_REGISTRY[gate_id]["requires"]:
            if req not in self.passed:
                return {"status": "DENIED", "error": "PREREQUISITE",
                        "hint": f"'{gate_id}' 需先通过 '{req}'。已通过: {sorted(self.passed)}"}

        # 收集上下文 → 调用 gate.check()
        ctx = {k: v for k, v in kwargs.items()
               if k in GATE_REGISTRY[gate_id]["context_keys"]}
        gate_mod = _load_gate(gate_id)
        result = gate_mod.check(ctx)

        if result.get("status") == "OK":
            self.passed.add(gate_id)
            self.contexts[gate_id] = ctx
            result["gate_name"] = GATE_REGISTRY[gate_id]["name"]
            result["remaining"] = self.remaining
        return result

    def can_write(self) -> dict:
        if not self.recipe:
            return {"status": "DENIED", "error": "NO_RECIPE",
                    "hint": "请先 gate_set_recipe(name)。"}
        missing = self.remaining
        if missing:
            return {"status": "DENIED", "error": "GATE_NOT_PASSED",
                    "missing": missing, "passed": sorted(self.passed),
                    "hint": f"配方 '{self.recipe}' 还需通过: {missing}"}
        return {"status": "OK", "recipe": self.recipe, "passed": sorted(self.passed)}

    def reset(self) -> None:
        self.recipe = None
        self.passed.clear()
        self.contexts.clear()


state = SessionState()
