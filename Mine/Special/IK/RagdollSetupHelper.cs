using UnityEngine;
using System.Collections.Generic;

public class RagdollSetupHelper : MonoBehaviour
{
    [Header("物理设置")]
    public float defaultMass = 1f;
    public float defaultDrag = 0.05f;
    public float defaultAngularDrag = 5f;
    
    [Header("关节设置")]
    public float springStrength = 3000f;
    public float damperStrength = 50f;
    public float maxForce = 10000f;
    
    [ContextMenu("设置所有Rigidbody")]
    public void SetupAllRigidbodies()
    {
        PIDBoneFollower[] followers = GetComponentsInChildren<PIDBoneFollower>();
        
        foreach (var follower in followers)
        {
            Rigidbody rb = follower.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = defaultMass;
                rb.drag = defaultDrag;
                rb.angularDrag = defaultAngularDrag;
                rb.useGravity = false; // 通常布娃娃不需要重力，由动画驱动
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                
                Debug.Log($"设置 {follower.name} 的Rigidbody属性");
            }
        }
    }
    
    [ContextMenu("添加关节约束")]
    public void AddJointConstraints()
    {
        PIDBoneFollower[] followers = GetComponentsInChildren<PIDBoneFollower>();
        
        foreach (var follower in followers)
        {
            // 如果没有ConfigurableJoint，添加一个
            ConfigurableJoint joint = follower.GetComponent<ConfigurableJoint>();
            if (joint == null)
            {
                joint = follower.gameObject.AddComponent<ConfigurableJoint>();
            }
            
            // 设置关节属性
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;
            
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;
            
            // 设置弹簧和阻尼
            JointDrive drive = new JointDrive();
            drive.positionSpring = springStrength;
            drive.positionDamper = damperStrength;
            drive.maximumForce = maxForce;
            
            joint.xDrive = drive;
            joint.yDrive = drive;
            joint.zDrive = drive;
            joint.angularXDrive = drive;
            joint.angularYZDrive = drive;
            
            Debug.Log($"设置 {follower.name} 的关节约束");
        }
    }
    
    [ContextMenu("禁用所有碰撞")]
    public void DisableCollisions()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        
        for (int i = 0; i < colliders.Length; i++)
        {
            for (int j = i + 1; j < colliders.Length; j++)
            {
                Physics.IgnoreCollision(colliders[i], colliders[j]);
            }
        }
        
        Debug.Log($"禁用了 {colliders.Length} 个碰撞体之间的碰撞");
    }
}
