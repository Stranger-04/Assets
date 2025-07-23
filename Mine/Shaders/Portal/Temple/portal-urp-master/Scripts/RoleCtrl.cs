using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

namespace PortalProject
{
    public class RoleCtrl : PortalTraveller
    {
        public float jumpSpeed = 5;
        public float speed = 5;

        public Rigidbody rb;

        public Vector3 currSpeed;
        

        private void Awake()
        {
             rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            float mx = Input.GetAxis("Mouse X");
            transform.Rotate(0, mx, 0);


            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");

            Vector3 dir = new Vector3(x, 0 ,z);
            if(dir!=Vector3.zero)
            {
                dir.Normalize();

                Vector3 worldDir = Camera.main.transform.TransformDirection(dir);
                worldDir.y = 0;
                worldDir.Normalize();

                currSpeed = worldDir * speed;
            }
            else
                currSpeed = Vector3.zero;

            if(Input.GetKeyDown(KeyCode.Space))
            {
                currSpeed.y = jumpSpeed;
            }
            else
            {
                currSpeed.y = rb.velocity.y;
            }

            rb.velocity = currSpeed;




        }

        public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
        {
            //cine.transform.position = pos + Camera.main.transform.localPosition - transform.position;
            //cine.ForceCameraPosition(pos + Camera.main.transform.localPosition-transform.position, rot );
            
            base.Teleport(fromPortal, toPortal, pos, rot);

            

            //Camera.main.transform.position = pos + Camera.main.transform.localPosition;
            //Camera.main.transform.rotation = rot * Camera.main.transform.localRotation;

        }


    }

    



}