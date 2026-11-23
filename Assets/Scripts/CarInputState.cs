using System;

/// <summary>
/// Snapshot inmutable de las teclas de dirección del auto.
/// Es el "contrato" entre la lectura de controles y el envío de mensajes.
/// </summary>
public readonly struct CarInputState : IEquatable<CarInputState>
{
    public readonly bool Forward;
    public readonly bool Backward;
    public readonly bool Left;
    public readonly bool Right;

    public CarInputState(bool forward, bool backward, bool left, bool right)
    {
        Forward = forward;
        Backward = backward;
        Left = left;
        Right = right;
    }

    public static readonly CarInputState Neutral = new CarInputState(false, false, false, false);

    public bool Equals(CarInputState other) =>
        Forward == other.Forward && Backward == other.Backward &&
        Left == other.Left && Right == other.Right;

    public override bool Equals(object obj) => obj is CarInputState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Forward, Backward, Left, Right);
}
