# screenshot — 截图留档（按需）

> 按需手动触发，不在自动化流水线中。
> 仅当用户明确要求截图或需要人工判断画面效果时使用。

---

## 命令

```bash
unityctl screenshot capture
# 输出：Screenshots/screenshot_YYYY-MM-DD_HH-MM-SS.png
```

## 使用场景

- 用户明确要求截图
- Research Mode 中需要对比前后效果
- 需要人工判断的渲染效果

## 截图缓存清理

```bash
ls Screenshots/screenshot_*.png
```

清理策略见 [cleanup.md](cleanup.md) 轻清理流程。
