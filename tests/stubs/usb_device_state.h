#pragma once
enum { USB_DEVICE_STATE_CONFIGURED,USB_DEVICE_STATE_SUSPEND,USB_DEVICE_STATE_RESET };
struct usb_device_state { uint8_t configure_state; };
extern uint8_t usb_configure_state;
static inline uint8_t usb_device_state_get_configure_state(void) { return usb_configure_state; }
