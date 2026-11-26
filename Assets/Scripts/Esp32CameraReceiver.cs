using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class Esp32CameraReceiver : MonoBehaviour
{
    [SerializeField] private string host = "192.168.4.1";
    [SerializeField] private int cameraPort = 5005;
    [SerializeField] private float frameTimeoutSeconds = 0.2f;

    [Header("Salida al shader")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private string textureProperty = "_MainTex";

    [Header("Iluminación reactiva")]
    [SerializeField] private bool affectAmbientLight = true;
    [SerializeField, Range(0.5f, 10f)] private float lightMultiplier = 2.5f;
    [SerializeField, Range(1f, 30f)] private float lightSmoothSpeed = 8f;
    [SerializeField] private Light targetLight;
    [SerializeField, Range(8, 64)] private int sampleStride = 24;

    private readonly Dictionary<uint, PendingFrame> pendingFrames =
        new Dictionary<uint, PendingFrame>();
    private readonly List<uint> tempFrameIds = new List<uint>();
    private readonly object frameLock = new object();

    private byte[] latestJpeg;
    private uint lastCompletedFrameId;

    private UdpClient udpClient;
    private CancellationTokenSource receiveCancellation;
    private Task receiveTask;
    private float nextKeepAlive;
    private Color currentAmbientColor;
    private Color targetAmbientColor;

    public Texture2D LatestFrame { get; private set; }

    public event Action<Texture2D> FrameReceived;

    private void Awake()
    {
        ResolveTargetMaterial();
    }

    private void OnEnable()
    {
        ResolveTargetMaterial();
        SetMarginPreview(true);
        currentAmbientColor = RenderSettings.ambientLight;
        targetAmbientColor = currentAmbientColor;
        FrameReceived += OnFrameReceived;
        StartReceiver();
    }

    private void OnDisable()
    {
        FrameReceived -= OnFrameReceived;
        StopReceiver();
        SetMarginPreview(true);
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    private void Update()
    {
        byte[] jpeg;
        lock (frameLock)
        {
            jpeg = latestJpeg;
            latestJpeg = null;
        }

        if (jpeg != null)
        {
            if (LatestFrame == null)
            {
                LatestFrame = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }

            if (LatestFrame.LoadImage(jpeg, false))
            {
                FrameReceived?.Invoke(LatestFrame);
            }
        }

        if (udpClient != null && Time.unscaledTime >= nextKeepAlive)
        {
            nextKeepAlive = Time.unscaledTime + 2f;
            _ = SendCommandAsync("START");
        }

        if (affectAmbientLight)
        {
            ApplySmoothedLighting();
        }
    }

    private void ResolveTargetMaterial()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetMaterial == null && targetRenderer != null)
        {
            targetMaterial = targetRenderer.material;
        }
    }

    private void SetMarginPreview(bool visible)
    {
        if (targetMaterial != null && targetMaterial.HasProperty("_PreviewMargenes"))
        {
            targetMaterial.SetFloat("_PreviewMargenes", visible ? 1f : 0f);
        }
    }

    private void OnFrameReceived(Texture2D frame)
    {
        if (targetMaterial == null)
        {
            ResolveTargetMaterial();
        }

        if (targetMaterial == null)
        {
            return;
        }

        if (targetMaterial.HasProperty(textureProperty))
        {
            SetMarginPreview(false);
            targetMaterial.SetTexture(textureProperty, frame);
            if (affectAmbientLight)
            {
                UpdateAmbientLighting(frame);
            }
        }
        else
        {
            Debug.LogWarning($"ESP32 camera: el material no tiene la propiedad {textureProperty}.");
        }
    }

    private void UpdateAmbientLighting(Texture2D frame)
    {
        Color32[] pixels = frame.GetPixels32();
        if (pixels == null || pixels.Length == 0)
        {
            return;
        }

        long totalR = 0;
        long totalG = 0;
        long totalB = 0;
        int count = 0;
        int step = Mathf.Max(1, sampleStride);

        for (int index = 0; index < pixels.Length; index += step)
        {
            totalR += pixels[index].r;
            totalG += pixels[index].g;
            totalB += pixels[index].b;
            count++;
        }

        float scale = lightMultiplier / (255f * count);
        targetAmbientColor = new Color(totalR * scale, totalG * scale, totalB * scale, 1f);
    }

    private void ApplySmoothedLighting()
    {
        currentAmbientColor = Color.Lerp(currentAmbientColor, targetAmbientColor,
            Time.deltaTime * lightSmoothSpeed);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = currentAmbientColor;

        if (targetLight == null)
        {
            return;
        }

        float maxChannel = Mathf.Max(currentAmbientColor.r,
            Mathf.Max(currentAmbientColor.g, currentAmbientColor.b));
        targetLight.color = maxChannel > 0.0001f
            ? new Color(currentAmbientColor.r / maxChannel, currentAmbientColor.g / maxChannel,
                currentAmbientColor.b / maxChannel)
            : Color.black;
        targetLight.intensity = maxChannel;
    }

    public void StartReceiver()
    {
        if (receiveCancellation != null)
        {
            return;
        }

        try
        {
            lock (pendingFrames)
            {
                pendingFrames.Clear();
                lastCompletedFrameId = 0;
            }
            lock (frameLock)
            {
                latestJpeg = null;
            }

            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            udpClient.Client.ReceiveBufferSize = 131072; // 128 KB para absorber rafagas sin acumular cola
            receiveCancellation = new CancellationTokenSource();
            receiveTask = Task.Run(() => ReceiveLoopAsync(udpClient, receiveCancellation.Token));
            nextKeepAlive = 0f;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"ESP32 camera UDP: {exception.Message}");
        }
    }

    public void StopReceiver()
    {
        _ = SendCommandAsync("STOP");

        receiveCancellation?.Cancel();
        receiveCancellation = null;
        udpClient?.Close();
        udpClient = null;

        lock (frameLock)
        {
            latestJpeg = null;
        }

        lock (pendingFrames)
        {
            pendingFrames.Clear();
            lastCompletedFrameId = 0;
        }
    }

    private async Task SendCommandAsync(string command)
    {
        if (udpClient == null || !IPAddress.TryParse(host, out IPAddress address))
        {
            return;
        }

        try
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(command);
            await udpClient.SendAsync(data, data.Length,
                new IPEndPoint(address, cameraPort));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"ESP32 camera UDP: {exception.Message}");
        }
    }

    private async Task ReceiveLoopAsync(UdpClient client, CancellationToken cancellationToken)
    {
        await SendCommandAsync("START").ConfigureAwait(false);

        IPEndPoint remoteEndPoint = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await client.ReceiveAsync().ConfigureAwait(false);
                ProcessPacket(result.Buffer);

                // Vaciar inmediatamente todos los datagramas ya encolados en el socket
                // para evitar procesar frames viejos en orden FIFO.
                while (!cancellationToken.IsCancellationRequested && client.Available > 0)
                {
                    byte[] bufferedPacket = client.Receive(ref remoteEndPoint);
                    ProcessPacket(bufferedPacket);
                }

                ExpirePendingFrames();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ProcessPacket(byte[] packet)
    {
        if (packet.Length < 16 ||
            packet[0] != (byte)'A' || packet[1] != (byte)'E' ||
            packet[2] != (byte)'C' || packet[3] != (byte)'1')
        {
            return;
        }

        uint frameId = ReadUInt32(packet, 4);
        uint frameLength = ReadUInt32(packet, 8);
        ushort partIndex = ReadUInt16(packet, 12);
        ushort partCount = ReadUInt16(packet, 14);

        if (frameLength == 0 || frameLength > 65536 ||
            partCount == 0 || partCount > 512 || partIndex >= partCount)
        {
            return;
        }

        lock (pendingFrames)
        {
            // Descartar paquetes de frames mas viejos que el ultimo completado.
            // Si el frameId retrocede bruscamente (>10), el ESP32 se reinicio: resetear estado.
            if (frameId <= lastCompletedFrameId)
            {
                if (lastCompletedFrameId - frameId > 10)
                {
                    lastCompletedFrameId = 0;
                    pendingFrames.Clear();
                }
                else
                {
                    return;
                }
            }

            if (!pendingFrames.TryGetValue(frameId, out PendingFrame frame) ||
                frame.FrameLength != frameLength || frame.PartCount != partCount)
            {
                frame = new PendingFrame(frameLength, partCount);
                pendingFrames[frameId] = frame;
            }

            int payloadLength = packet.Length - 16;
            int destinationOffset = partIndex * 1384;
            if (destinationOffset + payloadLength > frameLength)
            {
                return;
            }

            Array.Copy(packet, 16, frame.Data, destinationOffset, payloadLength);
            if (!frame.Received[partIndex])
            {
                frame.Received[partIndex] = true;
                frame.ReceivedCount++;
            }

            frame.LastUpdateTicks = Stopwatch.GetTimestamp();

            if (frame.ReceivedCount == frame.PartCount)
            {
                lastCompletedFrameId = frameId;

                tempFrameIds.Clear();
                foreach (uint key in pendingFrames.Keys)
                {
                    if (key <= frameId)
                    {
                        tempFrameIds.Add(key);
                    }
                }
                foreach (uint key in tempFrameIds)
                {
                    pendingFrames.Remove(key);
                }

                byte[] jpeg = new byte[frameLength];
                Array.Copy(frame.Data, jpeg, frameLength);

                lock (frameLock)
                {
                    latestJpeg = jpeg;
                }
            }
        }
    }

    private void ExpirePendingFrames()
    {
        long nowTicks = Stopwatch.GetTimestamp();
        long timeoutTicks = (long)(frameTimeoutSeconds * Stopwatch.Frequency);

        lock (pendingFrames)
        {
            tempFrameIds.Clear();
            foreach (KeyValuePair<uint, PendingFrame> pair in pendingFrames)
            {
                if (nowTicks - pair.Value.LastUpdateTicks > timeoutTicks)
                {
                    tempFrameIds.Add(pair.Key);
                }
            }

            foreach (uint frameId in tempFrameIds)
            {
                pendingFrames.Remove(frameId);
            }
        }
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        return (uint)data[offset] |
               ((uint)data[offset + 1] << 8) |
               ((uint)data[offset + 2] << 16) |
               ((uint)data[offset + 3] << 24);
    }

    private static ushort ReadUInt16(byte[] data, int offset)
    {
        return (ushort)(data[offset] | (data[offset + 1] << 8));
    }

    private sealed class PendingFrame
    {
        public readonly uint FrameLength;
        public readonly ushort PartCount;
        public readonly byte[] Data;
        public readonly bool[] Received;
        public int ReceivedCount;
        public long LastUpdateTicks;

        public PendingFrame(uint frameLength, ushort partCount)
        {
            FrameLength = frameLength;
            PartCount = partCount;
            Data = new byte[frameLength];
            Received = new bool[partCount];
            LastUpdateTicks = Stopwatch.GetTimestamp();
        }
    }
}
