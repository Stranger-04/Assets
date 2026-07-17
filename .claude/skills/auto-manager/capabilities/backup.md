# backup — 重大改动前备份

> 架构级改动或大量修改前，建立实体备份 + Git 备份双保险。

---

## 触发条件

满足以下任一条件时，**必须先备份再动手**：

| 触发条件 | 示例 |
|----------|------|
| 架构级改动 | 单级联→多级联、Shader→Compute Shader、Feature 重构 |
| 同一文件修改超 50 行 | 重写 RecordRenderGraph、替换投影算法 |
| 用户明确要求备份 | "先备份再改" |
| 文件从未被 git 提交 | `git status` 显示 `??` 的资产文件 |

> 研发模式**默认跳过备份**（依赖 git 历史 + 改动粒度小）。

---

## 备份流程

```
重大改动触发
  │
  ├── [0a] 实体备份（方便对比检查）
  │     ├── mkdir Assets/path/to/feature/.backup_v<序号>_<描述>/
  │     ├── cp 所有相关文件到备份目录
  │     ├── echo "" > .backup_v<序号>_<描述>/.gitignore  （Unity 自动忽略 . 开头目录）
  │     └── echo "备份说明" > .backup_v<序号>_<描述>/README.txt
  │
  └── [0b] Git 备份（方便回退）
        ├── git add <改动文件>
        └── git commit -m "<Feature>: <改动简述>（备份基准）"
```

## 命名规范

- 格式：`.backup_v<序号>_<简短描述>/`
- 示例：`.backup_v1_single_cascade/`、`.backup_v2_before_compute/`
- Unity 自动忽略 `.` 开头的目录，不会被导入为资产

## 回退方式

```bash
# 实体对比：直接 diff 两个目录
diff Assets/xxx/.backup_v1_xxx/CurrentFile.cs Assets/xxx/CurrentFile.cs

# Git 回退：找到备份 commit 的 hash
git log --oneline -10
git checkout <commit_hash> -- Assets/xxx/File.cs
```
