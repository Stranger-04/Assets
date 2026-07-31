using UnityEngine;

namespace Mine.Interaction
{
    /// <summary>
    /// 通用交互处理器接口 — 接收正交相机渲染的原始交互数据 (originRT)，
    /// 独立管理自己的输出 RT，通过 Compute Shader 处理后暴露给全局 Shader。
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
    /// </remarks>
    public interface IUniversalInteractionProcessor
    {
        /// <summary>初始化：创建输出 RT，绑定纹理到 Compute Shader，查找 kernel。</summary>
        /// <param name="resolution">RT 分辨率（边长像素数）</param>
        /// <param name="sourceRT">Manager 管理的共享输入 RT (originRT)</param>
        void Initialize(int resolution, RenderTexture sourceRT);

        /// <summary>每帧处理：设置参数并 Dispatch Compute Shader。</summary>
        /// <param name="deltaTime">帧间隔时间</param>
        /// <param name="worldDelta">物体世界空间位移 (XZ)，用于 RT 数据重投影</param>
        void Process(float deltaTime, Vector2 worldDelta);

        /// <summary>绑定输出 RT 为全局 Shader 属性，供 debug/渲染 shader 采样。</summary>
        void BindGlobalTextures();

        /// <summary>释放 Compute Shader 持有的引用和自管理的 RT。</summary>
        void Release();
    }
}
