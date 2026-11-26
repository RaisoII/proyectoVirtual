using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Video;

public class CameraReceiver : MonoBehaviour
{
    [Header("Configuración de Red")]
    [Tooltip("URL del stream de IP Webcam (usar /video o /videofeed)")]
    public string streamUrl = "http://192.168.0.50:8080/video";

    [Header("Pantalla")]
    public Renderer screenRenderer;

    [Header("Diagnóstico")]
    public float currentFPS;

    [Header("══ Iluminación Ambiental Reactiva ══")]
    [Tooltip("¿Afectar la luz ambiental con el brillo y color del video?")]
    public bool affectAmbientLight = true;

    [Tooltip("Multiplicador de intensidad de luz (subilo si el video o el ambiente es muy oscuro)")]
    [Range(0.5f, 10.0f)]
    public float lightMultiplier = 2.5f;

    [Tooltip("Suavizado de la transición de luz (evita parpadeos bruscos)")]
    [Range(1f, 30f)]
    public float lightSmoothSpeed = 8.0f;

    [Tooltip("Luz direccional o foco del habitáculo opcional (si se deja vacío, afecta RenderSettings.ambientLight)")]
    public Light targetLight;

    [Tooltip("Salto de píxeles al analizar el frame (mayor valor = 0 impacto en CPU)")]
    [Range(8, 64)]
    public int sampleStride = 24;

    private Color currentAmbientColor = Color.black;

    private Texture2D tex;
    private Thread streamThread;
    private volatile bool isRunning;
    private TcpClient activeClient;

    private readonly object frameLock = new object();
    private byte[] latestFrame;
    private bool hasNewFrame;
    private bool firstFrameReported;

    private int frameCounter;
    private float fpsTimer;

    void Start()
    {
        // 1. Desactivar VideoPlayer si existe para evitar conflictos
        var vp = GetComponent<VideoPlayer>();
        if (vp != null && vp.enabled)
        {
            vp.enabled = false;
        }

        // 2. Auto-detectar Renderer
        if (screenRenderer == null)
        {
            screenRenderer = GetComponent<Renderer>();
        }

        // 3. Crear textura dinámica sin mipmaps
        tex = new Texture2D(2, 2, TextureFormat.RGB24, false);

        if (screenRenderer != null)
        {
            if (screenRenderer.material.HasProperty("_BaseMap"))
            {
                screenRenderer.material.SetTexture("_BaseMap", tex);
            }
            screenRenderer.material.mainTexture = tex;
            SetMarginPreview(true);
        }
        else
        {
            Debug.LogError("[CameraReceiver] No se encontró ningún Renderer asignado.");
        }

        currentAmbientColor = RenderSettings.ambientLight;

        // 4. Iniciar hilo de lectura de baja latencia
        isRunning = true;
        streamThread = new Thread(StreamLoop)
        {
            IsBackground = true,
            Name = "LowLatency_MJPEG_Receiver"
        };
        streamThread.Start();
    }

    void Update()
    {
        // Medir FPS reales recibidos
        fpsTimer += Time.deltaTime;
        if (fpsTimer >= 1f)
        {
            currentFPS = frameCounter / fpsTimer;
            frameCounter = 0;
            fpsTimer = 0f;
        }

        // Si hay un fotograma fresco listo, dibujarlo
        if (hasNewFrame)
        {
            byte[] frameToLoad = null;
            lock (frameLock)
            {
                if (hasNewFrame)
                {
                    frameToLoad = latestFrame;
                    hasNewFrame = false;
                }
            }

            if (frameToLoad != null && frameToLoad.Length > 0)
            {
                tex.LoadImage(frameToLoad);
                frameCounter++;

                if (!firstFrameReported)
                {
                    firstFrameReported = true;
                    SetMarginPreview(false);
                    Debug.Log("[CameraReceiver] ¡Conexión en tiempo real activa!");
                }

                // Calcular color promedio para iluminación ambiental
                if (affectAmbientLight)
                {
                    UpdateAmbientLighting();
                }
            }
        }

        // Transición suave de la luz en cada cuadro
        if (affectAmbientLight)
        {
            ApplySmoothedLighting();
        }
    }

    private void UpdateAmbientLighting()
    {
        // Muestreo ultrarrápido con strides sobre los píxeles crudos
        try
        {
            var rawColors = tex.GetPixels32();
            if (rawColors == null || rawColors.Length == 0) return;

            long totalR = 0, totalG = 0, totalB = 0;
            int count = 0;
            int step = sampleStride;

            for (int i = 0; i < rawColors.Length; i += step)
            {
                totalR += rawColors[i].r;
                totalG += rawColors[i].g;
                totalB += rawColors[i].b;
                count++;
            }

            if (count > 0)
            {
                float avgR = (totalR / (float)count) / 255f;
                float avgG = (totalG / (float)count) / 255f;
                float avgB = (totalB / (float)count) / 255f;

                // Color promedio ponderado con el multiplicador
                targetAmbientColor = new Color(avgR, avgG, avgB) * lightMultiplier;
            }
        }
        catch { }
    }

