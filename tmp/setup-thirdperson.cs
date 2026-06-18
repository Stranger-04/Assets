using UnityEngine;
using Mine.CamController;

public class Script
{
    public static object Main()
    {
        var cube = GameObject.Find("RigidbodyMover_Cube");
        var oldRig = GameObject.Find("CameraRig");
        if (cube == null || oldRig == null) return "cube or CameraRig not found";

        // 1. 救出相机，先挪到根层级避免被 Destroy 连带
        var oldCam = oldRig.GetComponentInChildren<Camera>();
        if (oldCam == null) return "no camera found";
        oldCam.transform.SetParent(null);

        // 2. 销毁旧 CameraRig
        Object.DestroyImmediate(oldRig);

        // 3. 新层级：Cube → FocusPoint → Yaw → Pitch → Cam
        var focus = new GameObject("FocusPoint");
        focus.transform.SetParent(cube.transform);
        focus.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var yaw = new GameObject("YawPivot");
        yaw.transform.SetParent(focus.transform);
        yaw.transform.localPosition = Vector3.zero;

        var pitch = new GameObject("PitchPivot");
        pitch.transform.SetParent(yaw.transform);
        pitch.transform.localPosition = Vector3.zero;

        // 4. 相机归位
        oldCam.transform.SetParent(pitch.transform);
        oldCam.transform.localPosition = new Vector3(0f, 0f, -6f);
        oldCam.transform.localRotation = Quaternion.identity;

        // 5. 挂脚本
        var tpc = focus.AddComponent<ThirdPersonCamera>();

        return new
        {
            structure = "Cube → FocusPoint(0,1.5,0) → Yaw → Pitch → Cam(0,0,-6)",
            scriptOn  = "FocusPoint",
            tip       = "WASD=Cube移动，鼠标=旋转，滚轮=缩放"
        };
    }
}
