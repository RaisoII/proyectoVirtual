using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteeringWheelManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 rotation = this.transform.localRotation.eulerAngles;
        if(rotation.y > 90)
        {
            float rotationnegative = rotation.y - 360;
            //Debug.Log("SteerWheel" + rotationnegative);
        }else
        {
           //Debug.Log("SteerWheel" + rotation.y);
        }
    }
}
