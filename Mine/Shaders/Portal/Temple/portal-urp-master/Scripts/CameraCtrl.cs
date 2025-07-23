using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//第一人称相机
public class CameraCtrl : MonoBehaviour
{
    public float horizontalSpeedScale = 1;
    public float verticalSpeedScale = 1;

    public Transform horizontalT;
    public Transform verticalT;

    private void Update()
    {

        float y = Input.GetAxis("Mouse Y");

        verticalT.Rotate(-y*verticalSpeedScale,0, 0);
        

    }

}
