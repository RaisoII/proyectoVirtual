using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class GearLeverManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 rotation = this.transform.localRotation.eulerAngles;
        if(rotation.x > 90)
        {
            float rotationnegative = rotation.x - 360;
            Debug.Log("GearLever" + rotationnegative);
        }else
        {
            Debug.Log("GearLever" + rotation.x);
        }
    }
}

