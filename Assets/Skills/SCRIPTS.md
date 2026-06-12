# Scripts 脚本索引

通过 `unityctl script execute` 运行的 C# 脚本集合，用于在 Unity Editor 中调试和获取渲染/场景信息。

> **前置条件**: unityctl bridge 运行中 + Unity Editor 已连接

---

## 快速使用

```bash
# 基本格式
unityctl script execute Assets/Skills/scripts/<脚本名>.cs

# 查看渲染管线
unityctl script execute Assets/Skills/scripts/FrameDebugger_Report.cs
```

---

## 脚本列表

### 1. FrameDebugger_Report.cs

**用途**: 通过 Frame Debugger 获取当前帧的完整渲染管线事件列表。

**输出**:
- 每个渲染事件的名称（包含 RenderPass、Shader、关键词）
- Draw Call 数量统计

**使用场景**:
- 查看当前 URP 渲染管线结构
- 排查渲染顺序问题
- 检查某个后处理是否启用
- 统计 Draw Call

**示例**:
```bash
unityctl script execute Assets/Skills/scripts/FrameDebugger_Report.cs
```

**示例输出**:
```
=== Frame Debugger 渲染管线 (46 events) ===

  0. ... (RP 0:0) Draw Main Light Shadowmap/Shadows.DrawSRPBatcher
 10. ... (RP 1:0) Blit Color LUT/ColorGradingLUT
 11. ... (RP 2:0) DrawDepthNormalPrepass/RenderLoop.DrawSRPBatcher
 14. ... SSAO
 18. ... (RP 3:0) DrawOpaqueObjects/RenderLoop.DrawSRPBatcher
 22. ... (RP 3:0) DrawSkybox/Camera.RenderSkybox
 23. ... (RP 4:0) CopyColor/CopyColor
 24. ... (RP 5:0) DrawTransparentObjects/RenderLoop.DrawSRPBatcher
 29. ... Bloom/RG_BloomPrefilter
 ...
 45. ... (RP 6:0) RG_UberPost

总事件: 46  |  Draw Calls: 22
```

---

### 2. FrameDebugger_DiscoverAPI.cs

**用途**: 通过反射列出 `FrameDebuggerUtility` 内部类的所有方法、属性和类型。用于开发探索 Unity Editor 内部 API。

**输出**:
- `FrameDebuggerUtility` 的公开/私有静态方法签名
- 所有包含 "FrameDebugger" 的类名和所在程序集

**使用场景**:
- 开发新的 Frame Debugger 相关脚本
- 探索 Unity Editor 内部 API 变更（如 Unity 6 中 FrameDebugger API 被标记为 internal）
- 编写需要直接调用 Frame Debugger 的工具

**示例**:
```bash
unityctl script execute Assets/Skills/scripts/FrameDebugger_DiscoverAPI.cs
```

---

### 3. FrameDebugger_DebugState.cs

**用途**: 诊断 Frame Debugger 的当前状态，包括启用状态、事件计数、窗口连接状态。

**输出**:
- `FrameDebugger.enabled` 状态
- 事件计数 (`count`)
- 禁用/启用/切换操作前后的状态变化

**使用场景**:
- Frame Debugger 不工作时排查问题
- 检查是否成功捕获到帧数据

**示例**:
```bash
unityctl script execute Assets/Skills/scripts/FrameDebugger_DebugState.cs
```

---

## 环境速查

| 命令 | 说明 |
|------|------|
| `unityctl status` | 检查 bridge + Editor 连接 |
| `unityctl bridge start` | 启动 bridge（如未运行） |
| `unityctl asset refresh` | 修改脚本后重新编译 |

---

## 添加新脚本

1. 在 `Assets/Skills/scripts/` 下创建 `.cs` 文件
2. 必须包含 `public class Script` 和 `public static object Main()` 方法
3. 在此文档中登记新脚本

```cs
// 模板
using UnityEditor;
using UnityEngine;

public class Script
{
    public static object Main()
    {
        // 你的逻辑
        return "result";
    }
}
```
