# unityctl Command Reference

> Unity Editor 远程控制 CLI。完整命令层级参考。

---

## 命令层级

### Status & Bridge

```bash
unityctl status              # 检查 Unity、bridge、连接状态
unityctl bridge start/stop   # 管理 bridge 守护进程
```

### Editor Lifecycle

```bash
unityctl editor run/stop     # 启动或停止 Unity Editor
unityctl wait                # 阻塞等待 Unity 连接（最长 120s）
unityctl wait --timeout 300  # 自定义超时
```

### Compilation

```bash
unityctl asset refresh       # 编译脚本，失败时返回错误
```

### Play Mode

```bash
unityctl play enter/exit     # 进入/退出 Play Mode
unityctl play pause          # 切换暂停（Edit Mode 也可用——预置 play 时暂停）
unityctl play step           # 前进一帧（仅 Play Mode）
```

### Logs & Diagnostics

```bash
unityctl logs                # 显示自上次清除以来的日志（编译/play 时自动清除）
unityctl logs -n 50          # 限制条目数
unityctl logs --stack        # 包含堆栈
unityctl logs --full         # 忽略清除边界
```

### Scenes

```bash
unityctl scene list          # 列出场景
unityctl scene load <path>   # 加载场景
unityctl scene load <path> --additive  # 加性加载
```

### Script Execution

```bash
# 直接 eval C# 表达式
unityctl script eval 'Application.version'
unityctl script eval 'GameObject.FindObjectsOfType<Camera>().Length'
unityctl script eval --id -1290 'target.transform.position'
unityctl script eval -u UnityEngine.SceneManagement 'SceneManager.GetActiveScene().name'

# 执行 .cs 文件
unityctl script execute /tmp/MyScript.cs
unityctl script execute /tmp/SpawnObjects.cs -- Cube 5 'My Object'

# 超时控制（默认 30s）
unityctl script eval -t 600 -u UnityEditor 'return BuildPipeline.BuildPlayer(opts).summary.result.ToString();'

# 异步
unityctl script eval 'await Task.Delay(500); return GameObject.Find("Boss") != null;'

# 类型查找
unityctl script lookup-type <Name>
unityctl script members <Type> [--filter X] [--static]
```

### Screenshots & Recording

```bash
unityctl screenshot capture              # 捕获 Game View
unityctl screenshot list-windows         # 列出 Editor 窗口
unityctl screenshot window <window>      # 按类型或标题捕获特定窗口
unityctl record start                    # 开始录制（手动停止）
unityctl record start --duration 10      # 录制 10 秒，阻塞至完成
unityctl record stop                     # 停止录制，返回文件路径 + 时长
```

### Scene Snapshot

```bash
unityctl snapshot                          # 场景层级树（默认深度 2）
unityctl snapshot --depth 4                # 更深遍历
unityctl snapshot --id 14200 --components  # 展开一个物体及所有属性
unityctl snapshot --screen                 # 包含屏幕空间边界和可见性
unityctl snapshot --filter "type:Rigidbody"  # 过滤
unityctl snapshot query 400 300            # 屏幕坐标 (400,300) 处是什么 UI 元素
```

### UI Interaction (Play Mode only)

```bash
unityctl ui click --name "StartButton"    # 按名称查找并点击（推荐）
unityctl ui click --id 14200              # 按 instance ID 点击
unityctl ui click 400 300                 # 按屏幕坐标点击
```

### Prefab Editing

```bash
unityctl prefab open Assets/Prefabs/Player.prefab
unityctl prefab close / close --save / close --discard
```

### Dialog Detection

```bash
unityctl dialog list                   # 列出检测到的弹出对话框
unityctl dialog dismiss                # 关闭第一个对话框
unityctl dialog dismiss --button "OK"  # 点击特定按钮
```

---

## 典型工作流

```bash
unityctl asset refresh       # 编辑 C# 后编译
unityctl snapshot            # 验证场景状态（结构化、低成本）
unityctl play enter
unityctl snapshot            # 运行时状态检查
unityctl logs                # 检查错误/警告
unityctl play exit
# 仅在需要判断视觉效果时截图
```

## 故障排除

| 问题 | 解决方案 |
|------|---------|
| Bridge 无响应 | `unityctl bridge stop && unityctl bridge start` |
| Editor 未连接 | 正常 — 指数退避，最长 15 秒 |
| 编译后连接断开 | 正常 — domain reload，自动重连 |
| "Project not found" | `unityctl setup` 或 `unityctl config set project-path <path>` |
| 不确定 Unity 何时就绪 | `unityctl wait --timeout 300` |
| 命令超时 | 可能原生对话框阻塞：`unityctl dialog list` |
| 进度条卡住 | `unityctl dialog list` 检查，等待或关闭 |

## 最佳实践

90. **结构化优于截图**：能用 `snapshot`、`logs`、`script eval` 验证的，不用 `screenshot`
1. **快照优于评估**：用 `snapshot` 观察场景，`ui click` 交互，`eval --id` 定制操作
2. **名称优先于 ID**：`--name` 比 `--id` 更稳定（instance ID 在 Play Mode 间会变）
3. **总是用 Write 工具创建 .cs 文件**：不用 shell heredoc（在 C# 单引号处会断）
