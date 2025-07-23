using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PortalProject
{
    public class CarCtrl : PortalTraveller
    {
        public float speed = 1f;
        Rigidbody rb;

        WheelCollider[] ws;
        Transform wheelModels;
        // Start is called before the first frame update
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            ws = GetComponentsInChildren<WheelCollider>();
            wheelModels = graphicObj.transform.Find("WheelModel");
        }

        // Update is called once per frame
        void Update()
        {
            Vector3 v = transform.forward * speed;
            v.y = rb.velocity.y;
            rb.velocity = v;

            for (int i = 0; i < wheelModels.childCount; i++)
            {
                wheelModels.GetChild(i).Rotate(0, -6 * ws[i].rpm * Time.deltaTime, 0);
            }
            if (cloneObj != null && cloneObj.activeSelf)
            {
                Transform cws = cloneObj.transform.Find("WheelModel");
                for (int i = 0; i < cws.childCount; i++)
                {
                    cws.GetChild(i).Rotate(0, -6 * ws[i].rpm * Time.deltaTime, 0);
                }
            }

        }
    }
}