using UnityEngine;
using Mine.CamController;

public class Script
{
    public static object Main()
    {
        var cube = GameObject.Find("RigidbodyMover_Cube");
        var rig  = GameObject.Find("CameraRig");
        if (cube == null || rig == null) return "cube or rig not found";

        // 1. CameraRig 作为锚点固定在 cube 上方
        rig.transform.SetParent(cube.transform);
        rig.transform.localPosition = new Vector3(0f, 1.8f, 0f); // 角色眼睛高度

        // 2. 找层级: CameraRig > YawPivot > PitchPivot > Camera
        var yaw   = rig.transform.GetChild(0);
        var pitch = yaw.GetChild(0);
        var cam   = pitch.GetChild(0);

        // 3. Camera 纯 Z 距离，无 Y 分量
        cam.localPosition = new Vector3(0f, 0f, -6f);

        // 4. 同步脚本中的 _zoomDistance
        var controller = rig.GetComponent<CameraRigController>();
        var t = typeof(CameraRigController);
        t.GetField("_zoomDistance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(controller, 6f);

        return new
        {
            rigLocalPos        = rig.transform.localPosition.ToString("F2"),
            cameraLocalPos     = cam.localPosition.ToString("F2"),
            zoomDistance       = 6f,
            structure = "Cube → CameraRig(0,1.8,0) → Yaw → Pitch → Camera(0,0,-6)"
        };
    }
}
