#!/usr/bin/env bash
# PreToolUse hook — guard dangerous Bash commands
# Reads JSON from stdin: { "tool_name": "Bash", "tool_input": { "command": "..." } }
# Exit 2 = block, Exit 0 = allow

INPUT=$(cat)
CMD=$(echo "$INPUT" | python3 -c "import sys,json; print(json.load(sys.stdin).get('tool_input',{}).get('command',''))" 2>/dev/null)

# ═══ Hard blocks (exit 2) ═══

# C1: git stash --all permanently forbidden
if echo "$CMD" | grep -q "git stash.*--all"; then
    echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"C1 安全红线: git stash --all 永久禁止。使用 git stash push -- <paths> 指定精确路径。"}}'
    exit 2
fi

# C2: No destructive ops on Assets/Mine/ (rm -rf only, cp/mv allowed)
if echo "$CMD" | grep -qE "rm -rf.*Assets/Mine/"; then
    echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"C2 安全红线: 禁止删除 Assets/Mine/ 下功能代码。清理仅限 tmp/ 和 Screenshots/。"}}'
    exit 2
fi

exit 0
