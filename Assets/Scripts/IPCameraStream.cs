using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// Streams an IP camera MJPEG feed into a Unity texture.
/// Attach this script to a GameObject with a Renderer (e.g., a Plane).
/// </summary>
public class IPCameraStream : MonoBehaviour
{
    [Header("IP Camera Settings")]
    [Tooltip("Full MJPEG URL of the IP camera, e.g., http://192.168.0.102:8080/video")]
    public string cameraUrl = "http://192.168.0.102:8080/video";

    [Tooltip("Refresh rate in seconds for fetching frames")]
    public float refreshInterval = 0.1f;

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (string.IsNullOrEmpty(cameraUrl))
        {
            Debug.LogError("Camera URL is empty. Please set it in the inspector.");
            enabled = false;
            return;
        }

        // Start streaming frames
        StartCoroutine(StreamCamera());
    }

    IEnumerator StreamCamera()
    {
        while (true)
        {
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(cameraUrl))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Camera stream error: {uwr.error}");
                }
                else
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                    rend.material.mainTexture = tex;
                }
            }

            yield return new WaitForSeconds(refreshInterval);
        }
    }
}



