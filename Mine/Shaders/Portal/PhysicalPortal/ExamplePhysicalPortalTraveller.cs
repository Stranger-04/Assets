using UnityEngine;

namespace PortalProject
{
    /// <summary>
    /// 示例：扩展的物理传送门旅行者，展示如何添加自定义传送效果
    /// </summary>
    public class ExamplePhysicalPortalTraveller : PhysicalPortalTraveller
    {
        [Header("特效配置")]
        public ParticleSystem teleportEffect;
        public AudioClip teleportSound;
        public float effectDuration = 1f;
        
        private AudioSource audioSource;
        
        private void Start()
        {
            // 获取或创建音频源
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && teleportSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
        
        protected override void OnBeforeTeleport()
        {
            base.OnBeforeTeleport();
            
            // 播放传送特效
            if (teleportEffect != null)
            {
                teleportEffect.Play();
            }
            
            // 播放传送音效
            if (audioSource != null && teleportSound != null)
            {
                audioSource.PlayOneShot(teleportSound);
            }
            
            if (enableDebugLog)
                Debug.Log($"{gameObject.name} 开始传送特效");
        }
        
        protected override void OnAfterTeleport()
        {
            base.OnAfterTeleport();
            
            // 传送后可以添加额外的特效
            if (teleportEffect != null)
            {
                // 延迟停止特效
                Invoke(nameof(StopTeleportEffect), effectDuration);
            }
            
            if (enableDebugLog)
                Debug.Log($"{gameObject.name} 传送完成，特效将在 {effectDuration} 秒后停止");
        }
        
        private void StopTeleportEffect()
        {
            if (teleportEffect != null)
            {
                teleportEffect.Stop();
            }
        }
    }
}
