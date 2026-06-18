using UnityEngine;
using Mine.CamController;

public class Script
{
    public static object Main()
    {
        var cube = GameObject.Find("RigidbodyMover_Cube");
        var rig  = GameObject.Find("CameraRig");
        if (cube == null || rig == null) return "cube or rig not found";

        // 1. CameraRig 回到 cube 位置作为锚点
        rig.transform.SetParent(cube.transform);
        rig.transform.localPosition = Vector3.zero;

        // 2. 层级: CameraRig > YawPivot > PitchPivot > Camera
        var yaw   = rig.transform.GetChild(0);
        var pitch = yaw.GetChild(0);
        var cam   = pitch.GetChild(0);

        // 3. YawPivot 承载高度和距离
        yaw.localPosition   = new Vector3(0f, 1.8f, -6f);
        pitch.localPosition = Vector3.zero;
        cam.localPosition   = Vector3.zero;

        // 4. 同步 _zoomDistance
        var controller = rig.GetComponent<CameraRigController>();
        var t = typeof(CameraRigController);
        t.GetField("_zoomDistance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(controller, 6f);

        return new
        {
            rigLocal           = rig.transform.localPosition.ToString("F1"),
            yawLocal           = yaw.localPosition.ToString("F2"),
            cameraLocal        = cam.localPosition.ToString("F1"),
            structure = "Rig(0,0,0)→Yaw(0,1.8,-6)→Pitch(0,0,0)→Cam(0,0,0)",
            zoomPrinciple      = "滚轮改 YawPivot.z，旋转绕 Rig 锚点公转"
        };
    }
}
