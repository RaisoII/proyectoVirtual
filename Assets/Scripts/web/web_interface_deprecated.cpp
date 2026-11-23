#include <Arduino.h>
#include <WiFi.h>
#include <WebServer.h>
#include <WebSocketsServer.h>
#include "control_deprecated.h"
#include "web_interface_deprecated.h"
#include "web_page_deprecated.h"

namespace {
  constexpr char WIFI_SSID[] = "Auto_ESP32";
  constexpr char WIFI_PASSWORD[] = "12345678";
  constexpr int HTTP_PORT = 80;
  constexpr int WEBSOCKET_PORT = 81;

  WebServer server(HTTP_PORT);
  WebSocketsServer webSocket(WEBSOCKET_PORT);
  int clienteControlActivo = -1;

  void eventoWebSocket(uint8_t cliente, WStype_t type, uint8_t* payload, size_t length) {
    if (type == WStype_CONNECTED) {
      if (clienteControlActivo == -1) {
        clienteControlActivo = cliente;
        Serial.printf("Cliente %u obtuvo el control\n", cliente);
      } else if (cliente != clienteControlActivo) {
        Serial.printf("Cliente %u rechazado: ya hay un controlador activo\n", cliente);
        webSocket.disconnect(cliente);
      }
      return;
    }

    if (type == WStype_DISCONNECTED) {
      if (cliente == clienteControlActivo) {
        clienteControlActivo = -1;
        procesarMovimiento(0, 0);
        Serial.println("Controlador desconectado; se libero el control");
      }
      return;
    }

    if (cliente != clienteControlActivo) {
      return;
    }

    if (type != WStype_TEXT || payload == nullptr || length == 0) {
      return;
    }

    String mensaje;
    mensaje.reserve(length);
    mensaje.concat(reinterpret_cast<const char*>(payload), length);

    const int coma = mensaje.indexOf(',');
    if (coma > 0) {
      const int beta = mensaje.substring(0, coma).toInt();
      const int gamma = mensaje.substring(coma + 1).toInt();
      procesarMovimiento(beta, gamma);
    }
  }
}

void iniciarInterfazWeb() {
  WiFi.softAP(WIFI_SSID, WIFI_PASSWORD);
  Serial.print("IP del servidor: ");
  Serial.println(WiFi.softAPIP());

  server.on("/", []() {
    server.send_P(200, "text/html", paginaHTML);
  });
  server.begin();

  webSocket.begin();
  webSocket.onEvent(eventoWebSocket);
}

void actualizarInterfazWeb() {
  server.handleClient();
  webSocket.loop();
}
