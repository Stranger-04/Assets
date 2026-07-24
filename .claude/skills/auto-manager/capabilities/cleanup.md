# Cleanup — 清理系统

> 两级清理模式：轻清理（阶段性完成后的快速整理）和重清理（全部任务完成后的深度清理）。

---

## 清理模式对比

| 维度 | 🧹 轻清理 (Light Cleanup) | 🧹🧹 重清理 (Deep Cleanup) |
|------|--------------------------|---------------------------|
| **触发时机** | 任意阶段性功能设计/调试完成后 | 全部任务确认完成后 |
| **执行条件** | 可随时执行，无需确认 | 必须人工确认后才执行 |
| **影响范围** | 临时文件、调试产物 | 临时代码、冗余代码、场景结构、文件组织 |
| **可逆性** | 可逆（仅清理明确标记的临时内容） | 部分不可逆（删除冗余文件和代码） |
| **触发命令** | `/clean light` 或自动触发 | `/clean deep`（需二次确认） |

### 触发方式

轻清理可在以下任一节点自动提示执行：

```
[P4] 编译验证通过 → "编译通过，是否需要轻清理？"
[P6] 运行时验证通过 → "运行时零错误，是否需要轻清理？"
```

重清理仅在任务全部完成后，由用户主动触发。

---

## 🧹 轻清理 (Light Cleanup)

### 清理清单

| 类别 | 目标 | 操作 | 自动化 |
|------|------|------|--------|
| **临时脚本** | `tmp/*.cs` | 删除所有临时脚本（**保留 `tmp/.reusable/` 目录**） | ✅ 全自动 |
| **调试日志** | `Debug.Log` / `print()` | 搜索并移除无条件的调试输出 | ⚠️ 列出后人工确认 |
| **截图缓存** | `Screenshots/*.png` | 清理本次会话产生的截图 | ✅ 全自动 |
| **注释标记** | `// TODO` / `// FIXME` / `// HACK` | 列出并提醒处理 | ❌ 仅报告 |
| **临时 GameObject** | Hierarchy 中以 `_Temp` / `_Debug` 开头的物体 | 列出并询问是否删除 | ⚠️ 列出后人工确认 |
| **未引用的临时资源** | Assets 中以 `Temp_` / `_Test` 命名的文件 | 列出并询问是否删除 | ⚠️ 列出后人工确认 |

### 轻清理流程

```
轻清理触发
  │
  ├── [L1] 扫描 tmp/ 目录 → 删除 .cs 文件（跳过 tmp/.reusable/）
  │     └── tmp/.reusable/ 下的脚本永久保留，不纳入清理
  │
  ├── [L2] 扫描 Screenshots/ → 删除本次会话截图
  │     └── 识别方式：文件创建时间在本次会话范围内
  │
  ├── [L3] 搜索项目中的调试日志
  │     └── grep -rn "Debug\.Log\|print(" Assets/Mine/ --include="*.cs"
  │     └── 列出文件+行号，询问是否移除
  │
  ├── [L4] 搜索临时 GameObject
  │     └── unityctl script execute 扫描 Hierarchy
  │     └── 列出 _Temp / _Debug 前缀的物体
  │
  └── [L5] 报告清理结果
        └── "轻清理完成：删除 X 个临时脚本、Y 张截图、发现 Z 个待处理项"
```

---

## 可复用临时脚本 (tmp/.reusable/)

`tmp/` 根目录下的 `.cs` 文件为一次性测试脚本，轻清理时全部删除。
**`tmp/.reusable/` 目录下的脚本永久保留**，轻清理和重清理都不动它。

### 存放标准

| 保留（.reusable/） | 清理（tmp/ 根目录） |
|------|------|
| 纯工具脚本：场景查询、组件检查、环境诊断 | 功能绑定脚本：setup_xxx、debug_xxx、tune_xxx |
| 不依赖具体类名/资源路径 | 硬编码了具体功能路径（如 `FishSimulation`、`Rain.compute`） |
| 可跨项目复用 | 仅对当前功能有效 |

| 保留示例 | 清理示例 |
|------|------|
| `query_scene.cs` — 通用场景层级查询 | `setup_fish.cs` — 绑定 FishSimulation |
| `organize_scene.cs` — 通用测试物体归类 | `setup_rain.cs` — 绑定 RainSimulation |
| `check_renderer.cs` — 通用管线检查 | `debug_trajectory.cs` — 绑定 Trajectory 功能 |
| | `tune_fishforce.cs` — 绑定 FishForce 参数调试 |

