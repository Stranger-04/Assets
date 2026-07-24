# Unity Editor Platform

> Unity Editor 平台的 Bridge 配置、生命周期和可用性策略。

---

## Bridge 配置

```bash
unityctl status                # 检查当前状态
unityctl bridge start          # 启动 bridge 守护进程（幂等，已运行则跳过）
unityctl bridge stop           # 停止 bridge
unityctl editor run            # 启动 Unity Editor（或手动打开项目）
unityctl wait                  # 阻塞等待 Unity 连接（最长 120s）
```

---

## 可用性状态

| 状态 | Agent 行为 |
|------|-----------|
| ✅ **已连接** (Editor running + bridge connected) | 完整流水线：知识加载 → 代码编辑 → 编译验证 → Play Mode → 清理 |
| ❌ **未连接** (Editor not running) | 精简流水线：知识加载 → 代码编辑。跳过编译/运行/清理。报告末尾注明"Editor 未运行，未执行编译验证"。 |

---

## 连接管理

### 自动重连

- Editor 编译导致 domain reload → 自动重连（正常现象）
- Bridge 断开检测：`unityctl status` 返回 disconnected
- 重连策略：指数退避，最长等待 15 秒

### 阻塞检测

- 命令超时 → 检查是否有原生对话框阻塞 Unity
- `unityctl dialog list` → 列出检测到的弹出对话框
- `unityctl dialog dismiss` → 关闭对话框（点击第一个按钮）
- `unityctl dialog dismiss --button "OK"` → 点击特定按钮

### 进度条

- Unity 导入资源、编译时可能显示进度条
- 用 `unityctl dialog list` 检查，等待或关闭

---

## 验证工具选择策略

| 验证目标 | 推荐工具 | 说明 |
|---------|---------|------|
| 场景层级、组件、属性 | `snapshot` (--components, --filter) | 结构化、低成本 |
| UI 布局、可见性、屏幕位置 | `snapshot --screen` | 精确坐标 |
| 运行时行为、错误、警告 | `logs` | 文本、可搜索 |
| 特定值或状态 | `script eval` | 直接查询 |
| 测试正确性 | `test run` | 自动化 |
| **视觉效果**（美术、Shader、布局） | `screenshot capture` | 仅在视觉是验证目标时使用 |

> **原则**：能用结构化工具验证的，不用截图。截图的上下文成本高且难以 diff。

---

## 跨引用

- **Agent 宪法**：../agents/unity-developer.md（模式选择逻辑 + 退出条件）
- **CLI 命令**：../cli/unityctl.md（完整命令层级参考）
- **Skill**：../skills/auto-manager/（Editor 可用性影响流水线步骤）
