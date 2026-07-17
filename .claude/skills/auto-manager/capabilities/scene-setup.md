# scene-setup — 场景配置

> 通过 Roslyn 脚本在 Editor 中执行 C#，创建/配置场景物体。

---

## 命令

```bash
unityctl script execute -f tmp/setup_xxx.cs
```

## 脚本模板

```csharp
using UnityEngine;

public class Script
{
    public static object Main()
    {
        // 创建物体
        var go = new GameObject("TestObj");

        // 添加组件
        var comp = go.AddComponent<SomeComponent>();

        // 通过 AssetDatabase 加载资源
        var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Path/To/Shader.compute");
        comp.someField = shader;

        // 调用初始化
        comp.Init();

        return "OK: 描述结果";
    }
}
```

## 适用场景

- 创建/删除 GameObject
- 添加/移除 Component
- 修改 Inspector 中的公开字段
- 调用 public 方法（如 `InitInstances()`）
- 读取组件状态进行调试

## 调试期接口指向

若 Feature 内存在需要外部资源引用的接口（`Shader`、`Material`、`ComputeShader`、`RenderTexture` 等），**可直接将接口指向项目中已有的资源地址**，跳过"先创建空资源再回填引用"的步骤。

| 场景 | 示例 |
|------|------|
| Shader 字段 | `public Shader targetShader;` → Inspector 指向 `Assets/Mine/Shaders/PCSS/PCSS.shader` |
| Material 引用 | `public Material debugMat;` → 指向已有的 `.mat` |
| ComputeShader | `public ComputeShader cs;` → 指向已有 `.compute` |
| RenderTexture | 直接指定已有的 RT 资产路径 |

**标记格式：**

```csharp
// DEBUG_REF: 设计完成后清空此引用
public Shader targetShader;
```

设计结束后清空所有 `DEBUG_REF` 标记的引用。

## 场景整理规范

同一框架的测试物体统一挂在功能父容器下，不散落在场景根级。

```
场景层级结构：
  InstanceManager/                    ← 框架名（空物体，无组件）
  ├── FishTest
  └── RainTest

  __TestObjects__/                    ← 其他散落测试物体（重清理时归类）
  └── __Picker__/...
```

| 规则 | 说明 |
|------|------|
| 父容器按框架命名 | 如 `InstanceManager`，不是 `__TestObjects__` |
| 子物体按功能命名 | `FishTest`、`RainTest`，清晰表示正在测试的功能 |
| 根级不散落 | 任何含 `Test`/`Temp`/`Debug` 的物体都应在容器下 |
| 创建时即归类 | 新测试物体直接在父容器下创建 |

## 引用

- 临时脚本管理规则见 [cleanup.md](cleanup.md) 中的 `tmp/.reusable/` 策略
- 场景整理 Roslyn 脚本见 [cleanup.md](cleanup.md) 重清理部分
