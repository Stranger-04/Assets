#!/bin/bash
# post-session hook: 会话结束后更新 memory 和 learnings
# 由 Claude Code 会话结束时可选触发

MEMORY_FILE="$(dirname "$0")/../../memory/MEMORY.md"
LEARNINGS_DIR="$(dirname "$0")/../../learnings"

# 追加会话摘要到 MEMORY.md
# 格式：| 日期 | 摘要 |
echo "| $(date +%Y-%m-%d) | <session-summary> |" >> "$MEMORY_FILE"

# 如果 learnings 有更新，提示
echo "Session ended. Check $LEARNINGS_DIR for new learnings to capture."
