using UnityEngine;

/// <summary>
/// POSS (Per-Object Soft Shadow) — 挂载到需要独立软阴影的动态物体上。
/// 关闭 Renderer 自带的 Cast Shadows，由此组件接管逐物体阴影投射。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class POSSComponent : MonoBehaviour
{
    [HideInInspector] public int registrationIndex = -1;

    Renderer m_Renderer;

    public Renderer CachedRenderer
    {
        get
        {
            if (m_Renderer == null)
                m_Renderer = GetComponent<Renderer>();
            return m_Renderer;
        }
    }

    void OnEnable()
    {
        if (CachedRenderer != null)
        {
            CachedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            CachedRenderer.staticShadowCaster = true;
        }
        POSSManager.Register(this);
    }

    void OnDisable()
    {
        POSSComponent self = this;
        POSSManager.Unregister(self);
        if (CachedRenderer != null)
            CachedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    void OnValidate()
    {
        if (registrationIndex >= 0 && POSSManager.HasInstance)
        {
            POSSComponent self = this;
            POSSManager.Unregister(self);
            POSSManager.Register(this);
        }
    }
}
