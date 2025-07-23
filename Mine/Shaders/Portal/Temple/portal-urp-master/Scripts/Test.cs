using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public Transform obj;
    public Vector3 pos;
    public Vector3 rot;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var m = obj.localToWorldMatrix;
        pos = m.GetColumn(3);
        rot = m.rotation.eulerAngles;
    }
}
