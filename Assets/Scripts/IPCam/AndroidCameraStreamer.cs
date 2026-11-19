using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Android;

public class AndroidCameraStreamer : MonoBehaviour
{
    [Header("Configuración de Red y Cámara")]
    [SerializeField] private int port = 8080;
    [SerializeField] private int requestedWidth = 1280;
    [SerializeField] private int requestedHeight = 720;
    [SerializeField] private int requestedFPS = 30;
    [Range(10, 100)]
    [SerializeField] private int jpegQuality = 70;

    private WebCamTexture webCamTexture;
    private Texture2D frameTexture;
    private TcpListener tcpListener;
    private Thread serverThread;
    private bool isRunning = false;

    private byte[] currentJpegFrame;
    private readonly object frameLock = new object();
    private const string Boundary = "frameboundary";

    private void Start()
    {
        // Forzar orientación horizontal y evitar que se apague la pantalla
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        StartCoroutine(InitializeCameraAndServer());
    }

    private IEnumerator InitializeCameraAndServer()
    {
        // Solicitar permiso de cámara en tiempo de ejecución (Android)
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                yield return null;
            }
        }

        // Buscar explícitamente la cámara trasera
        WebCamDevice[] devices = WebCamTexture.devices;
        string backCamName = null;

        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                backCamName = devices[i].name;
                break;
            }
        }

        if (string.IsNullOrEmpty(backCamName))
        {
            Debug.LogError("No se encontró ninguna cámara trasera.");
            yield break;
        }

        webCamTexture = new WebCamTexture(backCamName, requestedWidth, requestedHeight, requestedFPS);
        webCamTexture.Play();

        // Esperar a que la cámara entregue dimensiones válidas (>16px es el estándar de Unity)
        while (webCamTexture.width <= 16)
        {
            yield return null;
        }

        frameTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);

        // Iniciar el servidor HTTP en un hilo secundario
        isRunning = true;
        serverThread = new Thread(ListenForClients);
        serverThread.IsBackground = true;
        serverThread.Start();

        Debug.Log($"Stream activo en http://<IP_DEL_TELEFONO>:{port}/");
    }

    private void Update()
    {
        // Capturar el frame en el hilo principal de Unity cuando haya actualización
        if (webCamTexture != null && webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
        {
            frameTexture.SetPixels32(webCamTexture.GetPixels32());
            frameTexture.Apply(false);

            byte[] encoded = frameTexture.EncodeToJPG(jpegQuality);
            lock (frameLock)
            {
                currentJpegFrame = encoded;
            }
        }
    }

    private void ListenForClients()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();

            while (isRunning)
            {
                if (!tcpListener.Pending())
                {
                    Thread.Sleep(10);
                    continue;
                }

                TcpClient client = tcpListener.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }
        catch (Exception e)
        {
            if (isRunning) Debug.LogError($"Error en servidor TCP: {e.Message}");
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                // Leer la petición HTTP entrante (GET / o GET /stream)
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                // Si piden /stream entregamos los bytes MJPEG puros; si piden /, entregamos la página HTML
                if (request.StartsWith("GET /stream"))
                {
                    StreamMJPEG(stream);
                }
                else
                {
                    ServeHtmlPage(stream);
                }
            }
        }
        catch
        {
            // Desconexión normal del navegador/cliente
        }
    }

    private void ServeHtmlPage(NetworkStream stream)
    {
        string html = "<!DOCTYPE html><html><head><meta name='viewport' content='width=device-width, initial-scale=1.0'>" +
                      "<style>body{margin:0;background:#000;display:flex;justify-content:center;align-items:center;height:100vh;overflow:hidden;}" +
                      "img{width:100vw;height:100vh;object-fit:contain;}</style></head>" +
                      "<body><img src='/stream'/></body></html>";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(html);
        string header = "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: text/html; charset=utf-8\r\n" +
                        $"Content-Length: {bodyBytes.Length}\r\n" +
                        "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(bodyBytes, 0, bodyBytes.Length);
        stream.Flush();
    }

    private void StreamMJPEG(NetworkStream stream)
    {
        string header = "HTTP/1.1 200 OK\r\n" +
                        "Cache-Control: no-cache, private\r\n" +
                        "Pragma: no-cache\r\n" +
                        $"Content-Type: multipart/x-mixed-replace; boundary={Boundary}\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);

        byte[] lastSentFrame = null;

        while (isRunning && stream.CanWrite)
        {
            byte[] frameToSend = null;
            lock (frameLock)
            {
                if (currentJpegFrame != null && currentJpegFrame != lastSentFrame)
                {
                    frameToSend = currentJpegFrame;
                    lastSentFrame = frameToSend;
                }
            }

            if (frameToSend != null)
            {
                string partHeader = $"--{Boundary}\r\n" +
                                    "Content-Type: image/jpeg\r\n" +
                                    $"Content-Length: {frameToSend.Length}\r\n\r\n";
                byte[] partHeaderBytes = Encoding.ASCII.GetBytes(partHeader);
                byte[] footerBytes = Encoding.ASCII.GetBytes("\r\n");

                stream.Write(partHeaderBytes, 0, partHeaderBytes.Length);
                stream.Write(frameToSend, 0, frameToSend.Length);
                stream.Write(footerBytes, 0, footerBytes.Length);
                stream.Flush();
            }

            Thread.Sleep(15); // Evita saturar el CPU del hilo de red
        }
    }

    private void OnDestroy()
    {
        isRunning = false;
        tcpListener?.Stop();
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }
    }
}