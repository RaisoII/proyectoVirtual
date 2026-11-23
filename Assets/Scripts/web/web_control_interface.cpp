#include "web_control_interface.h"
#include "web_page.h"

#include <Arduino.h>
#include <WiFi.h>
#include <WebServer.h>
#include <WebSocketsServer.h>

namespace {
constexpr char accessPointName[] = "Proyecto-Autito";
constexpr char accessPointPassword[] = "autito1234";
constexpr uint16_t webSocketPort = 81;

WebServer httpServer(80);
WebSocketsServer webSocketServer(webSocketPort);
ControlRouter *controlRouter = nullptr;

void handleWebSocketEvent(uint8_t clientId, WStype_t eventType,
                          uint8_t *payload, size_t length) {
  switch (eventType) {
    case WStype_CONNECTED:
      Serial.printf("[WebSocket] Cliente #%u conectado\n", clientId);
      break;
    case WStype_DISCONNECTED:
      Serial.printf("[WebSocket] Cliente #%u desconectado\n", clientId);
      break;
    case WStype_TEXT:
      if (controlRouter != nullptr) {
        controlRouter->handleFrame(payload, length);
      }
      break;
    default:
      break;
  }
}
} // namespace

void iniciarInterfazWeb(ControlRouter &router) {
  controlRouter = &router;
  WiFi.mode(WIFI_AP);
  WiFi.softAP(accessPointName, accessPointPassword);
  Serial.printf("[WiFi] AP iniciado: %s (IP: %s)\n", accessPointName, WiFi.softAPIP().toString().c_str());
  httpServer.on("/", HTTP_GET, []() {
    httpServer.send_P(200, "text/html", paginaHTML);
  });
  httpServer.begin();

  webSocketServer.begin();
  webSocketServer.onEvent(handleWebSocketEvent);
  Serial.printf("[WebSocket] Servidor iniciado en puerto %u\n", webSocketPort);
  Serial.println("[HTTP] Servidor iniciado en puerto 80");
}

void actualizarInterfazWeb() {
  httpServer.handleClient();
  webSocketServer.loop();
}
