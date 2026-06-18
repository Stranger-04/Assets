using UnityEngine;

public class Script
{
    public static object Main()
    {
        var rig   = GameObject.Find("CameraRig");
        var ctrl  = rig?.GetComponent<Mine.CamController.CameraRigController>();
        var cam   = Camera.main;

        return new
        {
            rigExists       = rig != null,
            controllerActive = ctrl != null && ctrl.enabled,
            cameraName      = cam?.name,
            cursorLocked    = Cursor.lockState == CursorLockMode.Locked,
            cursorVisible   = Cursor.visible,
            camWorldPos     = cam?.transform.position.ToString(),
            camForward      = cam?.transform.forward.ToString()
        };
    }
}
