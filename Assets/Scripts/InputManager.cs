using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public Transform gearLever, steeringWheel;
    public Esp32UnityController esp32Controller;

    // Update is called once per frame
    void Update()
    {
        float steeringAngle = steeringWheel.eulerAngles.y;
        steeringAngle = signalizeAngle(steeringAngle, 90);
        float throttleValue = gearLever.eulerAngles.x;
        throttleValue = signalizeAngle(throttleValue, 46);

        float steeringInput = Mathf.Clamp(steeringAngle / 90f, -1f, 1f);
        float throttleInput = Mathf.Clamp(throttleValue / 45f, -1f, 1f);
    
        // Send the input values to the ESP32
        //Debug.Log("DESPUÉS:  Steering Input: " + steeringInput + ", Throttle Input: " + throttleInput);
        esp32Controller.SetControls(steeringInput, throttleInput);
    }

    float signalizeAngle(float angle, float maxAngle)
    {
        if (angle > maxAngle)
        {
            angle -= 360f;
        }
        return angle;
    }
}

