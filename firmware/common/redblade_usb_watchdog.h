// SPDX-License-Identifier: GPL-2.0-or-later
#pragma once
#include <stdbool.h>
#include <stdint.h>
#include "hal.h"
#include "usb_driver.h"
#include "usb_device_state.h"

// Full-speed SOF frame numbers advance even when no RGB application is open.
// Read the existing hardware counter: no new ISR, timer or host heartbeat.
enum { REDBLADE_USB_IDLE_MS = 250 };
typedef struct {
    uint32_t changed_at;
    uint16_t frame;
    bool active;
} redblade_usb_watchdog_t;

static inline uint16_t redblade_usb_frame(void) {
    return usbGetFrameNumberX(&USB_DRIVER);
}

static inline void redblade_usb_watchdog_init(redblade_usb_watchdog_t *watchdog, bool active, uint32_t now) {
    watchdog->changed_at = now;
    watchdog->frame = redblade_usb_frame();
    watchdog->active = active;
}

static inline bool redblade_usb_watchdog_poll(redblade_usb_watchdog_t *watchdog, bool configured, uint32_t now) {
    const uint16_t frame = redblade_usb_frame();
    if (!configured || frame != watchdog->frame) {
        watchdog->frame = frame;
        watchdog->changed_at = now;
    }
    return configured && (uint32_t)(now - watchdog->changed_at) < REDBLADE_USB_IDLE_MS;
}

static inline void redblade_usb_blank(void) {
    // Explicitly clear AND flush all 109 pixels before the suspend loop can
    // stop ordinary rendering. Do not stop the shared key-scan/PWM timer.
    rgb_matrix_set_color_all(0, 0, 0);
    rgb_matrix_update_pwm_buffers();
}
