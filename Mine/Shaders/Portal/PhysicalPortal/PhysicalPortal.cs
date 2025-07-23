using System.Collections.Generic;
using UnityEngine;

namespace PortalProject
{
    /// <summary>
    /// 物理传送门 - 专注于传送功能，不涉及视觉效果
    /// </summary>
    public class PhysicalPortal : MonoBehaviour
    {
        [Header("传送门配置")]
        public PhysicalPortal targetPortal;
        
        [Header("调试选项")]
        public bool enableDebugLog = false;
        
        private void OnTriggerEnter(Collider other)
        {
            // 获取传送者组件
            PhysicalPortalTraveller traveller = other.GetComponent<PhysicalPortalTraveller>();
            if (traveller == null) return;
            
            // 检查目标传送门是否存在
            if (targetPortal == null)
            {
                if (enableDebugLog)
                    Debug.LogWarning($"传送门 {gameObject.name} 没有设置目标传送门");
                return;
            }
            
            // 执行传送
            TeleportObject(traveller);
        }
        
        private void OnTriggerExit(Collider other)
        {
            PhysicalPortalTraveller traveller = other.GetComponent<PhysicalPortalTraveller>();
            if (traveller != null)
            {
                if (enableDebugLog)
                    Debug.Log($"物体 {traveller.name} 离开传送门区域");
            }
        }
        
        /// <summary>
        /// 传送物体到目标传送门
        /// </summary>
        /// <param name="traveller">要传送的物体</param>
        private void TeleportObject(PhysicalPortalTraveller traveller)
        {
            if (enableDebugLog)
                Debug.Log($"开始传送物体: {traveller.name}");
            
            // 计算相对位置和旋转
            Matrix4x4 sourceToTarget = targetPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix;
            
            // 获取传送者的当前变换
            Transform travellerTransform = traveller.transform;
            Vector3 currentPosition = travellerTransform.position;
            Quaternion currentRotation = travellerTransform.rotation;
            
            // 计算新的位置和旋转
            Vector3 newPosition = sourceToTarget.MultiplyPoint3x4(currentPosition);
            Quaternion newRotation = sourceToTarget.rotation * currentRotation;
            
            // 处理速度（如果物体有 Rigidbody）
            Rigidbody rb = traveller.GetComponent<Rigidbody>();
            Vector3 newVelocity = Vector3.zero;
            Vector3 newAngularVelocity = Vector3.zero;
            
            if (rb != null)
            {
                // 转换线性速度
                newVelocity = sourceToTarget.MultiplyVector(rb.velocity);
                // 转换角速度
                newAngularVelocity = sourceToTarget.MultiplyVector(rb.angularVelocity);
                
                if (enableDebugLog)
                    Debug.Log($"原速度: {rb.velocity}, 新速度: {newVelocity}");
            }
            
            // 执行传送
            traveller.PerformTeleport(newPosition, newRotation, newVelocity, newAngularVelocity);
            
            if (enableDebugLog)
                Debug.Log($"传送完成: {traveller.name} 从 {transform.name} 传送到 {targetPortal.transform.name}");
        }
        
        /// <summary>
        /// 检查物体是否在传送门的正面
        /// </summary>
        /// <param name="position">物体位置</param>
        /// <returns>正面返回1，背面返回-1</returns>
        public int GetSideOfPortal(Vector3 position)
        {
            Vector3 offsetFromPortal = position - transform.position;
            return System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
        }
        
        private void OnDrawGizmos()
        {
            // 绘制传送门方向
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
            
            // 绘制到目标传送门的连线
            if (targetPortal != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, targetPortal.transform.position);
            }
        }
        
        private void OnValidate()
        {
            // 确保传送门不会指向自己
            if (targetPortal == this)
            {
                Debug.LogWarning("传送门不能指向自己！");
                targetPortal = null;
            }
        }
    }
}
