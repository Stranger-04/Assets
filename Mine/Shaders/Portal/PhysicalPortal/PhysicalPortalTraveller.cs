using UnityEngine;

namespace PortalProject
{
    /// <summary>
    /// 物理传送门旅行者 - 可以被传送门传送的物体
    /// </summary>
    public class PhysicalPortalTraveller : MonoBehaviour
    {
        [Header("传送配置")]
        public bool canBeTeleported = true;
        public bool preserveVelocity = true;
        
        [Header("调试选项")]
        public bool enableDebugLog = false;
        
        // 组件缓存
        private Rigidbody rb;
        private Collider col;
        
        // 传送状态
        private bool isTeleporting = false;
        
        // 传送前的状态记录
        private Vector3 preTeleportPosition;
        private Quaternion preTeleportRotation;
        private Vector3 preTeleportVelocity;
        private Vector3 preTeleportAngularVelocity;
        
        private void Awake()
        {
            // 缓存组件
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            
            if (col == null)
            {
                Debug.LogError($"PhysicalPortalTraveller on {gameObject.name} requires a Collider component!");
            }
        }
        
        /// <summary>
        /// 检查是否可以被传送
        /// </summary>
        /// <returns>可以传送返回true</returns>
        public bool CanBeTeleported()
        {
            if (!canBeTeleported) return false;
            if (isTeleporting) return false;
            
            return true;
        }
        
        /// <summary>
        /// 执行传送
        /// </summary>
        /// <param name="newPosition">新位置</param>
        /// <param name="newRotation">新旋转</param>
        /// <param name="newVelocity">新速度</param>
        /// <param name="newAngularVelocity">新角速度</param>
        public void PerformTeleport(Vector3 newPosition, Quaternion newRotation, Vector3 newVelocity, Vector3 newAngularVelocity)
        {
            if (!CanBeTeleported())
            {
                if (enableDebugLog)
                    Debug.Log($"{gameObject.name} 当前不能被传送");
                return;
            }
            
            // 记录传送前状态
            preTeleportPosition = transform.position;
            preTeleportRotation = transform.rotation;
            if (rb != null)
            {
                preTeleportVelocity = rb.velocity;
                preTeleportAngularVelocity = rb.angularVelocity;
            }
            
            // 开始传送
            isTeleporting = true;
            
            if (enableDebugLog)
            {
                Debug.Log($"传送 {gameObject.name}:");
                Debug.Log($"  位置: {preTeleportPosition} -> {newPosition}");
                Debug.Log($"  旋转: {preTeleportRotation} -> {newRotation}");
                if (rb != null)
                {
                    Debug.Log($"  速度: {preTeleportVelocity} -> {newVelocity}");
                    Debug.Log($"  角速度: {preTeleportAngularVelocity} -> {newAngularVelocity}");
                }
            }
            
            // 调用传送前事件
            OnBeforeTeleport();
            
            // 执行位置和旋转变换
            transform.position = newPosition;
            transform.rotation = newRotation;
            
            // 如果有 Rigidbody，处理速度
            if (rb != null && preserveVelocity)
            {
                rb.velocity = newVelocity;
                rb.angularVelocity = newAngularVelocity;
            }
            
            // 调用传送后事件
            OnAfterTeleport();
            
            // 延迟结束传送状态，避免在同一帧内多次传送
            Invoke(nameof(EndTeleport), 0.02f);
        }
        
        /// <summary>
        /// 传送前调用的虚方法，子类可以重写
        /// </summary>
        protected virtual void OnBeforeTeleport()
        {
            // 可以在这里添加传送前的特效、音效等
        }
        
        /// <summary>
        /// 传送后调用的虚方法，子类可以重写
        /// </summary>
        protected virtual void OnAfterTeleport()
        {
            // 可以在这里添加传送后的特效、音效等
        }
        
        /// <summary>
        /// 结束传送状态
        /// </summary>
        private void EndTeleport()
        {
            isTeleporting = false;
            
            if (enableDebugLog)
                Debug.Log($"{gameObject.name} 传送状态结束");
        }
        
        /// <summary>
        /// 获取当前速度（如果有 Rigidbody）
        /// </summary>
        /// <returns>当前速度</returns>
        public Vector3 GetVelocity()
        {
            return rb != null ? rb.velocity : Vector3.zero;
        }
        
        /// <summary>
        /// 获取当前角速度（如果有 Rigidbody）
        /// </summary>
        /// <returns>当前角速度</returns>
        public Vector3 GetAngularVelocity()
        {
            return rb != null ? rb.angularVelocity : Vector3.zero;
        }
        
        /// <summary>
        /// 设置是否可以被传送
        /// </summary>
        /// <param name="canTeleport">是否可以传送</param>
        public void SetCanBeTeleported(bool canTeleport)
        {
            canBeTeleported = canTeleport;
            
            if (enableDebugLog)
                Debug.Log($"{gameObject.name} 传送能力设置为: {canTeleport}");
        }
        
        /// <summary>
        /// 强制结束传送状态
        /// </summary>
        public void ForceEndTeleport()
        {
            CancelInvoke(nameof(EndTeleport));
            EndTeleport();
        }
        
        private void OnDestroy()
        {
            // 清理Invoke调用
            CancelInvoke();
        }
        
        // 调试信息
        private void OnGUI()
        {
            if (!enableDebugLog) return;
            
            Vector2 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
            GUI.Label(new Rect(screenPos.x - 100, Screen.height - screenPos.y, 200, 40), 
                $"Can Teleport: {CanBeTeleported()}\n" +
                $"Teleporting: {isTeleporting}");
        }
    }
}
