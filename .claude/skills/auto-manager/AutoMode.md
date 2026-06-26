# AutoMode — 自动化流程文档

> Claude Code + unityctl 自动化操作 Unity Editor 的标准流程。默认开启，遇到关键节点自动暂停等待人工介入。

---

## 知识库预加载（AutoMode 激活时自动执行）

AutoMode 激活后，**在分析用户指令之前**，自动读取 `Assets/MarkDowns/` 文件夹下的所有 `.md` 文件，作为项目知识库注入上下文。

### 预加载逻辑

```
AutoMode 激活
  │
  ├── [0.1] 扫描 Assets/MarkDowns/*.md
  │     └── ls Assets/MarkDowns/*.md
  │
  ├── [0.2] 按优先级读取
  │     ├── 高优先级（必读）：结构规范类 → ScriptStructure.md, ShaderStructure.md
  │     ├── 中优先级（按需）：模板类 → ScriptDocTemplate.md, ShaderDocTemplate.md
  │     └── 低优先级（相关时读）：领域知识类 → Unity 6 全屏后处理 Shader 差异.md 等
  │
  └── [0.3] 提取关键约束
        ├── 代码风格规范（命名、注释格式、文件结构）
        ├── Shader 语法差异（Unity 6 vs 旧版本的 API 变化）
        └── 模板要求（新建文件时应遵循的文档格式）
```

### 读取策略

| 条件 | 行为 |
|------|------|
| MarkDowns 文件夹不存在 | 跳过，不报错 |
| 文件已在当前会话中读过 | 跳过（避免重复消耗 context） |
| 文件 > 500 行 | 先读前 100 行确认内容，再决定是否全读 |
| 用户指令明确涉及 Shader | 必读 ShaderStructure.md + ShaderDocTemplate.md |
| 用户指令明确涉及 C# 脚本 | 必读 ScriptStructure.md + ScriptDocTemplate.md |
| 用户指令涉及全屏后处理 | 必读 Unity 6 全屏后处理 Shader 差异.md |

### 约束应用

读取完毕后，后续所有代码生成和修改必须遵循 MarkDowns 中定义的规范：

1. **脚本结构**：遵循 `ScriptStructure.md` 中的文件组织方式
2. **Shader 结构**：遵循 `ShaderStructure.md` 中的语法和 API 用法（特别注意 Unity 6 差异）
3. **文档模板**：新增文件时按 `ScriptDocTemplate.md` / `ShaderDocTemplate.md` 格式添加头部注释

---

## 执行条件（默认开启）

AutoMode 在以下条件**全部满足**时自动激活：

| 条件 | 说明 |
|------|------|
| Unity Editor 已在运行 | `unityctl status` 返回 connected |
| bridge 已启动 | `unityctl bridge start` 已在后台运行 |
| 项目路径正确 | 当前目录下存在 `.unityctl/bridge.json` |
| 用户发出可自动化指令 | 如"创建物体测试"、"编译并运行"、"验证效果" |

### 前置检查命令

```bash
unityctl status                    # 一键检查 Editor + bridge 状态
unityctl bridge start              # 若未启动，后台拉起 bridge
```

### 默认行为

- 修改 `.cs` 后 → **自动** `asset refresh` 编译
- 编译报错 → **自动** 读取错误、定位文件、修复、重新编译
- 编译通过 → **自动** 执行配置脚本、进入 Play Mode
- 运行报错 → **自动** 读取日志、分析原因、修复、重新进入 Play Mode
- 零错误 → **自动** 截图、退出、报告结果

---

## 自动化流程

```
用户指令
  │
  ├── [1] 定位 & 分析 ─── 读取相关文件，理解现有结构
  │
  ├── [2] 方案设计 ─── 输出计划（新建/修改哪些文件）
  │
  ├── [3] 代码生成 ─── Write/Edit 生成全部文件
  │
  ├── [4] 编译验证 ─── unityctl asset refresh
  │     ├── 编译失败 → 读错误 → 修复 → 回到 [4]
  │     └── 编译通过 → 继续
  │
  ├── [5] 场景配置 ─── unityctl script execute 自动创建物体、挂载脚本、配参
  │     ├── 脚本执行失败 → 读错误 → 修复 → 回到 [4]
  │     └── 成功 → 继续
  │
  ├── [6] 运行时验证 ─── unityctl play enter → sleep Ns → unityctl logs
  │     ├── 运行时错误 → 分析 → 修复 → 回到 [4]
  │     └── 零错误 → 继续
  │
  ├── [7] 截图留档 ─── unityctl screenshot capture
  │
  └── [8] 退出 Play Mode ─── unityctl play exit → 报告结果
```

### 各阶段详解

#### [4] 编译验证

