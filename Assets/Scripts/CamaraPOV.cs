using UnityEngine;
using UnityEngine.InputSystem;

public class CamaraPOV : MonoBehaviour
{
    public float sensibilidad = 0.15f;
    public float limiteVertical = 85f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed) return;

        Vector2 delta = mouse.delta.ReadValue() * sensibilidad;

        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, -limiteVertical, limiteVertical);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}