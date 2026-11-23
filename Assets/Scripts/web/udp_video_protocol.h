#ifndef UDP_VIDEO_PROTOCOL_H
#define UDP_VIDEO_PROTOCOL_H

#include <stdint.h>
#include <stddef.h>

// Identificador mágico para validar datagramas de video: 'V' (0x56) y 'R' (0x52)
constexpr uint16_t UDP_PACKET_MAGIC = 0x5256;

// Tamaño objetivo máximo por datagrama UDP para no exceder la MTU (1500 Wi-Fi)
// y prevenir por completo la fragmentación IP en la capa de red.
constexpr size_t UDP_TARGET_PACKET_SIZE = 1200;

// Puerto UDP predeterminado para el streaming de video
constexpr uint16_t UDP_VIDEO_DEFAULT_PORT = 8282;

#pragma pack(push, 1)
struct UdpPacketHeader {
    uint16_t magic;              // Número mágico de validación (0x5256)
    uint32_t frameId;            // Identificador correlativo del frame capturado
    uint16_t fragmentIndex;      // Índice del fragmento actual (0-indexed)
    uint16_t totalFragments;     // Cantidad total de fragmentos que componen el frame
    uint32_t totalFrameSize;     // Tamaño total en bytes del frame JPEG completo
    uint32_t captureTimestampMs; // Timestamp de captura (millis() en la ESP32)
    uint32_t fragmentOffset;     // Desplazamiento en bytes del fragmento dentro del frame
    uint16_t payloadSize;        // Cantidad de bytes de datos JPEG en este datagrama
};
#pragma pack(pop)

constexpr size_t UDP_PACKET_HEADER_SIZE = sizeof(UdpPacketHeader); // 24 bytes
constexpr size_t UDP_MAX_PAYLOAD_SIZE = UDP_TARGET_PACKET_SIZE - UDP_PACKET_HEADER_SIZE; // 1176 bytes

#endif // UDP_VIDEO_PROTOCOL_H

