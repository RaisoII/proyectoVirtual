using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Única responsabilidad: leer el teclado.
/// - Estado digital (WASD) + evento StateChanged -> lo usa el envío a la ESP32.
/// - Ejes analógicos suavizados (steering / throttle) -> los usa lo visual (volante, etc.).
/// </summary>
public class CarInputReader : MonoBehaviour
{
    [Header("Diagnóstico de Entrada (digital)")]
    public bool forward;
    public bool backward;
    public bool left;
    public bool right;

    [Header("Ejes analógicos (suavizados)")]
    [Tooltip("-1 = izquierda total, 0 = centro, 1 = derecha total")]
    [Range(-1f, 1f)] public float steering;
    [Tooltip("-1 = reversa total, 0 = neutro, 1 = acelerador total")]
    [Range(-1f, 1f)] public float throttle;

    [Header("Suavizado (mayor = más rápido)")]
    [Tooltip("Velocidad con la que el eje sube hacia el valor presionado")]
    public float pressSpeed = 5f;
    [Tooltip("Velocidad con la que el eje vuelve a 0 al soltar")]
    public float releaseSpeed = 8f;

    /// <summary>Estado digital actual de los controles.</summary>
    public CarInputState Current { get; private set; } = CarInputState.Neutral;

    /// <summary>Se dispara solo cuando cambia alguna tecla.</summary>
    public event Action<CarInputState> StateChanged;

    private void Update()
    {
        CarInputState state = Read();

        if (!state.Equals(Current))
        {
            Current = state;
            forward = state.Forward;
            backward = state.Backward;
            left = state.Left;
            right = state.Right;

            StateChanged?.Invoke(state);
        }

        // Ejes: -1..1 según las teclas, suavizados con lerp
        float steeringTarget = (state.Right ? 1f : 0f) - (state.Left ? 1f : 0f);
        float throttleTarget = (state.Forward ? 1f : 0f) - (state.Backward ? 1f : 0f);

        steering = Smooth(steering, steeringTarget);
        throttle = Smooth(throttle, throttleTarget);
    }

    private float Smooth(float current, float target)
    {
        float speed = Mathf.Approximately(target, 0f) ? releaseSpeed : pressSpeed;

        // Lerp independiente del framerate
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
        float value = Mathf.Lerp(current, target, t);

        // Evita quedarse "casi" en el objetivo para siempre
        return Mathf.Abs(value - target) < 0.001f ? target : value;
    }

    private static CarInputState Read()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return new CarInputState(
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
        }
#endif
        // Fallback al Input clásico si el Input System no está disponible
        return new CarInputState(
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
            Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
            Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));
    }
}
