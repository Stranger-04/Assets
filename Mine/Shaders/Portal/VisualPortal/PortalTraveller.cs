using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace PortalProject
{

    public class PortalTraveller : MonoBehaviour
    {
        public Vector3 previousOffsetFromPortal { get; set; }

        public GameObject graphicObj;
        public GameObject cloneObj;
        public Material[] originalMaterials { get; set; }
        public Material[] cloneMaterials { get; set; }


        protected Material[] GetMaterials(Transform t)
        {
            List<Material> list = new List<Material>();
            Renderer[] rs = t.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                for (int j = 0; j < rs[i].materials.Length; j++)
                {
                    list.Add(rs[i].materials[j]);
                }
            }
            return list.ToArray();
        }
        public virtual void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
        {
            transform.position = pos;
            transform.rotation = rot;
        }
        public virtual void EnterPortalThreshold()
        {
            if (cloneObj == null)
            {
                cloneObj = Instantiate(graphicObj);
                cloneObj.transform.parent = graphicObj.transform.parent;
                cloneObj.transform.localScale = graphicObj.transform.localScale;
                cloneMaterials = GetMaterials(cloneObj.transform);
                originalMaterials = GetMaterials(graphicObj.transform);
            }
            else
            {
                cloneObj.SetActive(true);
            }
        }
        public virtual void ExitPortalThreshold()
        {
            cloneObj.SetActive(false);
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                originalMaterials[i].SetVector("sliceCenter", Vector3.zero);
                originalMaterials[i].SetVector("sliceNormal", Vector3.zero);
            }
        }
    }
}