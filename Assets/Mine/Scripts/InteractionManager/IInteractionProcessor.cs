using UnityEngine;

namespace Mine.Interaction
{
    /// <summary>
    /// 通用交互处理器接口 — 接收正交相机渲染的原始交互数据，
    /// 通过 Compute Shader 处理后输出到 RT_InteractionResult。
    ///
    /// 实现者必须是 MonoBehaviour，挂载在 UniversalInteractionManager 同一 GameObject 上。
    /// UniversalInteractionManager 通过 GetComponent&lt;IUniversalInteractionProcessor&gt;()
    /// 自动发现并调用。
    /// </summary>
    ///
    /// <remarks>
    /// 实现示例：
    /// - WaterInteractionProcessor (波纹扩散 + 时间衰减)
    /// - 未来：GrassInteractionProcessor (直通)、SnowInteractionProcessor (累积) 等
    ///
    /// 使用方式：
    /// 1. 编写继承 MonoBehaviour 并实现此接口的类
    /// 2. 将脚本挂载到 UniversalInteractionManager 所在 GameObject
    /// 3. Inspector 中拖入 ComputeShader
    /// 4. UniversalInteractionManager 在 OnEnable 时自动发现并调用 Initialize
    /// </remarks>
    public interface IUniversalInteractionProcessor
    {
        /// <summary>初始化：绑定 RT 到 Compute Shader，查找 kernel。</summary>
        void Initialize(int resolution, RenderTexture sourceRT, RenderTexture resultRT);

        /// <summary>每帧处理：设置参数并 Dispatch Compute Shader。</summary>
        void Process(float deltaTime);

        /// <summary>释放 Compute Shader 持有的 RT 引用。</summary>
        void Release();
    }
}
