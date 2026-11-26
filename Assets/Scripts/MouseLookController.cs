using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLookController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private Vector3 initialRotation = new Vector3(0f, 0f, 0f);
    [SerializeField] private bool clampVertical = true;
    [SerializeField] private float minVerticalAngle = -85f;
    [SerializeField] private float maxVerticalAngle = 85f;

    private float yaw;
    private float pitch;
    private Quaternion baseRotation;

    private void Start()
    {
        baseRotation = Quaternion.Euler(initialRotation);
        transform.localRotation = baseRotation;
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
        {
            return;
        }

        Vector2 delta = Mouse.current.delta.ReadValue() * sensitivity;

        // Horizontal (izquierda/derecha) -> eje Y del mundo
        yaw += delta.x;

        // Vertical (arriba/abajo)
        pitch -= delta.y;
        if (clampVertical)
        {
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }

        // Aplicamos yaw en mundo y pitch en eje local de la cámara
        Quaternion yawRot = Quaternion.AngleAxis(yaw, Vector3.up);
        Quaternion pitchRot = Quaternion.AngleAxis(pitch, Vector3.right);

        transform.localRotation = yawRot * baseRotation * pitchRot;
    }
}