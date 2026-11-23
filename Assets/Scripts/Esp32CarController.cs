using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Única responsabilidad: conectarse a la ESP32 y enviarle el estado de los controles.
/// No lee teclado: escucha a un CarInputReader.
/// </summary>
public class Esp32CarController : MonoBehaviour
{
    [Header("Origen de controles")]
    [Tooltip("Si se deja vacío, se busca en este mismo GameObject")]
    [SerializeField] private CarInputReader inputReader;

    [Header("Configuración WebSocket")]
    [Tooltip("IP de la ESP32 (en modo AP suele ser 192.168.4.1)")]
    public string esp32Ip = "192.168.4.1";

    [Tooltip("Puerto WebSocket configurado en la ESP32")]
    public int esp32Port = 81;

    [Header("Estado de Conexión")]
    public bool isConnected;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cts;
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1); // ClientWebSocket no admite envíos concurrentes
    private bool isConnecting;

    private void Awake()
    {
        if (inputReader == null) inputReader = GetComponent<CarInputReader>();
        if (inputReader == null)
            Debug.LogError("[Esp32CarController] Falta un CarInputReader.", this);
    }

    private async void Start()
    {
        await ConnectWebSocket();
    }

    private void OnEnable()
    {
        if (inputReader != null) inputReader.StateChanged += OnInputChanged;
    }

    private async void OnDisable()
    {
        if (inputReader != null) inputReader.StateChanged -= OnInputChanged;

        // Enviar neutro al desactivarse
        await SendControlState(CarInputState.Neutral);
    }

    private async void OnDestroy()
    {
        try
        {
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                await SendControlState(CarInputState.Neutral);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch { }

        cts?.Cancel();
        webSocket?.Dispose();
        sendLock.Dispose();
    }

    private async void OnInputChanged(CarInputState state)
    {
        await SendControlState(state);
    }

    // ───────────────────────── Conexión ─────────────────────────

    private async Task ConnectWebSocket()
    {
        if (isConnecting) return;
        isConnecting = true;

        try
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            webSocket?.Dispose();
            webSocket = new ClientWebSocket();

            Uri uri = new Uri($"ws://{esp32Ip}:{esp32Port}");
            Debug.Log($"[Esp32CarController] Conectando a {uri}...");

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            connectCts.CancelAfter(4000); // Timeout de 4 segundos
            await webSocket.ConnectAsync(uri, connectCts.Token);

            isConnected = (webSocket.State == WebSocketState.Open);
            Debug.Log("[Esp32CarController] ¡Conectado con éxito a la ESP32!");

            // 1. Seleccionar driver "wasd" en el ControlRouter
            await SendRawMessage("{\"type\":\"select\",\"driver\":\"wasd\"}");

            // 2. Enviar el estado actual (neutro o lo que esté presionado al reconectar)
            await SendControlState(inputReader != null ? inputReader.Current : CarInputState.Neutral);
        }
        catch (Exception ex)
        {
            isConnected = false;
            Debug.LogWarning($"[Esp32CarController] Error de conexión: {ex.Message}. Reintentando en 3s...");
            _ = RetryConnectAfterDelay(3000);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task RetryConnectAfterDelay(int delayMs)
    {
        await Task.Delay(delayMs);
        if (this != null && enabled && (webSocket == null || webSocket.State != WebSocketState.Open))
        {
            await ConnectWebSocket();
        }
    }

    // ───────────────────────── Protocolo ─────────────────────────

    private Task SendControlState(CarInputState s)
    {
        // Estructura exacta esperada por la ESP32:
        // {"type":"control","data":{"forward":true,"backward":false,"left":false,"right":false}}
        string json =
            $"{{\"type\":\"control\",\"data\":{{" +
            $"\"forward\":{Bool(s.Forward)},\"backward\":{Bool(s.Backward)}," +
            $"\"left\":{Bool(s.Left)},\"right\":{Bool(s.Right)}}}}}";
        return SendRawMessage(json);
    }

    private static string Bool(bool value) => value ? "true" : "false";

    // ───────────────────────── Transporte ─────────────────────────

    private async Task SendRawMessage(string message)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open) return;

        byte[] bytes = Encoding.UTF8.GetBytes(message);

        await sendLock.WaitAsync();
        try
        {
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Esp32CarController] Error al enviar comando: {ex.Message}");
            isConnected = false;
            _ = ConnectWebSocket();
        }
        finally
        {
            sendLock.Release();
        }
    }
}
