using System;
using System.Diagnostics;

/// <summary>
/// Reensamblador de alta eficiencia para frames JPEG fragmentados sobre datagramas UDP.
/// Implementa buffers preasignados (cero recolección de basura por frame) y
/// política de descarte inmediato de frames viejos o incompletos.
/// </summary>
public class UdpFrameAssembler
{
    private const int MaxFrameCapacity = 256 * 1024; // 256 KB (más que suficiente para QQVGA/QVGA JPEG)
    private const int MaxFragmentsPerFrame = 512;

    // Buffers preasignados de reensamblado
    private readonly byte[] assemblingBuffer = new byte[MaxFrameCapacity];
    private readonly bool[] receivedFragments = new bool[MaxFragmentsPerFrame];

    // Buffer de intercambio protegido para el hilo de renderizado
    private readonly object lockObj = new object();
    private readonly byte[] readyBuffer = new byte[MaxFrameCapacity];
    private int readyBufferSize;
    private uint readyFrameId;
    private uint readyCaptureTimestampMs;
    private float readyReassemblyTimeMs;
    private bool hasNewCompletedFrame;

    // Estado del frame actualmente en reensamblado
    private uint currentFrameId;
    private ushort expectedFragments;
    private ushort countFragmentsReceived;
    private uint expectedFrameSize;
    private uint bytesAssembled;
    private long frameAssemblyStartTicks;
    private readonly Stopwatch stopwatch = new Stopwatch();

    // Métricas diagnósticas en tiempo real
    public uint TotalPacketsReceived { get; private set; }
    public uint PacketsDroppedOld { get; private set; }
    public uint PacketsDroppedDuplicate { get; private set; }
    public uint PacketsDroppedInvalid { get; private set; }
    public uint FramesCompleted { get; private set; }
    public uint FramesIncompleteDropped { get; private set; }
    public uint FramesCorruptDropped { get; private set; }
    public float LastReassemblyTimeMs { get; private set; }

    public UdpFrameAssembler()
    {
        stopwatch.Start();
    }

    /// <summary>
    /// Procesa un datagrama UDP entrante. Diseñado para ejecutarse en el hilo de red secundario.
    /// </summary>
    public void ProcessPacket(byte[] packetBuffer, int packetLength)
    {
        TotalPacketsReceived++;

        if (!UdpVideoProtocol.TryParseHeader(packetBuffer, packetLength, out var header))
        {
            PacketsDroppedInvalid++;
            return;
        }

        if (header.TotalFrameSize > MaxFrameCapacity || header.TotalFragments > MaxFragmentsPerFrame)
        {
            PacketsDroppedInvalid++;
            return;
        }

        // Regla 1: Descartar fragmentos de frames anteriores (vencidos)
        if (header.FrameId < currentFrameId)
        {
            PacketsDroppedOld++;
            return;
        }

        // Regla 2: Si llega un frameId más nuevo que el que estábamos ensamblando
        if (header.FrameId > currentFrameId)
        {
            // Si el frame anterior quedó inconcluso, se contabiliza como frame descartado
            if (currentFrameId > 0 && countFragmentsReceived < expectedFragments)
            {
                FramesIncompleteDropped++;
            }

            // Reinicializar el ensamblador para el nuevo frame
            currentFrameId = header.FrameId;
            expectedFragments = header.TotalFragments;
            expectedFrameSize = header.TotalFrameSize;
            countFragmentsReceived = 0;
            bytesAssembled = 0;
            frameAssemblyStartTicks = stopwatch.ElapsedTicks;
            Array.Clear(receivedFragments, 0, expectedFragments);
        }

        // Regla 3: Detectar fragmentos duplicados
        if (receivedFragments[header.FragmentIndex])
        {
            PacketsDroppedDuplicate++;
            return;
        }

        // Copiar fragmento al buffer preasignado en su offset exacto
        Buffer.BlockCopy(packetBuffer, UdpVideoProtocol.HeaderSize, assemblingBuffer, (int)header.FragmentOffset, header.PayloadSize);
        receivedFragments[header.FragmentIndex] = true;
        countFragmentsReceived++;
        bytesAssembled += header.PayloadSize;

        // Regla 4: Verificar si el frame está completo
        if (countFragmentsReceived == expectedFragments && bytesAssembled == expectedFrameSize)
        {
            CompleteFrame(header);
        }
    }

    private void CompleteFrame(UdpVideoProtocol.PacketHeader header)
    {
        // Validación de marcadores JPEG estándar: SOI (0xFF 0xD8) y EOI (0xFF 0xD9)
        bool hasValidSOI = assemblingBuffer[0] == 0xFF && assemblingBuffer[1] == 0xD8;
        bool hasValidEOI = assemblingBuffer[expectedFrameSize - 2] == 0xFF && assemblingBuffer[expectedFrameSize - 1] == 0xD9;

        if (!hasValidSOI || !hasValidEOI)
        {
            FramesCorruptDropped++;
            return;
        }

        long endTicks = stopwatch.ElapsedTicks;
        float elapsedMs = (float)(endTicks - frameAssemblyStartTicks) * 1000f / Stopwatch.Frequency;
        LastReassemblyTimeMs = elapsedMs;

        lock (lockObj)
        {
            Buffer.BlockCopy(assemblingBuffer, 0, readyBuffer, 0, (int)expectedFrameSize);
            readyBufferSize = (int)expectedFrameSize;
            readyFrameId = header.FrameId;
            readyCaptureTimestampMs = header.CaptureTimestampMs;
            readyReassemblyTimeMs = elapsedMs;
            hasNewCompletedFrame = true;
        }

        FramesCompleted++;
    }

    /// <summary>
    /// Extrae el frame JPEG más reciente listo para visualización.
    /// Invocado desde el hilo principal (Update). Cero colas intermedias: si hubo varios frames,
    /// entrega únicamente el último.
    /// </summary>
    public bool TryGetLatestFrame(byte[] targetBuffer, out int frameSize, out uint frameId, out uint captureTimestampMs, out float reassemblyTimeMs)
    {
        lock (lockObj)
        {
            if (!hasNewCompletedFrame || readyBufferSize <= 0)
            {
                frameSize = 0;
                frameId = 0;
                captureTimestampMs = 0;
                reassemblyTimeMs = 0;
                return false;
            }

            if (targetBuffer.Length < readyBufferSize)
            {
                frameSize = 0;
                frameId = 0;
                captureTimestampMs = 0;
                reassemblyTimeMs = 0;
                return false;
            }

            Buffer.BlockCopy(readyBuffer, 0, targetBuffer, 0, readyBufferSize);
            frameSize = readyBufferSize;
            frameId = readyFrameId;
            captureTimestampMs = readyCaptureTimestampMs;
            reassemblyTimeMs = readyReassemblyTimeMs;
            hasNewCompletedFrame = false;
            return true;
        }
    }
}

