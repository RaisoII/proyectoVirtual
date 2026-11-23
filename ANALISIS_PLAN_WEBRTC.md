# Auditoría y Análisis de Viabilidad: Streaming de Video WebRTC en ESP32-CAM y Unity/Android

**Documento base analizado:** `plan`  
**Fecha:** 21 de noviembre de 2026  
**Estado:** Etapa 1 (Auditoría Técnica) y Evaluación Preliminar de Arquitectura (Etapa 2) — *Fase previa a implementación*

---

## 1. Resumen Ejecutivo y Contexto Actual

El objetivo del plan es migrar el sistema de streaming de video desde HTTP/MJPEG sobre TCP (puerto 82) hacia WebRTC, persiguiendo la menor latencia extremo a extremo posible entre la cámara (ESP32-CAM) y el visor de Realidad Virtual (Unity en Android).

### 1.1. Arquitectura actual en el repositorio
* **ESP32-CAM (Firmware en `Assets/Scripts/web/`):**
  * **Captura:** Sensor OV2640 configurado con `PIXFORMAT_JPEG`, `CAMERA_GRAB_LATEST`, resolución `FRAMESIZE_QQVGA` (160x120) y calidad JPEG `63` (mínimo peso de frame).
  * **Almacenamiento:** Doble buffer en PSRAM externa (`config.fb_count = psramFound() ? 2 : 1; config.fb_location = CAMERA_FB_IN_PSRAM;`).
  * **Servidor HTTP:** Implementado con `esp_http_server` en el puerto 82 (`/stream`), sirviendo mediante `multipart/x-mixed-replace`.
  * **Red y Control:** ESP32 opera como punto de acceso Wi-Fi autónomo (`Proyecto-Autito`, IP `192.168.4.1`). Servidor Web en puerto 80 y WebSocket de control vehicular en puerto 81 (`Assets/Scripts/web/web_control_interface.cpp`).
* **Receptor Unity / Android (`Assets/Scripts/CameraReceiver.cs`):**
  * Hilo secundario dedicado (`LowLatency_MJPEG_Receiver`) conectado vía socket TCP (`TcpClient` con `NoDelay = true`).
  * Búsqueda manual de cabeceras JPEG SOI (`0xFF 0xD8`) y delimitadores EOI (`0xFF 0xD9`).
  * Descarte de frames atrasados en memoria intermedia mediante `latestFrame`.
  * Carga y decodificación en el hilo principal (`Update`) usando `Texture2D.LoadImage()`.
  * Cálculo reactivo de luz ambiental en base a los píxeles del frame recibido.

---

## 2. Respuestas a la Investigación Técnica Obligatoria (Punto 3 del Plan)

### 1. Modelo exacto de ESP32 y recursos disponibles
* **Modelo:** AI-Thinker ESP32-CAM basado en el SoC **ESP32-D0WDQ6-V3** (arquitectura Tensilica Xtensa Dual-Core 32-bit LX6 @ 160/240 MHz).
* **Memoria Interna (SRAM):** 520 KB totales, de los cuales quedan utilizables aproximadamente 280–320 KB para heap libre (compartido con la pila Wi-Fi y tareas FreeRTOS).
* **Memoria Externa (PSRAM):** 4 MB conectados por bus SPI a ~40 MHz.
* **Periféricos de Video:** Bus DVP paralelo de 8 bits dedicado a la cámara.
* **Limitación física crítica:** La velocidad del bus SPI de la PSRAM y el ancho de banda del subsistema de memoria imponen un cuello de botella si se realizan múltiples copias de buffers en RAM.

### 2. Framework y versión utilizada
* **Framework:** Arduino Core para ESP32 (compilado sobre FreeRTOS y ESP-IDF subyacente).
* **Dependencias actuales:** `esp_camera.h`, `esp_http_server.h`, `WiFi.h`, `WebServer.h`, `WebSocketsServer.h`.

### 3. Implementaciones WebRTC compatibles con ESP32
* No existe soporte oficial completo de WebRTC (audio, video, DataChannel) dentro del core oficial de Arduino-ESP32.
* Existen implementaciones en C orientadas a sistemas embebidos:
  * **`libpeer`** (desarrollado para microcontroladores y dispositivos IoT en C puro, compatible con ESP-IDF / Arduino).
  * **`esp-webrtc`** de Espressif (enfocado principalmente en ESP32-S3 con DSP y transporte de audio bidireccional SIP/WebRTC).
  * **`libdatachannel`** (diseñado en C++, pero con requerimientos de memoria y dependencias que suelen exceder los límites del ESP32 clásico salvo compilaciones muy recortadas).

