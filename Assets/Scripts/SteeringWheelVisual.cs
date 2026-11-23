using UnityEngine;

public class SteeringWheelVisual : MonoBehaviour
{
    [Header("Origen de controles")]
    [SerializeField] private CarInputReader inputReader;

    [Header("Eje de giro")]
    [Tooltip("Eje LOCAL del volante alrededor del cual gira.")]
    [SerializeField] private Vector3 localAxis = Vector3.forward;

    [Header("Giro")]
    [Tooltip("Ángulo máximo hacia cada lado.")]
    [SerializeField] private float maxAngle = 450f;

    [Tooltip("Invierte el sentido del giro.")]
    [SerializeField] private bool invert;

    private Quaternion initialLocalRotation;

    private void Awake()
    {
        if (inputReader == null)
            inputReader = GetComponentInParent<CarInputReader>();

        if (inputReader == null)
            Debug.LogError("[SteeringWheelVisual] Falta asignar un CarInputReader.", this);

        initialLocalRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        if (inputReader == null)
            return;

        float angle = inputReader.steering * maxAngle;

        if (invert)
            angle *= -1f;

        Vector3 axis = localAxis.normalized;

        if (axis.sqrMagnitude < 0.001f)
            axis = Vector3.forward;

        transform.localRotation =
            initialLocalRotation *
            Quaternion.AngleAxis(angle, axis);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 axis = localAxis.normalized;

        Gizmos.color = Color.magenta;

        Vector3 worldAxis = transform.TransformDirection(axis);

        Gizmos.DrawLine(
            transform.position - worldAxis * 0.3f,
            transform.position + worldAxis * 0.3f
        );

        Gizmos.DrawSphere(
            transform.position + worldAxis * 0.3f,
            0.01f
        );
    }
}