```bash
unityctl asset refresh
# 输出：compilation succeeded / compilation failed
# 失败时会列出具体错误文件、行号、错误码
```

#### [5] 场景配置

通过 Roslyn 脚本直接在 Editor 中执行 C#：

```bash
unityctl script execute -f tmp/setup_xxx.cs
```

脚本模板：

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

**适用场景：**
- 创建/删除 GameObject
- 添加/移除 Component
- 修改 Inspector 中的公开字段
- 调用 public 方法（如 `InitInstances()`）
- 读取组件状态进行调试

#### [6] 运行时验证

```bash
unityctl play enter            # 进入 Play Mode
sleep 3                         # 等待模拟运行几帧
unityctl logs -n 20             # 检查日志
```

**自动诊断规则：**

| 日志模式 | 自动处理 |
|---------|---------|
| `NullReferenceException` | 定位空引用 → 检查序列化/域重载 → 修复 |
| `Thread group size must be above zero` | 检查 kernel index 是否有效 → 打印调试信息 |
| `error CSxxxx` | 编译错误 → 读文件 → 修复语法 |
| 无日志输出 | 属性缺少 `[SerializeField]` 或初始化守卫错误 |

#### [7] 截图留档

```bash
unityctl screenshot capture
# 输出：Screenshots/screenshot_YYYY-MM-DD_HH-MM-SS.png
```

---

## 退出条件

以下情况 AutoMode **自动暂停**，请求人工介入：

### 1. 需要人工观测

| 场景 | 说明 |
|------|------|
| 渲染效果验证 | 截图无法判定的画面质量（如颜色、透明度、动画流畅度） |
| 交互行为测试 | 需要鼠标点击、键盘输入的交互（如 Picker 选物体） |
| 性能评估 | 需要查看 Profiler、Frame Debugger 的数据 |

### 2. 需要人工决策

| 场景 | 说明 |
|------|------|
| 架构选择 | 多种实现方案各有优劣（如接口 vs 抽象类） |
| 参数调优 | 视觉效果参数（如雨滴速度、颜色、透明度） |
| Shader 逻辑 | 涉及 GPU 调试、渲染管线选择 |
| 破坏性操作 | 删除文件、修改 .meta、变更 SerializedReference |

### 3. 抵达关键节点

| 节点 | 说明 |
|------|------|
| 编译通过 | "零错误，是否进入 Play Mode 验证？" |
| Play Mode 通过 | "零运行时错误，请确认画面效果是否符合预期" |
| 测试完毕 | "全部验证通过，是否提交代码？" |
| 新功能就绪 | "框架已可用，是否需要添加更多模拟类型？" |

### 4. 兜底退出

| 条件 | 说明 |
|------|------|
| 同一错误连续 3 次 | 自动修复无效，需要人工分析 |
| 操作超时 60s | Editor 无响应或卡死 |
| bridge 断开 | `unityctl status` 返回 disconnected |

---

## 调试技巧

### 运行时检查组件状态

```bash
# 快速检查某个 GameObject 上的组件和字段值
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

# 日志（进入 Play Mode 后自动清除，所以只显示当前会话日志）
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

## 清理模式

AutoMode 提供两级清理模式，按任务阶段选择性执行。

### 清理模式对比

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
[4] 编译验证通过 → "编译通过，是否需要轻清理？"
[6] 运行时验证通过 → "运行时零错误，是否需要轻清理？"
[7] 截图留档完成 → "截图已保存，是否需要轻清理？"
```

重清理仅在任务全部完成后，由用户主动触发。

---

### 🧹 轻清理 (Light Cleanup)

适用于阶段性完成后的快速整理，只清理明确标记的临时内容。

#### 清理清单

| 类别 | 目标 | 操作 | 自动化 |
|------|------|------|--------|
| **临时脚本** | `tmp/*.cs` | 删除所有临时脚本（**保留 `tmp/.reusable/` 目录**） | ✅ 全自动 |
| **调试日志** | `Debug.Log` / `print()` | 搜索并移除无条件的调试输出 | ⚠️ 列出后人工确认 |
| **截图缓存** | `Screenshots/*.png` | 清理本次会话产生的截图 | ✅ 全自动 |
| **注释标记** | `// TODO` / `// FIXME` / `// HACK` | 列出并提醒处理 | ❌ 仅报告 |
| **临时 GameObject** | Hierarchy 中以 `_Temp` / `_Debug` 开头的物体 | 列出并询问是否删除 | ⚠️ 列出后人工确认 |
| **未引用的临时资源** | Assets 中以 `Temp_` / `_Test` 命名的文件 | 列出并询问是否删除 | ⚠️ 列出后人工确认 |

#### 轻清理流程

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

### 可复用临时脚本 (tmp/.reusable/)

