using UnityEngine;

/// <summary>
/// 搭建双层级 CameraRig：YawPivot → PitchPivot → Camera
/// 继承当前相机的世界位置与旋转
/// </summary>
public class Script
{
    public static object Main()
    {
        var cam = Camera.main;
        if (cam == null) return "错误：场景中没有 MainCamera";

        var camWorldPos = cam.transform.position;
        var camWorldRot = cam.transform.rotation;
        var camEuler    = camWorldRot.eulerAngles;

        // ── 创建 CameraRig 根节点 ──
        var rig = new GameObject("CameraRig");
        rig.transform.position = camWorldPos;

        // ── YawPivot：吸收水平旋转（世界 Y） ──
        var yawPivot = new GameObject("YawPivot");
        yawPivot.transform.SetParent(rig.transform, false);
        yawPivot.transform.localPosition = Vector3.zero;
        yawPivot.transform.rotation = Quaternion.Euler(0f, camEuler.y, 0f);

        // ── PitchPivot：吸收垂直旋转（局部 X） ──
        var pitchPivot = new GameObject("PitchPivot");
        pitchPivot.transform.SetParent(yawPivot.transform, false);
        pitchPivot.transform.localPosition = Vector3.zero;

        // 归一化 X 角度到 [-180, 180] 后取 pitch
        float pitch = camEuler.x;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        pitchPivot.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // ── 相机挂载，保留 Z 旋转（Roll） ──
        float roll = camEuler.z;
        cam.transform.SetParent(pitchPivot.transform, false);
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.Euler(0f, 0f, roll);

        // ── 添加控制器 ──
        var ctrl = rig.AddComponent<Mine.CamController.CameraRigController>();

        return new
        {
            status = "done",
            rig        = rig.name,
            yawPivot   = yawPivot.name,
            pitchPivot = pitchPivot.name,
            camera     = cam.name,
            worldPos   = rig.transform.position.ToString(),
            yawDeg     = camEuler.y,
            pitchDeg   = pitch,
            rollDeg    = roll
        };
    }
}
