using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receptor de red UDP nativo de alta velocidad para streaming de video en Unity / Android.
/// Ejecuta la escucha y el procesamiento en un hilo secundario independiente con buffers amplios
/// para evitar pérdidas por desbordamiento del socket del sistema operativo.
/// </summary>
public class UdpVideoReceiver : IDisposable
{
    private readonly int listenPort;
    private readonly string esp32Host;
    private readonly int esp32Port;
    private readonly UdpFrameAssembler frameAssembler;

    private Socket socket;
    private Thread networkThread;
    private volatile bool isRunning;
    private IPEndPoint esp32EndPoint;

    // Métricas de red
    public float CurrentThroughputKBps { get; private set; }
    public float CurrentPacketsPerSecond { get; private set; }
    public bool IsReceivingData { get; private set; }

    private long totalBytesInterval;
    private int totalPacketsInterval;
    private float lastMetricsTime;

    public UdpFrameAssembler Assembler => frameAssembler;

    public UdpVideoReceiver(int port = 8282, string targetHost = "192.168.4.1", int targetPort = 8282)
    {
        listenPort = port;
        esp32Host = targetHost;
        esp32Port = targetPort;
        frameAssembler = new UdpFrameAssembler();
    }

    public void Start()
    {
        if (isRunning) return;

        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            
            // Buffer de recepción amplio (512 KB) para absorber ráfagas Wi-Fi sin descartes
            socket.ReceiveBufferSize = 512 * 1024;
            socket.ReceiveTimeout = 1000;

            IPEndPoint localEp = new IPEndPoint(IPAddress.Any, listenPort);
            socket.Bind(localEp);

            if (IPAddress.TryParse(esp32Host, out var ip))
            {
                esp32EndPoint = new IPEndPoint(ip, esp32Port);
            }
            else
            {
                esp32EndPoint = new IPEndPoint(IPAddress.Broadcast, esp32Port);
            }

            isRunning = true;
            networkThread = new Thread(NetworkLoop)
            {
                IsBackground = true,
                Name = "UDP_Video_Network_Receiver",
                Priority = System.Threading.ThreadPriority.AboveNormal
            };
            networkThread.Start();

            Debug.Log($"[UdpVideoReceiver] Escuchando datagramas de video en puerto UDP {listenPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UdpVideoReceiver] Error al iniciar socket UDP: {ex.Message}");
            Stop();
        }
    }

    public void Stop()
    {
        isRunning = false;

        try
        {
            socket?.Close();
        }
        catch { }

        if (networkThread != null && networkThread.IsAlive)
        {
            networkThread.Join(500);
        }

        socket = null;
        networkThread = null;
        IsReceivingData = false;
        Debug.Log("[UdpVideoReceiver] Receptor UDP detenido.");
    }

    private void NetworkLoop()
    {
        byte[] receiveBuffer = new byte[2048];
        EndPoint senderRemote = new IPEndPoint(IPAddress.Any, 0);

        byte[] pingPayload = Encoding.ASCII.GetBytes("PING_VR");
        long lastPingTicks = 0;
        long lastDataReceivedTicks = DateTime.UtcNow.Ticks;

        while (isRunning)
        {
            try
            {
                // Enviar ping periódico (cada 1.5s) para registro de IP en el AP de la ESP32
                long nowTicks = DateTime.UtcNow.Ticks;
                if ((nowTicks - lastPingTicks) > TimeSpan.FromSeconds(1.5).Ticks && esp32EndPoint != null)
                {
                    lastPingTicks = nowTicks;
                    try
                    {
                        socket.SendTo(pingPayload, esp32EndPoint);
                    }
                    catch { }
                }

                // Recepción de datagramas
                int bytesRead = socket.ReceiveFrom(receiveBuffer, ref senderRemote);
                if (bytesRead > 0)
                {
                    lastDataReceivedTicks = nowTicks;
                    totalBytesInterval += bytesRead;
                    totalPacketsInterval++;

                    // Pasar datagrama directo al ensamblador sin asignaciones
                    frameAssembler.ProcessPacket(receiveBuffer, bytesRead);
                }
            }
            catch (SocketException ex)
            {
                // Timeout normal para poder chequear isRunning y enviar pings
                if (ex.SocketErrorCode != SocketError.TimedOut && ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    if (isRunning)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch (Exception)
            {
                if (isRunning)
                {
                    Thread.Sleep(10);
                }
            }

            // Actualizar estado de recepción activa
            long elapsedTicksSinceData = DateTime.UtcNow.Ticks - lastDataReceivedTicks;
            IsReceivingData = elapsedTicksSinceData < TimeSpan.FromSeconds(2.0).Ticks;
        }
    }

    /// <summary>
    /// Calcula las tasas de red promedio por segundo (invocar periódicamente desde Update).
    /// </summary>
    public void UpdateMetrics(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        CurrentThroughputKBps = (totalBytesInterval / 1024f) / deltaTime;
        CurrentPacketsPerSecond = totalPacketsInterval / deltaTime;

        totalBytesInterval = 0;
        totalPacketsInterval = 0;
    }

    public void Dispose()
    {
        Stop();
    }
}

