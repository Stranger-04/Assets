# VolumeComponent — 后处理参数定义

> Unity 6 URP 添加自定义后处理 Feature 的标准参数定义方式。

---

## 模板

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
[VolumeComponentMenuForRenderPipeline(
    "Post-processing/MyEffect",               // ← 菜单路径
    typeof(UniversalRenderPipeline)           // ← 指定管线
)]
public class MyEffect : VolumeComponent, IPostProcessComponent
{
    // ── 参数 ──
    [Tooltip("Effect intensity.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("Blur radius in pixels.")]
    public ClampedIntParameter radius = new ClampedIntParameter(4, 1, 16);

    [Tooltip("Tint color.")]
    public ColorParameter tint = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);

    // ── 兼容性 ──
    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => false; // ← 后处理通常为 false
}
```

## 可用参数类型

| 类型 | 示例 | 用途 |
|------|------|------|
| `ClampedFloatParameter` | `new(0.5f, 0, 1)` | 范围浮点 (Volume 面板有滑条) |
| `FloatParameter` | `new(1.0f)` | 无限制浮点 |
| `ClampedIntParameter` | `new(4, 1, 16)` | 范围整数 |
| `IntParameter` | `new(4)` | 无限制整数 |
| `ColorParameter` | `new(Color.white)` | 颜色拾取器 |
| `BoolParameter` | `new(false)` | 开关 |
| `Vector2Parameter` | `new(Vector2.one)` | 二维向量 |
| `Texture2DParameter` | `new(null)` | 纹理引用 |
| `NoInterpFloatParameter` | `new(1.0f)` | 浮点（体积混合时不插值） |

## 在 Feature 中读取参数

```csharp
// MyEffect 由 VolumeManager 自动注入
MyEffect m_Settings;  // ← Feature 中的引用（通过 VolumeStack 获取）

void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
{
    var stack = VolumeManager.instance.stack;
    m_Settings = stack.GetComponent<MyEffect>();
    if (m_Settings == null || !m_Settings.IsActive()) return;

    float intensity = m_Settings.intensity.value;
    int radius = m_Settings.radius.value;
    Color tint = m_Settings.tint.value;
}
```

## 关键注意事项

- `[VolumeComponentMenuForRenderPipeline]` 是 Unity 6 的新属性，旧版用 `[VolumeComponentMenu]`
- 参数在 Volume Profile 中序列化，**修改脚本不会丢失配置**（但重命名类型会丢失）
- `IsTileCompatible() = false` 表示不支持分块渲染（绝大多数后处理都返回 false）
