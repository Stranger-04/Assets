# safety — 安全红线 + 经验教训

> AutoMode 操作中绝对不能违反的规则，以及从历史踩坑中总结的经验。

---

## 安全红线

### 1. 禁止 `git stash --all`

`git stash --all` 会同时暂存并**删除**未跟踪的新文件，包括 `Assets/` 下的已完成代码。恢复时可能因冲突失败。

✅ 只能用 `git stash push -- <paths>` 指定精确路径：

```bash
# 备份临时文件
git stash push -m "cleanup-$(date +%Y%m%d-%H%M%S)" -- tmp/ Screenshots/
```

### 2. 不碰 Assets/ 下的已完成代码

清理只针对 `tmp/`、`Screenshots/`、场景测试物体。绝不删除 `Assets/Mine/` 下的功能代码、Shader、Compute、ScriptableObject。

### 3. 先列后删

任何涉及删除的操作，必须先列出完整清单，经人工确认后再执行。

### 4. 跳过 .meta 关联删除

删除资源文件时同步删除对应 `.meta` 文件，避免残留孤立 meta。

### 5. 场景物体用脚本清理

不直接 `rm`，通过 `unityctl script execute` 在 Editor 中 `DestroyImmediate`。

### 6. 保留用户代码

仅清理明确标记为临时/测试的内容，不动用户手写的正式代码。

---

## 经验教训

| 教训 | 说明 |
|------|------|
| **禁止 `git stash --all`** | 会删除所有未跟踪文件（包括 `Assets/Mine/Scripts/` 下新创建的代码），恢复时可能因冲突失败 |
| **备份必须精确** | `git stash push -- tmp/ Screenshots/` 只备份临时文件，不动 `Assets/` |
| **清理前先确认** | 删除任何东西前先列出完整清单，人工确认后再执行 |
| **场景物体用脚本** | 不直接 `rm`，通过 `unityctl script execute` 在 Editor 中 `DestroyImmediate` |
| **重清理不删设计文档** | 功能文件夹下的 `Plan.md` 是设计文档，属于项目资产，不应删除 |
| **可复用脚本存 .reusable/** | 场景查询、管线检查等通用功能脚本放入 `tmp/.reusable/`，清理时跳过该目录 |
| **测试物体按框架下挂** | 同一框架的测试物体挂到对应父容器下，不散落在根级 |
| **渐进式清理** | 先轻后重，轻清理可在任何阶段执行，重清理仅在最终确认后 |
