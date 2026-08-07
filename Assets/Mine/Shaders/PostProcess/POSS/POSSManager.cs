using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// POSS 全局管理器（场景单例）— 维护已注册的逐物体阴影投射器列表。
/// 由 POSSFeature 每帧查询可见物体。
/// </summary>
[ExecuteAlways]
public class POSSManager : MonoBehaviour
{
    static POSSManager s_Instance;
    public static bool HasInstance => s_Instance != null;
    public static POSSManager Instance
    {
        get
        {
            if (s_Instance == null)
            {
                var go = new GameObject("POSSManager");
                go.hideFlags = HideFlags.HideAndDontSave;
                s_Instance = go.AddComponent<POSSManager>();
            }
            return s_Instance;
        }
    }

    List<POSSComponent> m_Components = new List<POSSComponent>(16);

    public IReadOnlyList<POSSComponent> Components => m_Components;

    public static void Register(POSSComponent comp)
    {
        var mgr = Instance;
        if (!mgr.m_Components.Contains(comp))
        {
            comp.registrationIndex = mgr.m_Components.Count;
            mgr.m_Components.Add(comp);
        }
    }

    public static void Unregister(POSSComponent comp)
    {
        if (s_Instance == null) return;
        var mgr = s_Instance;
        mgr.m_Components.Remove(comp);
        for (int i = 0; i < mgr.m_Components.Count; i++)
            mgr.m_Components[i].registrationIndex = i;
        comp.registrationIndex = -1;
    }

    public int Count => m_Components.Count;

    void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_Instance = this;
    }

    void OnDestroy()
    {
        if (s_Instance == this)
            s_Instance = null;
    }
}
