using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

public class TorusHoleFilter : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [Tooltip("Radio mínimo desde el centro del toroide para permitir el agarre (en metros).")]
    [SerializeField] private float innerHoleRadius = 0.12f;

    public bool canProcess => isActiveAndEnabled;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return IsOutsideHole(interactor.transform.position);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return IsOutsideHole(interactor.transform.position);
    }

    private bool IsOutsideHole(Vector3 handWorldPos)
    {
        // Convertimos la posición de la mano al espacio local del toroide y medimos distancia en el plano del anillo (X, Z)
        Vector3 localPos = transform.InverseTransformPoint(handWorldPos);
        float radialDistance = new Vector2(localPos.x, localPos.z).magnitude;
        return radialDistance >= innerHoleRadius;
    }
}