using System;

/// <summary>
/// Protocolo binario de ultra-baja latencia para streaming de video sobre UDP.
/// Estructura idéntica y compatible byte a byte con UdpPacketHeader en C++ (ESP32).
/// </summary>
public static class UdpVideoProtocol
{
    // Magic: 'V' (0x56) y 'R' (0x52) = 0x5256 en little-endian
    public const ushort PacketMagic = 0x5256;
    public const int HeaderSize = 24;
    public const int TargetDatagramSize = 1200;
    public const int MaxPayloadSize = TargetDatagramSize - HeaderSize; // 1176 bytes
    public const ushort DefaultPort = 8282;

    public struct PacketHeader
    {
        public ushort Magic;              // 2 bytes
        public uint FrameId;              // 4 bytes
        public ushort FragmentIndex;      // 2 bytes
        public ushort TotalFragments;     // 2 bytes
        public uint TotalFrameSize;       // 4 bytes
        public uint CaptureTimestampMs;   // 4 bytes
        public uint FragmentOffset;       // 4 bytes
        public ushort PayloadSize;        // 2 bytes

        public override string ToString()
        {
            return $"[Header Frame={FrameId} Frag={FragmentIndex}/{TotalFragments} Size={PayloadSize}/{TotalFrameSize} Offset={FragmentOffset} Time={CaptureTimestampMs}ms]";
        }
    }

    /// <summary>
    /// Deserializa y valida la cabecera binaria del datagrama recibido.
    /// Operación sin asignaciones dinámicas en memoria (Zero-Allocation).
    /// </summary>
    public static bool TryParseHeader(byte[] buffer, int length, out PacketHeader header)
    {
        header = default;

        if (buffer == null || length < HeaderSize)
        {
            return false;
        }

        // Lectura de campos en Little-Endian (formato nativo de ESP32 y Android ARM64)
        header.Magic = (ushort)(buffer[0] | (buffer[1] << 8));
        if (header.Magic != PacketMagic)
        {
            return false;
        }

        header.FrameId = (uint)(buffer[2] | (buffer[3] << 8) | (buffer[4] << 16) | (buffer[5] << 24));
        header.FragmentIndex = (ushort)(buffer[6] | (buffer[7] << 8));
        header.TotalFragments = (ushort)(buffer[8] | (buffer[9] << 8));
        header.TotalFrameSize = (uint)(buffer[10] | (buffer[11] << 8) | (buffer[12] << 16) | (buffer[13] << 24));
        header.CaptureTimestampMs = (uint)(buffer[14] | (buffer[15] << 8) | (buffer[16] << 16) | (buffer[17] << 24));
        header.FragmentOffset = (uint)(buffer[18] | (buffer[19] << 8) | (buffer[20] << 16) | (buffer[21] << 24));
        header.PayloadSize = (ushort)(buffer[22] | (buffer[23] << 8));

        // Validaciones estrictas de integridad y límites
        if (header.TotalFragments == 0 || header.FragmentIndex >= header.TotalFragments)
        {
            return false;
        }

        if (header.TotalFrameSize == 0 || header.PayloadSize == 0)
        {
            return false;
        }

        if (header.FragmentOffset + header.PayloadSize > header.TotalFrameSize)
        {
            return false;
        }

        if (length < HeaderSize + header.PayloadSize)
        {
            return false;
        }

        return true;
    }
}