### 4. ¿JPEG directo o transcodificación a VP8 / H.264?
* **El gran desafío de WebRTC estándar:** Las especificaciones oficiales de WebRTC (RFC 7742 / W3C) **no contemplan JPEG** dentro de los tracks de video estándar (`MediaStreamTrack`). Exigen obligatoriamente soporte de **VP8** o **H.264 Constrained Baseline**.
* **Capacidad de cómputo del ESP32:** El sensor OV2640 entrega los cuadros comprimidos en **JPEG por hardware**. El ESP32 clásico **no posee codificador H.264 o VP8 por hardware**.
* Codificar H.264 o VP8 por software en los 240 MHz del LX6 es inviable para video en tiempo real (rendimiento esperado menor a 1 FPS con 100% de saturación de CPU).
* **Consecuencia técnica directa:** En un ESP32-CAM, una conexión WebRTC estándar **no puede enviar video mediante un track de video RTP normal**. La única forma viable de transportar JPEG sobre WebRTC en este hardware es a través de un canal binario **`RTCDataChannel`** (sobre SCTP / DTLS / UDP), empaquetando cada cuadro JPEG capturado directamente del DMA.

### 5. Consumo de RAM, PSRAM, CPU y ancho de banda
* **Handshake DTLS (Criptografía):** WebRTC exige cifrado DTLS en todos sus canales. El cálculo de intercambio de claves (ECDHE / RSA + AES-GCM) exige picos del 80-100% de CPU en un núcleo durante 1 a 3 segundos en el establecimiento de conexión, consumiendo entre 35 y 60 KB de SRAM interna continua.
* **Pila SCTP / DataChannel:** Requiere buffers de fragmentación y reensamblado (overhead adicional de ~20-30 KB RAM).
* **Ancho de banda Wi-Fi:** Un flujo QQVGA/QVGA JPEG a 15–25 FPS consume entre 200 Kbps y 1.2 Mbps, lo cual está dentro de los límites del radio 802.11b/g/n del ESP32.

### 6. Negociación SDP e Intercambio ICE
* Al no haber conexión a Internet, la negociación inicial se realiza de forma local:
  * Unity (Android) genera la oferta SDP (`offer`).
  * Android envía la oferta mediante una petición HTTP POST local (ej. `http://192.168.4.1/api/webrtc/offer`) o a través de un canal WebSocket dedicado.
  * La ESP32 procesa la oferta, configura sus sockets locales y responde con el SDP `answer`.
  * Los candidatos ICE se resuelven en la misma negociación o mediante trickling por HTTP/WebSocket.

### 7. Señalización sin Internet en modo Access Point
* Totalmente resuelta dentro de la LAN autónoma:
  * Como el ESP32 es el AP (`192.168.4.1`) y Android es el cliente (`192.168.4.2`), ambos dispositivos tienen visibilidad directa IP. No se requiere ningún servidor de señalización externo.

### 8. Necesidad de servidores STUN / TURN
* **Completamente innecesarios.**
* STUN sirve para atravesar NATs públicos y TURN para relay cuando hay firewalls restrictivos.
* Al encontrarse ambos dispositivos en el mismo segmento de red (`192.168.4.0/24`), los candidatos ICE son de tipo **`host`**. La conexión peer-to-peer se establece directamente entre las direcciones IP privadas locales.

---

## 3. Análisis Comparativo de Alternativas Técnicas

Dado que el ESP32 no puede codificar H.264/VP8 en tiempo real, evaluamos las dos rutas técnicas reales para lograr el objetivo de **ultra-baja latencia sobre UDP**:

```
+--------------------------------------------------------------------------------------------------+
| COMPARATIVA: ARQUITECTURA ACTUAL VS. WEBRTC DATACHANNEL VS. UDP/RTP DIRECTO                     |
+-----------------------------+-----------------------+---------------------+----------------------+
| Criterio                    | Actual: HTTP / MJPEG  | WebRTC DataChannel  | UDP / RTP Ligero     |
+-----------------------------+-----------------------+---------------------+----------------------+
| Protocolo de transporte     | TCP (Stream continuo) | UDP (DTLS + SCTP)   | UDP puro / RTP JPEG  |
| Recuperación de pérdidas    | Retransmisión TCP     | Descarte inmediato  | Descarte inmediato   |
| Congestión / Head-of-Line   | Sí (acumula frames)   | No (cero buffering) | No (cero buffering)  |
| Carga de CPU en ESP32       | Baja (~15-25%)        | Alta (Handshake)    | Mínima (~10-15%)     |
| Overhead de Criptografía    | Ninguno               | Alto (DTLS/SRTP)    | Ninguno              |
| Dependencias en Unity       | Cero (Nativo C#)      | Paquete WebRTC/JNI  | Cero (UdpClient C#)  |
| Soporte de JPEG nativo      | Sí                    | Como binario crudo  | Sí (fragmentado)     |
| Complejidad de integración  | Muy baja              | Muy alta            | Baja                 |
| Latencia esperada           | 120 - 350 ms          | 30 - 60 ms          | 20 - 50 ms           |
+-----------------------------+-----------------------+---------------------+----------------------+
```

