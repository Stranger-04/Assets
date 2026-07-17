# Roslyn Recipes — 调试食谱

> Roslyn 脚本模板 + unityctl 命令速查。纯参考手册，不参与自动化流程。

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

```csharp
using UnityEngine;

public class Script
{
    public static object Main()
    {
        var go = GameObject.Find("TargetName");
        if (go == null) return "NOT FOUND";

        var comp = go.GetComponent<SomeComponent>();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"fieldA: {comp?.fieldA}");
        sb.AppendLine($"fieldB: {comp?.fieldB}");
        return sb.ToString();
    }
}
```

### 运行时执行方法

```csharp
// 调用任意 public 方法
go.GetComponent<SomeComponent>().SomePublicMethod();
```

### 通用场景层级查询 (tmp/.reusable/)

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        sb.AppendLine($"根物体数量: {roots.Length}");

        foreach (var root in roots)
        {
            PrintHierarchy(root.transform, "", sb);
        }
        return sb.ToString();
    }

    static void PrintHierarchy(Transform t, string indent, System.Text.StringBuilder sb)
    {
        sb.AppendLine($"{indent}{t.name} [{t.GetComponents<Component>().Length} components]");
        foreach (Transform child in t)
        {
            PrintHierarchy(child, indent + "  ", sb);
        }
    }
}
```

### 通用管线检查 (tmp/.reusable/)

```csharp
using UnityEngine;
using UnityEngine.Rendering;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        var pipeline = GraphicsSettings.currentRenderPipeline;
        sb.AppendLine($"Render Pipeline: {pipeline?.GetType().Name ?? "Built-in"}");
        sb.AppendLine($"Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        return sb.ToString();
    }
}
```

---

## 引用

- 场景配置脚本模板：[../capabilities/scene-setup.md](../capabilities/scene-setup.md)
- 清理 Roslyn 脚本：[../capabilities/cleanup.md](../capabilities/cleanup.md) 附录部分
- 可复用脚本管理：[../capabilities/cleanup.md](../capabilities/cleanup.md) 中的 `tmp/.reusable/` 策略
