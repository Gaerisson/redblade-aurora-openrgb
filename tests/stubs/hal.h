#pragma once
#include <stdint.h>
extern uint16_t usb_frame_number;
#define usbGetFrameNumberX(usbp) (usb_frame_number)
