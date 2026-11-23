#include <Arduino.h>
#include "camera_stream.h"
#include "control.h"
#include "driver_car_actions.h"
#include "driver_control_giroscope.h"
#include "driver_control_joystick.h"
#include "driver_control_keys.h"
#include "web_control_interface.h"
#include "driver_car.h"

namespace {
DriverCarActions carActions;
GyroscopeDriver gyroscopeDriver(carActions);
JoystickDriver joystickDriver(carActions);
KeysDriver wasdDriver(carActions);
ControlRouter controlRouter;
} // namespace

void setup() {
  Serial.begin(115200);
  controlRouter.registerDriver(gyroscopeDriver);
  controlRouter.registerDriver(joystickDriver);
  controlRouter.registerDriver(wasdDriver);
  iniciarInterfazWeb(controlRouter);
  iniciarStreamCamara();
  // car_init();
}

void loop() {
  actualizarInterfazWeb();
  // accelerate(1);
  // update_motors();
}