`tmp/` 根目录下的 `.cs` 文件为一次性测试脚本，轻清理时全部删除。
**`tmp/.reusable/` 目录下的脚本永久保留**，轻清理和重清理都不动它。

#### 存放标准

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

#### 命名规范

- 文件名用 `snake_case`，描述功能：`query_xxx.cs`、`setup_xxx.cs`、`check_xxx.cs`
- 前缀表示类型：`query_` = 只读查询，`setup_` = 场景配置，`check_` = 状态检查

---

#### 轻清理 Roslyn 脚本

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

---

### 🧹🧹 重清理 (Deep Cleanup)

仅在任务**全部完成并确认**后执行。需要用户二次确认。

#### 清理清单

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

#### 重清理流程

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

#### 场景整理 Roslyn 脚本

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
        // 提取功能前缀用于分组
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

---

### 清理模式流程总图

```
任意阶段完成
  │
  ├── 用户主动 /clean light ──→ 轻清理流程 [L1-L5]
  │
  └── AutoMode 自动提示（编译通过 / 运行通过 / 截图完成）
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

### 清理安全原则

1. **渐进式清理**：先轻后重，轻清理可在任何阶段执行，重清理仅在最终确认后
2. **先列后删**：任何涉及删除的操作，必须先列出完整清单，经人工确认后再执行
3. **禁用 `--all`**：`git stash --all` 会同时暂存并**删除**未跟踪的新文件，包括 `Assets/` 下的已完成代码。只能用 `git stash push -- <paths>` 指定精确路径
4. **备份范围**：轻清理只备份 `tmp/` + `Screenshots/`；重清理只备份 `tmp/` + 待修改的特定文件
5. **不碰 Assets/ 下的已完成代码**：清理只针对 `tmp/`、`Screenshots/`、场景测试物体。绝不删除 `Assets/Mine/` 下的功能代码、Shader、Compute、ScriptableObject
6. **不可逆标记**：重清理的每一步在执行前都标注 `⚠️ 不可逆`
7. **跳过 .meta 关联**：删除资源文件时同步删除对应 `.meta` 文件，避免残留孤立 meta
8. **保留用户代码**：仅清理明确标记为临时/测试的内容，不动用户手写的正式代码

### 清理前自动保护

```bash
# 轻清理前：仅备份要清理的目录
git stash push -m "cleanup-light-$(date +%Y%m%d-%H%M%S)" -- tmp/ Screenshots/

# 重清理前：仅备份待修改的文件列表，不用 --all
git stash push -m "cleanup-deep-$(date +%Y%m%d-%H%M%S)" -- tmp/ Screenshots/ Assets/Scenes/
```

### 场景功能测试/整理要点

同一框架的测试物体统一挂在功能父容器下，不散落在场景根级。

```
场景层级结构：
  InstanceManager/                    ← 框架名（空物体，无组件）
  ├── FishTest                        ← FishSimulation + UniversalInstanceManager
  └── RainTest                        ← RainSimulation + UniversalInstanceManager

  __TestObjects__/                    ← 其他散落测试物体（重清理时归类）
  └── __Picker__/...
```

| 规则 | 说明 |
|------|------|
| **父容器按框架命名** | 如 `InstanceManager`、`Boids`，不是 `__TestObjects__` |
| **子物体按功能命名** | `FishTest`、`RainTest`，清晰表示正在测试哪个功能 |
| **根级不散落** | 任何含 `Test`/`Temp`/`Debug` 的物体都应在容器下 |
| **创建时即归类** | 新测试物体直接在父容器下创建，而非先创建再移动 |

---

### 经验教训

| 教训 | 说明 |
|------|------|
| **禁止 `git stash --all`** | 会删除所有未跟踪文件（包括 `Assets/Mine/Scripts/InstanceManager/` 等新创建的代码），恢复时可能因冲突失败 |
| **备份必须精确** | `git stash push -- tmp/ Screenshots/` 只备份临时文件，不动 `Assets/` |
| **清理前先 `ls` 确认** | 删除任何东西前先列出完整清单，人工确认后再执行 |
| **场景物体用脚本清理** | 不直接 `rm`，通过 `unityctl script execute` 在 Editor 中 `DestroyImmediate` |
| **重清理不删 Plan.md 等** | 功能文件夹下的 `Plan.md` 是设计文档，属于项目资产，不应删除 |
| **可复用脚本存 .reusable/** | 场景查询、管线检查等通用功能脚本放入 `tmp/.reusable/`，清理时跳过该目录 |
| **测试物体按框架下挂** | 同一框架的测试物体挂到对应父容器下（如 `InstanceManager/FishTest`、`InstanceManager/RainTest`），不散落在根级 |
