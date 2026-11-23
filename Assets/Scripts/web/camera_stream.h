#ifndef CAMERA_STREAM_H
#define CAMERA_STREAM_H

#include <Arduino.h>

// Inicializa la cámara OV2640, el streaming de ultra-baja latencia UDP
// y el servidor HTTP en puerto 82 (como fallback comparativo).
void iniciarStreamCamara();

// Permite detener la tarea de streaming si fuera necesario
void detenerStreamCamara();

// Devuelve el estado de la cámara
bool isCameraReady();

#endif // CAMERA_STREAM_H

