// ═══════════════════════════════════════════════════════════════
//  Unity 6 URP VolumeComponent 模板
//
//  使用方式：
//    1. 复制此类，改名为 YourEffect
//    2. 替换所有 ⚠️ 标记处
//    3. 在 Volume Profile 中添加此 component 即可控制参数
// ═══════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
[VolumeComponentMenuForRenderPipeline(
    "Post-processing/YourEffect",             // ⚠️ 替换菜单路径
    typeof(UniversalRenderPipeline)
)]
public class YourEffectTemplate : VolumeComponent, IPostProcessComponent
{
    // ═══ 参数定义 ═══
    [Tooltip("Effect intensity.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    // ⚠️ 添加你的参数，可用类型见下表：
    //   ClampedFloatParameter — 带滑条的范围浮点
    //   FloatParameter — 任意浮点
    //   ClampedIntParameter — 带滑条的范围整数
    //   IntParameter — 任意整数
    //   ColorParameter — 颜色拾取器
    //   BoolParameter — 开关
    //   Vector2Parameter — 二维向量
    //   Texture2DParameter — 纹理引用
    //   NoInterpFloatParameter — 不插值的浮点

    // ═══ 兼容性 ═══
    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => false;
}