    private Color targetAmbientColor = Color.black;

    private void SetMarginPreview(bool visible)
    {
        if (screenRenderer != null && screenRenderer.material.HasProperty("_PreviewMargenes"))
        {
            screenRenderer.material.SetFloat("_PreviewMargenes", visible ? 1f : 0f);
        }
    }

    private void ApplySmoothedLighting()
    {
        currentAmbientColor = Color.Lerp(currentAmbientColor, targetAmbientColor, Time.deltaTime * lightSmoothSpeed);

        // 1. Aplicar a la luz ambiental global de Unity
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = currentAmbientColor;

        // 2. Si asignaste una luz específica (ej: luz del habitáculo o direccional)
        if (targetLight != null)
        {
            targetLight.color = currentAmbientColor;
            targetLight.intensity = Mathf.Max(0.1f, (currentAmbientColor.r + currentAmbientColor.g + currentAmbientColor.b) / 3f);
        }
    }

    private void StreamLoop()
    {
        byte[] readChunk = new byte[32768];
        byte[] accumulator = new byte[512 * 1024];
        int accLength = 0;

        while (isRunning)
        {
            try
            {
                string urlStr = streamUrl;
                if (urlStr.EndsWith("/shot.jpg"))
                {
                    urlStr = urlStr.Replace("/shot.jpg", "/video");
                }

                Uri uri = new Uri(urlStr);
                string host = uri.Host;
                int port = uri.Port > 0 ? uri.Port : 8080;
                string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/video" : uri.PathAndQuery;

                Debug.Log($"[CameraReceiver] Conectando socket de baja latencia a {host}:{port}...");

                activeClient = new TcpClient();
                activeClient.NoDelay = true;
                activeClient.ReceiveBufferSize = 32768;
                activeClient.SendTimeout = 4000;
                activeClient.ReceiveTimeout = 4000;

                activeClient.Connect(host, port);

                using (NetworkStream stream = activeClient.GetStream())
                {
                    string getRequest = $"GET {path} HTTP/1.1\r\nHost: {host}:{port}\r\nConnection: close\r\n\r\n";
                    byte[] reqBytes = Encoding.ASCII.GetBytes(getRequest);
                    stream.Write(reqBytes, 0, reqBytes.Length);

                    accLength = 0;

                    while (isRunning)
                    {
                        int bytesRead = stream.Read(readChunk, 0, readChunk.Length);
                        if (bytesRead <= 0) break;

                        if (accLength + bytesRead > accumulator.Length)
                        {
                            accLength = 0;
                        }

                        Array.Copy(readChunk, 0, accumulator, accLength, bytesRead);
                        accLength += bytesRead;

                        int lastEOI = -1;
                        for (int i = accLength - 1; i >= 1; i--)
                        {
                            if (accumulator[i - 1] == 0xFF && accumulator[i] == 0xD9)
                            {
                                lastEOI = i;
                                break;
                            }
                        }

                        if (lastEOI != -1)
                        {
                            int matchingSOI = -1;
                            for (int i = lastEOI - 1; i >= 1; i--)
                            {
                                if (accumulator[i - 1] == 0xFF && accumulator[i] == 0xD8)
                                {
                                    matchingSOI = i - 1;
                                    break;
                                }
                            }

                            if (matchingSOI != -1)
                            {
                                int frameSize = lastEOI - matchingSOI + 1;
                                byte[] frameData = new byte[frameSize];
                                Array.Copy(accumulator, matchingSOI, frameData, 0, frameSize);

                                lock (frameLock)
                                {
                                    latestFrame = frameData;
                                    hasNewFrame = true;
                                }

                                int remaining = accLength - (lastEOI + 1);
                                if (remaining > 0)
                                {
                                    Array.Copy(accumulator, lastEOI + 1, accumulator, 0, remaining);
                                    accLength = remaining;
                                }
                                else
                                {
                                    accLength = 0;
                                }
                            }
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Debug.LogWarning($"[CameraReceiver] Reconectando stream ({ex.Message})...");
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                try { activeClient?.Close(); } catch { }
                activeClient = null;
            }
        }
    }

    private void OnDisable()
    {
        SetMarginPreview(true);
    }

    private void OnDestroy()
    {
        isRunning = false;
        try { activeClient?.Close(); } catch { }

        if (streamThread != null && streamThread.IsAlive)
        {
            streamThread.Join(500);
        }

        if (tex != null)
        {
            Destroy(tex);
        }
    }

    private void OnApplicationQuit()
    {
        isRunning = false;
        try { activeClient?.Close(); } catch { }
    }
}
