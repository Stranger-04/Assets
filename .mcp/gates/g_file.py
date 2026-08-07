"""G3: 文件放置 — 类型 + 类别 → 合法路径。

依赖: validation/project_paths
"""

from validation.project_paths import resolve_target_dir


def check(ctx: dict) -> dict:
    file_type = ctx.get("file_type", "").strip()
    category = ctx.get("category", "").strip()
    effect = ctx.get("effect", "").strip()
    return resolve_target_dir(file_type, category, effect)