### Hallazgo Clave:
La latencia actual del sistema no proviene del formato JPEG (que se comprime en microsegundos dentro del sensor OV2640), sino del **mecanismo de transporte TCP** (retransmisiones, buffer de socket y congestión de paquetes en la capa de transporte). 

Si se adopta WebRTC, **debe ser mediante `RTCDataChannel` binario no confiable (`ordered = false`, `maxRetransmits = 0`)**. Sin embargo, un protocolo **UDP/RTP ligero** consigue el mismo objetivo exacto de transmisión sin cola ni retransmisión, ahorrando la pesada carga criptográfica en la ESP32.

---

## 4. Arquitectura Propuesta (Etapa 2 del Plan)

Para cumplir con la solicitud del usuario manteniendo la modularidad y compatibilidad total con el sistema de control del vehículo, se plantea la siguiente arquitectura desacoplada:

```
+---------------------------------------------------------------------------------------+
|                                    ESP32-CAM                                          |
|                                                                                       |
|  [ Sensor OV2640 ] ---> Captura JPEG por Hardware (DMA en PSRAM)                      |
|                                  |                                                    |
|                                  v                                                    |
|  [ FreeRTOS Task: Video ] -------> Módulo de Transporte de Video                     |
|                                     |-- Opción A: WebRTC DataChannel (libpeer/UDP)    |
|                                     |-- Opción B: Fallback MJPEG (Puerto 82 TCP)      |
|                                                                                       |
|  [ FreeRTOS Task: Control ] -----> Servidor WebSocket (Puerto 81) - SIN CAMBIOS       |
|                                    (Driver Joystick / Giroscopio / WASD)              |
+---------------------------------------------------------------------------------------+
                                           |
                                  Wi-Fi Local (AP)
                                           |
+---------------------------------------------------------------------------------------+
|                                  UNITY / ANDROID                                      |
|                                                                                       |
|  [ Receptor de Control ] --------> WebSocket Client (Puerto 81)                       |
|                                                                                       |
|  [ Receptor de Video ] ----------> WebRTC Receiver / UDP Listener                     |
|                                    - Descarte inmediato de paquetes desfasados        |
|                                    - Cero buffering de frames anteriores              |
|                                    - Inyección a Texture2D en pantalla                |
|                                                                                       |
|  [ Instrumentación ] ------------> Métricas de Latencia E2E y FPS reales              |
+---------------------------------------------------------------------------------------+
```

---

## 5. Matriz de Cumplimiento de Restricciones del Plan

* **No reemplazar componentes sin justificarlo:** El sensor OV2640 y el ESP32 se mantienen; se aprovecha la compresión JPEG por hardware ya existente.
* **No modificar el sistema de control del vehículo:** El archivo `web_control_interface.cpp` y el servidor WebSocket del puerto 81 permanecen totalmente aislados e intactos.
* **No agregar dependencias innecesarias:** Se evita importar pilas completas si se opta por la solución más liviana, y en caso de WebRTC se acota al stack embebido mínimo en C (`libpeer`).
* **No inventar compatibilidad de bibliotecas:** Se explicita que WebRTC estándar de video no admite JPEG y que el ESP32 no puede codificar H.264 por software.
* **Mantener streaming MJPEG durante pruebas:** El puerto 82 (`/stream`) se mantendrá operativo en paralelo como método de contingencia y referencia comparativa.

---

## 6. Próximos Pasos (Pendientes de Aprobación)

De acuerdo con la indicación final de la línea 136 del `plan`, **no se aplicarán modificaciones en el código** hasta recibir la confirmación de la ruta a seguir:

1. **Decisión de Transporte:**
   * **Ruta 1 (WebRTC DataChannel):** Integrar `libpeer` en ESP32 para abrir un DataChannel binario hacia Unity WebRTC.
   * **Ruta 2 (Protocolo UDP/RTP Directo):** Transmitir los paquetes JPEG en datagramas UDP con descarte inmediato (mismo beneficio de WebRTC, sin el consumo de memoria ni CPU de DTLS).
2. **Definición del Sistema de Instrumentación:** Implementar timestamp incrustado en el encabezado del paquete de video para calcular la latencia real extremo a extremo en milisegundos desde Unity.

