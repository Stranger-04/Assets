# Cross-Reference Rules

> 维护 .claude 体系跨引用一致性的操作规则。

---

## 新增引用时

```bash
# 1. 在源文件中添加引用
#    例如 agent 中添加: - **Skill 入口**：../skills/auto-manager/SKILL.md

# 2. 验证目标文件存在
ls .claude/skills/auto-manager/SKILL.md

# 3. 在被引用文件中添加反向引用（如果已有跨引用段）
#    例如在 SKILL.md 或 AutoMode.md 中添加指向 agent 的引用

# 4. 全局检查无断链
grep -rn "\[.*\](.*\.md)" .claude/ | while read line; do
    # 提取路径，验证存在
done
```

## 删除文件时

```bash
# 1. 先找到所有引用该文件的地方
grep -rn "目标文件名" .claude/ --include="*.md"

# 2. 更新所有引用
#    删除 → 改为指向新位置，或移除引用行

# 3. 再次 grep 确认无残留引用
grep -rn "目标文件名" .claude/ --include="*.md"
# 应该返回空
```

## 重命名文件时

```bash
# 1. 找到所有引用
grep -rn "旧文件名" .claude/ --include="*.md"

# 2. 批量替换（谨慎）
sed -i '' 's/旧文件名/新文件名/g' <file1> <file2> ...

# 3. 验证
grep -rn "旧文件名" .claude/ --include="*.md"  # 应为空
grep -rn "新文件名" .claude/ --include="*.md"  # 应有结果
```

## 常用检查命令

```bash
# 检查是否还有指向已删除 rules/ 的引用
grep -rn "rules/" .claude/skills/ --include="*.md"

# 列出所有跨引用
grep -rn "\[.*\](.*\.\./.*\.md)" .claude/ --include="*.md"

# 验证所有 .md 文件路径
find .claude -name "*.md" | while read f; do
    echo "=== $f ==="
    grep -oP '\[.*?\]\(\K[^)]+\.md' "$f" | while read ref; do
        target=$(dirname "$f")/$ref
        [ -f "$target" ] || echo "  BROKEN: $target"
    done
done
```
