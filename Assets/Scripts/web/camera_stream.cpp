#include "camera_stream.h"
#include "udp_video_sender.h"
#include "web_control_interface.h"

#include <Arduino.h>
#include <WiFi.h>
#include <esp_camera.h>
#include <esp_http_server.h>

namespace {
constexpr uint16_t cameraHttpPort = 82;
constexpr char multipartBoundary[] = "frame";

// Mapeo de pines para AI-Thinker ESP32-CAM
constexpr int cameraPinPwdn = 32;
constexpr int cameraPinReset = -1;
constexpr int cameraPinXclk = 0;
constexpr int cameraPinSiod = 26;
constexpr int cameraPinSioc = 27;
constexpr int cameraPinY9 = 35;
constexpr int cameraPinY8 = 34;
constexpr int cameraPinY7 = 39;
constexpr int cameraPinY6 = 36;
constexpr int cameraPinY5 = 21;
constexpr int cameraPinY4 = 19;
constexpr int cameraPinY3 = 18;
constexpr int cameraPinY2 = 5;
constexpr int cameraPinVsync = 25;
constexpr int cameraPinHref = 23;
constexpr int cameraPinPclk = 22;

httpd_handle_t cameraHttpServer = nullptr;
bool cameraReady = false;
TaskHandle_t udpStreamTaskHandle = nullptr;
volatile bool udpStreamRunning = false;

// ══════════════════════════════════════════════════════════════════════
// TAREA FREERTOS DEDICADA: Captura y Transmisión UDP de Ultra-Baja Latencia
// Se ejecuta en Core 0 para no competir con el stack Wi-Fi/Arduino (Core 1)
// ══════════════════════════════════════════════════════════════════════
void cameraUdpStreamTask(void *pvParameters) {
  uint32_t frameIdCounter = 1;

  Serial.println("[CameraUDP] Tarea de streaming UDP iniciada en Core 0");

  while (udpStreamRunning) {
    if (!cameraReady) {
      vTaskDelay(pdMS_TO_TICKS(100));
      continue;
    }

    // Capturar frame más reciente (CAMERA_GRAB_LATEST descarta frames antiguos en sensor)
    camera_fb_t *fb = esp_camera_fb_get();
    if (fb == nullptr) {
      vTaskDelay(pdMS_TO_TICKS(5));
      continue;
    }

    const uint32_t captureTimestamp = millis();

    // Fragmentar y transmitir directamente por UDP
    udpVideoSender.sendFrame(fb->buf, fb->len, frameIdCounter++, captureTimestamp);

    // Liberar inmediatamente el framebuffer a la PSRAM
    esp_camera_fb_return(fb);

    // Mínimo rendimiento de CPU (1ms) para que el watchdog y tareas del sistema respiren
    vTaskDelay(pdMS_TO_TICKS(1));
  }

  Serial.println("[CameraUDP] Tarea de streaming UDP finalizada");
  vTaskDelete(nullptr);
}

// ══════════════════════════════════════════════════════════════════════
// HTTP SERVER (Puerto 82) - Fallback comparativo MJPEG y Diagnóstico
// ══════════════════════════════════════════════════════════════════════
esp_err_t streamHandler(httpd_req_t *request) {
  if (!cameraReady) {
    httpd_resp_set_status(request, "503 Service Unavailable");
    httpd_resp_set_type(request, "text/plain");
    return httpd_resp_sendstr(request, "Camera is not initialized");
  }

  char contentType[64];
  snprintf(contentType, sizeof(contentType),
           "multipart/x-mixed-replace;boundary=%s", multipartBoundary);
  httpd_resp_set_type(request, contentType);
  httpd_resp_set_hdr(request, "Cache-Control", "no-store");

  while (true) {
    camera_fb_t *frame = esp_camera_fb_get();
    if (frame == nullptr) {
      Serial.println("[CameraHTTP] Frame capture failed");
      return ESP_FAIL;
    }

    char partHeader[96];
    const int headerLength = snprintf(
        partHeader, sizeof(partHeader),
        "--%s\r\nContent-Type: image/jpeg\r\nContent-Length: %u\r\n\r\n",
        multipartBoundary, static_cast<unsigned int>(frame->len));

    esp_err_t result = httpd_resp_send_chunk(request, partHeader, headerLength);
    if (result == ESP_OK) {
      result = httpd_resp_send_chunk(request,
                                     reinterpret_cast<const char *>(frame->buf),
                                     frame->len);
    }
    if (result == ESP_OK) {
      result = httpd_resp_send_chunk(request, "\r\n", 2);
    }
    esp_camera_fb_return(frame);
    if (result != ESP_OK) break;
  }

  return ESP_OK;
}

esp_err_t healthHandler(httpd_req_t *request) {
  sensor_t *sensor = cameraReady ? esp_camera_sensor_get() : nullptr;
  char body[192];
  snprintf(body, sizeof(body),
           "{\"camera\":\"%s\",\"sensor_pid\":\"%04x\",\"udp_fps\":%.1f,\"udp_packets\":%u,\"udp_errors\":%u}",
           cameraReady ? "ready" : "unavailable",
           sensor != nullptr ? sensor->id.PID : 0,
           udpVideoSender.getFps(),
           udpVideoSender.getPacketsSent(),
           udpVideoSender.getSendErrors());
  httpd_resp_set_type(request, "application/json");
  httpd_resp_set_hdr(request, "Access-Control-Allow-Origin", "*");
  httpd_resp_set_hdr(request, "Cache-Control", "no-store");
  return httpd_resp_send(request, body, HTTPD_RESP_USE_STRLEN);
}

bool initializeCamera() {
  camera_config_t config = {};
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = cameraPinY2;
  config.pin_d1 = cameraPinY3;
  config.pin_d2 = cameraPinY4;
  config.pin_d3 = cameraPinY5;
  config.pin_d4 = cameraPinY6;
  config.pin_d5 = cameraPinY7;
  config.pin_d6 = cameraPinY8;
  config.pin_d7 = cameraPinY9;
  config.pin_xclk = cameraPinXclk;
  config.pin_pclk = cameraPinPclk;
  config.pin_vsync = cameraPinVsync;
  config.pin_href = cameraPinHref;
  config.pin_sscb_sda = cameraPinSiod;
  config.pin_sscb_scl = cameraPinSioc;
  config.pin_pwdn = cameraPinPwdn;
  config.pin_reset = cameraPinReset;
  config.xclk_freq_hz = 20000000;
  config.pixel_format = PIXFORMAT_JPEG;
  config.frame_size = FRAMESIZE_QQVGA;
  config.jpeg_quality = 63;
  config.fb_count = psramFound() ? 2 : 1;
  config.fb_location = psramFound() ? CAMERA_FB_IN_PSRAM : CAMERA_FB_IN_DRAM;
  config.grab_mode = CAMERA_GRAB_LATEST;

  const esp_err_t result = esp_camera_init(&config);
  if (result != ESP_OK) {
    Serial.printf("[Camera] Initialization failed: 0x%x\n", result);
    return false;
  }

  sensor_t *sensor = esp_camera_sensor_get();
  if (sensor != nullptr) {
    Serial.printf("[Camera] Sensor PID: 0x%04x\n", sensor->id.PID);
  }
  Serial.println("[Camera] Initialized at QQVGA JPEG, minimum quality");
  return true;
}

bool startCameraHttpServer() {
  httpd_config_t config = HTTPD_DEFAULT_CONFIG();
  config.server_port = cameraHttpPort;
  config.ctrl_port = cameraHttpPort + 1;

  if (httpd_start(&cameraHttpServer, &config) != ESP_OK) {
    Serial.println("[Camera] Failed to start HTTP server");
    return false;
  }

  httpd_uri_t streamUri = {};
  streamUri.uri = "/stream";
  streamUri.method = HTTP_GET;
  streamUri.handler = streamHandler;
  httpd_register_uri_handler(cameraHttpServer, &streamUri);

  httpd_uri_t healthUri = {};
  healthUri.uri = "/health";
  healthUri.method = HTTP_GET;
  healthUri.handler = healthHandler;
  httpd_register_uri_handler(cameraHttpServer, &healthUri);

  Serial.printf("[Camera] HTTP Fallback Stream: http://%s:%u/stream\n",
                WiFi.softAPIP().toString().c_str(), cameraHttpPort);
  return true;
}
} // namespace

bool isCameraReady() {
  return cameraReady;
}

void iniciarStreamCamara() {
  cameraReady = initializeCamera();

  // 1. Iniciar socket UDP para transmisión directa
  udpVideoSender.begin(UDP_VIDEO_DEFAULT_PORT);

  // 2. Iniciar servidor HTTP en puerto 82 como fallback de comparación
  startCameraHttpServer();

  // 3. Iniciar tarea FreeRTOS para captura y transmisión UDP desacoplada
  if (cameraReady && !udpStreamRunning) {
    udpStreamRunning = true;
    xTaskCreatePinnedToCore(
        cameraUdpStreamTask,
        "CameraUDPTask",
        4096,
        nullptr,
        2, // Prioridad 2
        &udpStreamTaskHandle,
        0  // Core 0 (dejando Core 1 para Wi-Fi y Loop Arduino)
    );
  }
}

void detenerStreamCamara() {
  udpStreamRunning = false;
  udpVideoSender.stop();
  if (cameraHttpServer != nullptr) {
    httpd_stop(cameraHttpServer);
    cameraHttpServer = nullptr;
  }
}
