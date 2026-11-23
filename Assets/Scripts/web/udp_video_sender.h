#ifndef UDP_VIDEO_SENDER_H
#define UDP_VIDEO_SENDER_H

#include <Arduino.h>
#include <WiFiUdp.h>
#include "udp_video_protocol.h"

class UdpVideoSender {
public:
    UdpVideoSender();

    bool begin(uint16_t localPort = UDP_VIDEO_DEFAULT_PORT);
    void stop();

    void setTarget(const IPAddress &ip, uint16_t port = UDP_VIDEO_DEFAULT_PORT);
    void checkIncomingHandshake();

    bool sendFrame(const uint8_t *frameBuffer, size_t frameSize, uint32_t frameId, uint32_t captureTimestampMs);

    // Métricas en tiempo real
    uint32_t getFramesSent() const { return framesSent_; }
    uint32_t getPacketsSent() const { return packetsSent_; }
    uint32_t getBytesSent() const { return bytesSent_; }
    uint32_t getSendErrors() const { return sendErrors_; }
    float getFps() const { return currentFps_; }

private:
    WiFiUDP udp_;
    uint16_t localPort_;
    IPAddress targetIp_;
    uint16_t targetPort_;
    bool targetConfigured_;
    bool isRunning_;

    // Buffer estático para componer datagramas (cero fragmentación en heap)
    uint8_t packetBuffer_[UDP_TARGET_PACKET_SIZE];

    // Contadores de métricas
    uint32_t framesSent_;
    uint32_t packetsSent_;
    uint32_t bytesSent_;
    uint32_t sendErrors_;
    
    // Cálculo de FPS
    uint32_t fpsFrameCounter_;
    unsigned long lastFpsTime_;
    float currentFps_;
};

extern UdpVideoSender udpVideoSender;

#endif // UDP_VIDEO_SENDER_H

