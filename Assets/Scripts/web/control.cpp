#include "control.h"

#include <Arduino.h>
#include <string.h>

bool ControlRouter::registerDriver(ControlDriver &driver) {
  if (driverCount_ >= maxDrivers || findDriver(driver.id()) != nullptr) {
    return false;
  }

  drivers_[driverCount_++] = &driver;
  return true;
}

bool ControlRouter::handleFrame(const uint8_t *frame, size_t length) {
  if (frame == nullptr || length == 0 || length > maxFrameSize) {
    return false;
  }

  JsonDocument document;
  if (deserializeJson(document, frame, length) != DeserializationError::Ok ||
      !document.is<JsonObject>()) {
    return false;
  }

  const char *type = document["type"] | "";
  if (strcmp(type, "select") == 0) {
    const char *driverId = document["driver"] | "";
    ControlDriver *driver = findDriver(driverId);
    if (driver == nullptr) {
      Serial.printf("[ControlRouter] Driver desconocido: %s\n", driverId);
      return false;
    }

    selectedDriver_ = driver;
    Serial.printf("[ControlRouter] Driver activo seleccionado: %s\n", driverId);
    return true;
  }

  if (strcmp(type, "control") != 0 || selectedDriver_ == nullptr ||
      !document.containsKey("data")) {
    return false;
  }

  return selectedDriver_->process(document["data"].as<JsonVariantConst>());
}

ControlDriver *ControlRouter::findDriver(const char *id) const {
  if (id == nullptr || id[0] == '\0') {
    return nullptr;
  }

  for (size_t index = 0; index < driverCount_; ++index) {
    if (strcmp(drivers_[index]->id(), id) == 0) {
      return drivers_[index];
    }
  }

  return nullptr;
}
