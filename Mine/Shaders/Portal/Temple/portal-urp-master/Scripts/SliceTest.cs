using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliceTest : MonoBehaviour
{
    public Transform target;

    Material[] ms;
    // Start is called before the first frame update
    void Start()
    {
        ms = GetMaterials(target);
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < ms.Length; i++)
        {
            ms[i].SetVector("sliceCenter", transform.position);
            ms[i].SetVector("sliceNormal", transform.forward);

            //Debug.Log(ms[i].GetFloat("_SliceSide"));
        }
        
    }
    Material[] GetMaterials(Transform t)
    {
        List<Material> list = new List<Material>();
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++)
        {
            for(int j = 0; j < rs[i].materials.Length; j++)
            {
                list.Add(rs[i].materials[j]);
            }
        }
        return list.ToArray();
    }
}
