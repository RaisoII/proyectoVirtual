#include "udp_video_sender.h"
#include <string.h>

UdpVideoSender udpVideoSender;

UdpVideoSender::UdpVideoSender()
    : localPort_(UDP_VIDEO_DEFAULT_PORT),
      targetIp_(192, 168, 4, 2), // IP inicial esperada del primer cliente DHCP en SoftAP
      targetPort_(UDP_VIDEO_DEFAULT_PORT),
      targetConfigured_(false),
      isRunning_(false),
      framesSent_(0),
      packetsSent_(0),
      bytesSent_(0),
      sendErrors_(0),
      fpsFrameCounter_(0),
      lastFpsTime_(0),
      currentFps_(0.0f) {
  memset(packetBuffer_, 0, sizeof(packetBuffer_));
}

bool UdpVideoSender::begin(uint16_t localPort) {
  localPort_ = localPort;
  if (udp_.begin(localPort_)) {
    isRunning_ = true;
    lastFpsTime_ = millis();
    Serial.printf("[UdpVideoSender] Servidor UDP listo en puerto %u. Destino inicial: %s:%u\n",
                  localPort_, targetIp_.toString().c_str(), targetPort_);
    return true;
  }
  Serial.printf("[UdpVideoSender] ERROR: No se pudo iniciar el socket UDP en puerto %u\n", localPort_);
  return false;
}

void UdpVideoSender::stop() {
  if (isRunning_) {
    udp_.stop();
    isRunning_ = false;
    Serial.println("[UdpVideoSender] Socket UDP cerrado.");
  }
}

void UdpVideoSender::setTarget(const IPAddress &ip, uint16_t port) {
  targetIp_ = ip;
  targetPort_ = port;
  targetConfigured_ = true;
  Serial.printf("[UdpVideoSender] Destino UDP configurado: %s:%u\n",
                targetIp_.toString().c_str(), targetPort_);
}

void UdpVideoSender::checkIncomingHandshake() {
  if (!isRunning_) return;

  int packetSize = udp_.parsePacket();
  if (packetSize > 0) {
    IPAddress remoteIp = udp_.remoteIP();
    uint16_t remotePort = udp_.remotePort();

    // Si recibimos un paquete del cliente Android (ej. "PING_VR"), bloqueamos su IP/puerto para unicast de alta velocidad
    if (remoteIp != targetIp_ || remotePort != targetPort_ || !targetConfigured_) {
      targetIp_ = remoteIp;
      targetPort_ = UDP_VIDEO_DEFAULT_PORT; // Mantener puerto de escucha acordado
      targetConfigured_ = true;
      Serial.printf("[UdpVideoSender] Cliente detectado y vinculado por ping UDP: %s:%u\n",
                    targetIp_.toString().c_str(), targetPort_);
    }

    // Drenar el paquete recibido
    while (udp_.available()) {
      udp_.read();
    }
  }
}

bool UdpVideoSender::sendFrame(const uint8_t *frameBuffer, size_t frameSize, uint32_t frameId, uint32_t captureTimestampMs) {
  if (!isRunning_ || frameBuffer == nullptr || frameSize == 0) {
    return false;
  }

  // Verificar si hay pings de descubrimiento recibidos
  checkIncomingHandshake();

  const uint16_t totalFragments = (frameSize + UDP_MAX_PAYLOAD_SIZE - 1) / UDP_MAX_PAYLOAD_SIZE;

  for (uint16_t fragmentIndex = 0; fragmentIndex < totalFragments; ++fragmentIndex) {
    const uint32_t offset = fragmentIndex * UDP_MAX_PAYLOAD_SIZE;
    const uint16_t payloadSize = (uint16_t)min((size_t)UDP_MAX_PAYLOAD_SIZE, frameSize - offset);

    UdpPacketHeader header;
    header.magic = UDP_PACKET_MAGIC;
    header.frameId = frameId;
    header.fragmentIndex = fragmentIndex;
    header.totalFragments = totalFragments;
    header.totalFrameSize = (uint32_t)frameSize;
    header.captureTimestampMs = captureTimestampMs;
    header.fragmentOffset = offset;
    header.payloadSize = payloadSize;

    // Construir datagrama sin asignaciones en heap
    memcpy(packetBuffer_, &header, UDP_PACKET_HEADER_SIZE);
    memcpy(packetBuffer_ + UDP_PACKET_HEADER_SIZE, frameBuffer + offset, payloadSize);

    const size_t datagramSize = UDP_PACKET_HEADER_SIZE + payloadSize;

    if (!udp_.beginPacket(targetIp_, targetPort_)) {
      sendErrors_++;
      continue;
    }

    size_t written = udp_.write(packetBuffer_, datagramSize);
    if (written != datagramSize || !udp_.endPacket()) {
      sendErrors_++;
    } else {
      packetsSent_++;
      bytesSent_ += datagramSize;
    }
  }

  framesSent_++;
  fpsFrameCounter_++;

  // Medir FPS transmitidos cada segundo
  unsigned long now = millis();
  if (now - lastFpsTime_ >= 1000) {
    currentFps_ = (fpsFrameCounter_ * 1000.0f) / (now - lastFpsTime_);
    fpsFrameCounter_ = 0;
    lastFpsTime_ = now;
  }

  return true;
}

