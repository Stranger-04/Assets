using UnityEngine;
using Mine.CamController;

public class Script
{
    public static object Main()
    {
        var cube = GameObject.Find("RigidbodyMover_Cube");
        var rig  = GameObject.Find("CameraRig");
        if (cube == null || rig == null) return "cube or rig not found";

        // 1. CameraRig 独立（不再作为 cube 子物体）
        rig.transform.SetParent(null);

        // 2. CameraRig 放在 cube 上方偏后的位置
        rig.transform.position = cube.transform.position + new Vector3(0f, 1.8f, 6f);

        // 3. 层级内全部归零
        var yaw   = rig.transform.GetChild(0);
        var pitch = yaw.GetChild(0);
        var cam   = pitch.GetChild(0);
        yaw.localPosition   = Vector3.zero;
        pitch.localPosition = Vector3.zero;
        cam.localPosition   = Vector3.zero;

        // 4. 设置 followTarget 和焦距
        var controller = rig.GetComponent<CameraRigController>();
        var t = typeof(CameraRigController);

        t.GetField("_followTarget",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(controller, cube.transform);

        // zoomDistance 用当前实际距离
        float dist = Vector3.Distance(rig.transform.position, cube.transform.position);
        t.GetField("_zoomDistance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(controller, dist);

        return new
        {
            rigWorldPos    = rig.transform.position.ToString("F2"),
            cubeWorldPos   = cube.transform.position.ToString("F2"),
            distance       = dist.ToString("F2"),
            focalDirection = "从 Rig 指向 Cube",
            structure      = "Rig(独立)→Yaw(0)→Pitch(0)→Cam(0)，Rig=Cube-focalDir*zoom",
            zoomPrinciple  = "滚轮改 _zoomDistance，HandleFollow 平滑吸附 Rig 至 Cube-focalDir*dist"
        };
    }
}
