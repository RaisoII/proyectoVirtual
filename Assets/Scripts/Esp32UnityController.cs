using System;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

public sealed class Esp32UnityController : MonoBehaviour
{
    [SerializeField] private string host = "192.168.4.1";
    [SerializeField] private int port = 81;
    [SerializeField] private float reconnectDelaySeconds = 2f;

    private WebSocket socket;
    private bool connecting;
    private bool driverSelected;
    private float nextConnectionAttempt;

    public bool IsReady =>
        driverSelected && socket != null && socket.State == WebSocketState.Open;

    private void Start()
    {
        TryConnect();
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        socket?.DispatchMessageQueue();
#endif

        if (!connecting && !IsReady && Time.unscaledTime >= nextConnectionAttempt)
        {
            TryConnect();
        }
    }

    public void SetControls(float steering, float throttle)
    {
        if (!IsReady)
        {
            return;
        }

        var message = new ControlMessage
        {
            data = new ControlData
            {
                steering = Mathf.Clamp(steering, -1f, 1f),
                throttle = Mathf.Clamp(throttle, -1f, 1f)
            }
        };

        _ = SendTextAsync(socket, JsonUtility.ToJson(message));
    }

    public void SetReverse(bool reverse)
    {
        if (!IsReady)
        {
            return;
        }

        var message = new GearMessage
        {
            data = new GearData { gear = reverse ? "R" : "D" }
        };

        _ = SendTextAsync(socket, JsonUtility.ToJson(message));
    }

    private async void TryConnect()
    {
        if (connecting)
        {
            return;
        }

        connecting = true;
        driverSelected = false;

        var connection = new WebSocket($"ws://{host}:{port}");
        socket = connection;

        connection.OnOpen += () =>
        {
            if (socket == connection)
            {
                _ = InitializeConnectionAsync(connection);
            }
        };

        connection.OnError += error =>
        {
            if (socket == connection)
            {
                Debug.LogWarning($"ESP32 WebSocket: {error}");
                ScheduleReconnect();
            }
        };

        connection.OnClose += code =>
        {
            if (socket == connection)
            {
                driverSelected = false;
                ScheduleReconnect();
            }
        };

        try
        {
            await connection.Connect();
        }
        catch (Exception exception)
        {
            if (socket == connection)
            {
                Debug.LogWarning($"No se pudo conectar con la ESP32: {exception.Message}");
                ScheduleReconnect();
            }
        }
    }

    private async Task InitializeConnectionAsync(WebSocket connection)
    {
        try
        {
            await connection.SendText("{\"type\":\"select\",\"driver\":\"unity\"}");
            await connection.SendText(
                "{\"type\":\"control\",\"data\":{\"steering\":0,\"throttle\":0}}");

            if (socket == connection)
            {
                driverSelected = true;
                connecting = false;
                Debug.Log("Driver unity seleccionado en la ESP32.");
            }
        }
        catch (Exception exception)
        {
            if (socket == connection)
            {
                Debug.LogWarning($"No se pudo inicializar el control ESP32: {exception.Message}");
                ScheduleReconnect();
            }
        }
    }

    private async Task SendTextAsync(WebSocket connection, string message)
    {
        try
        {
            await connection.SendText(message);
        }
        catch (Exception exception)
        {
            if (socket == connection)
            {
                Debug.LogWarning($"Error enviando control a la ESP32: {exception.Message}");
                driverSelected = false;
                ScheduleReconnect();
            }
        }
    }

    private void ScheduleReconnect()
    {
        connecting = false;
        driverSelected = false;
        nextConnectionAttempt = Time.unscaledTime + reconnectDelaySeconds;
    }

    private async void OnDestroy()
    {
        if (socket == null)
        {
            return;
        }

        try
        {
            await socket.Close();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Error cerrando WebSocket: {exception.Message}");
        }
    }

    [Serializable]
    private sealed class ControlMessage
    {
        public string type = "control";
        public ControlData data;
    }

    [Serializable]
    private sealed class ControlData
    {
        public float steering;
        public float throttle;
    }

    [Serializable]
    private sealed class GearMessage
    {
        public string type = "control";
        public GearData data;
    }

    [Serializable]
    private sealed class GearData
    {
        public string gear;
    }
}