### 命名规范

- 文件名用 `snake_case`，描述功能：`query_xxx.cs`、`setup_xxx.cs`、`check_xxx.cs`
- 前缀表示类型：`query_` = 只读查询，`setup_` = 场景配置，`check_` = 状态检查

---

## 🧹🧹 重清理 (Deep Cleanup)

仅在任务**全部完成并确认**后执行。需要用户二次确认。

### 清理清单

| 类别 | 目标 | 操作 | 自动化 |
|------|------|------|--------|
| **临时代码** | 不会被复用的实验性功能 | 删除标注为临时的类/方法 | ⚠️ 分析后列出，人工确认 |
| **冗余代码** | 重复逻辑、未使用的 using、死代码 | 扫描并移除 | ⚠️ 列出后人工确认 |
| **冗余文件** | 功能文件夹中的 `Plan.md`、`TODO.md` 等过程文档 | 删除 | ⚠️ 列出后人工确认 |
| **场景整理** | Hierarchy 中散落的测试物体 | 归类到统一空物体下 | ✅ 全自动 |
| **未使用资源** | 未被任何场景/预制体引用的资源 | 列出并询问 | ⚠️ 仅列出 |
| **空文件夹** | 无有效内容的目录 | 删除（含 .meta） | ✅ 全自动 |
| **注释代码** | 被注释掉的代码块 | 删除（保留有说明意义的注释） | ⚠️ 列出后人工确认 |
| **序列化冗余** | 无效的 SerializedField 引用 | 检查并报告 | ❌ 仅报告 |

### 重清理流程

```
重清理触发（需二次确认）
  │
  ├── [D1] 代码检查
  │     ├── 搜索未使用的 using 指令
  │     ├── 搜索被注释掉的代码块（连续 3 行以上 //）
  │     ├── 搜索标记为临时的类/方法（[Temp]、// TEMP、Experimental）
  │     └── 生成清理列表，等待人工确认
  │
  ├── [D2] 文件检查
  │     ├── 扫描功能文件夹下的 Plan.md / TODO.md / Design.md
  │     ├── 检查 Assets/ 下的空文件夹
  │     ├── 检查 .meta 孤立文件（对应源文件已删除）
  │     └── 生成清理列表，等待人工确认
  │
  ├── [D3] 场景整理
  │     ├── unityctl script execute 收集所有测试物体
  │     ├── 按功能创建父容器（如 `InstanceManager` 挂 FishTest + RainTest）
  │     ├── 将所有测试/调试物体移动到对应容器下
  │     └── 按功能分组（如 __RainTest__、__PickerTest__）
  │
  ├── [D4] 资源引用检查
  │     └── 扫描 Assets/Mine/ 下未被引用的资源
  │
  └── [D5] 最终报告
        └── "重清理完成：清理代码 X 处、删除文件 Y 个、整理物体 Z 个"
```

---

## 清理安全原则

1. **渐进式清理**：先轻后重，轻清理可在任何阶段执行，重清理仅在最终确认后
2. **先列后删**：任何涉及删除的操作，必须先列出完整清单，经人工确认后再执行
3. **禁用 `--all`**：`git stash --all` 会同时暂存并**删除**未跟踪的新文件。只能用 `git stash push -- <paths>` 指定精确路径
4. **备份范围**：轻清理只备份 `tmp/` + `Screenshots/`；重清理只备份 `tmp/` + 待修改的特定文件
5. **不碰 Assets/ 下的已完成代码**：清理只针对 `tmp/`、`Screenshots/`、场景测试物体。绝不删除 `Assets/Mine/` 下的功能代码
6. **不可逆标记**：重清理的每一步在执行前都标注 `⚠️ 不可逆`
7. **跳过 .meta 关联**：删除资源文件时同步删除对应 `.meta` 文件
8. **保留用户代码**：仅清理明确标记为临时/测试的内容

---

## 清理前自动保护

```bash
# 轻清理前：仅备份要清理的目录
git stash push -m "cleanup-light-$(date +%Y%m%d-%H%M%S)" -- tmp/ Screenshots/

# 重清理前：仅备份待修改的文件列表，不用 --all
git stash push -m "cleanup-deep-$(date +%Y%m%d-%H%M%S)" -- tmp/ Screenshots/ Assets/Scenes/
```

---

## 清理模式流程总图

