# Roslyn Script Execution Reference

> C# Roslyn 脚本模板 + unityctl 命令速查。CLI 层文档。

---

## 快速命令速查

```bash
# 状态检查
unityctl status

# 编译
unityctl asset refresh

# Play Mode
unityctl play enter
unityctl play exit

# 日志（进入 Play Mode 后自动清除，只显示当前会话日志）
unityctl logs -n 30
unityctl logs --stack       # 带堆栈

# 场景
unityctl scene list
unityctl scene load Assets/Scenes/xxx.unity

# 截图
unityctl screenshot capture

# 执行脚本
unityctl script execute -f path/to/script.cs
```

---

## 调试食谱

### 运行时检查组件状态

```bash
unityctl script execute -f tmp/debug_state.cs
```

### 运行时执行方法

```csharp
// 调用任意 public 方法
go.GetComponent<SomeComponent>().SomePublicMethod();
```

---

## 脚本模板

可复用的 Roslyn 脚本位于 [../scripts/roslyn/](../scripts/roslyn/)：

| 脚本 | 用途 |
|------|------|
| [scene-query.cs](../scripts/roslyn/scene-query.cs) | 通用场景层级遍历 |
| [pipeline-check.cs](../scripts/roslyn/pipeline-check.cs) | 渲染管线诊断 |
| [scene-organize.cs](../scripts/roslyn/scene-organize.cs) | 测试物体分组整理 |

---

## 引用

- 完整 unityctl 命令参考：[unityctl.md](unityctl.md)
- 场景配置脚本模板：../skills/auto-manager/capabilities/scene-setup.md
- 清理 Roslyn 脚本：../skills/auto-manager/capabilities/cleanup.md
- 可复用脚本策略 (`tmp/.reusable/`)：../skills/auto-manager/capabilities/cleanup.md