```
任意阶段完成
  │
  ├── 用户主动 /clean light ──→ 轻清理流程 [L1-L5]
  │
  └── AutoMode 自动提示（编译通过 / 运行通过）
        └── "是否执行轻清理？[Y/N]"
              ├── Y → 轻清理流程 [L1-L5]
              └── N → 跳过

全部任务完成
  │
  └── 用户主动 /clean deep
        └── "重清理将删除文件和代码，不可完全撤销，确认？[Y/N]"
              ├── Y → 重清理流程 [D1-D5]
              │     └── 每个步骤列出变更 → 再次确认 → 执行
              └── N → 取消
```

---

## 引用

- 安全红线详见 [../../learnings/safety.md](../../learnings/safety.md)
- 场景整理 Roslyn 脚本见下方附录

---

## 附录：Roslyn 脚本

### 轻清理 — 扫描临时物体

```csharp
using UnityEngine;
using UnityEditor;
using System.IO;

public class Script
{
    public static object Main()
    {
        var sb = new System.Text.StringBuilder();

        // 扫描临时 GameObject
        var allObjects = Object.FindObjectsOfType<GameObject>(true);
        var tempObjs = new System.Collections.Generic.List<string>();
        foreach (var go in allObjects)
        {
            if (go.name.StartsWith("_Temp") || go.name.StartsWith("_Debug") ||
                go.name.StartsWith("Temp_") || go.name.StartsWith("Test_"))
            {
                tempObjs.Add($"{go.name} (root: {go.transform.root.name})");
            }
        }

        sb.AppendLine($"=== 临时物体: {tempObjs.Count} 个 ===");
        foreach (var name in tempObjs)
            sb.AppendLine($"  - {name}");

        return sb.ToString();
    }
}
```

### 重清理 — 场景整理

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class Script
{
    // 匹配规则：名称中包含这些关键词的物体会被归类
    static string[] TestPatterns = { "Test", "Temp", "Debug", "Debug_", "_Test", "_Temp", "_Debug" };

    public static object Main()
    {
        var sb = new System.Text.StringBuilder();
        var testObjects = new System.Collections.Generic.List<GameObject>();

        // 扫描根物体及子物体
        var allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var root in allRoots)
        {
            CollectTestObjects(root, testObjects);
        }

        if (testObjects.Count == 0)
        {
            return "未发现测试物体，场景无需整理。";
        }

        // 创建容器
        var container = GameObject.Find("__TestObjects__");
        if (container == null)
        {
            container = new GameObject("__TestObjects__");
            Undo.RegisterCreatedObjectUndo(container, "Create Test Container");
        }

        // 按功能分组
        var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<GameObject>>();
        foreach (var go in testObjects)
        {
            var group = CategorizeObject(go.name);
            if (!groups.ContainsKey(group))
                groups[group] = new System.Collections.Generic.List<GameObject>();
            groups[group].Add(go);
        }

        int movedCount = 0;
        foreach (var kvp in groups)
        {
            var groupGo = new GameObject($"__{kvp.Key}__");
            groupGo.transform.SetParent(container.transform);
            Undo.RegisterCreatedObjectUndo(groupGo, "Create Group");

            foreach (var go in kvp.Value)
            {
                Undo.SetTransformParent(go.transform, groupGo.transform, "Move Test Object");
                movedCount++;
            }
            sb.AppendLine($"  [{kvp.Key}] {kvp.Value.Count} 个物体");
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        sb.Insert(0, $"场景整理完成：归类 {movedCount} 个测试物体到 __TestObjects__ 下\n分组详情:\n");
        return sb.ToString();
    }

    static void CollectTestObjects(GameObject go, System.Collections.Generic.List<GameObject> list)
    {
        foreach (Transform child in go.transform)
        {
            CollectTestObjects(child.gameObject, list);
        }

        foreach (var pattern in TestPatterns)
        {
            if (go.name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                list.Add(go);
                break;
            }
        }
    }

    static string CategorizeObject(string name)
    {
        var lower = name.ToLower();
        if (lower.Contains("rain")) return "RainTest";
        if (lower.Contains("picker")) return "PickerTest";
        if (lower.Contains("boid")) return "BoidsTest";
        if (lower.Contains("noise")) return "NoiseTest";
        if (lower.Contains("ik") || lower.Contains("ragdoll")) return "IKTest";
        if (lower.Contains("shader") || lower.Contains("card")) return "ShaderTest";
        return "OtherTests";
    }
}
```
