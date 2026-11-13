// SPDX-License-Identifier: GPL-2.0-or-later
#include QMK_KEYBOARD_H
#include "redblade_core.h"
#include "usb_device_state.h"
#include "redblade_usb_watchdog.h"
static bool ready;
static redblade_usb_watchdog_t usb_lighting;
static void usb_lighting_set(bool active) {
    if(active==usb_lighting.active) return;
    usb_lighting.active=active;
    redblade_core_usb(active);rgb_matrix_set_suspend_state(!active);
    if(!active) { gpio_write_pin_high(LED_FN_PIN);redblade_usb_blank(); }
}
const uint16_t PROGMEM keymaps[][MATRIX_ROWS][MATRIX_COLS] = {
    [0] = LAYOUT_fullsize_extended_iso(
        KC_ESC,           KC_F1,   KC_F2,   KC_F3,   KC_F4,   KC_F5,   KC_F6,   KC_F7,   KC_F8,   KC_F9,   KC_F10,  KC_F11,  KC_F12,     KC_PSCR, KC_SCRL, KC_PAUS,    KC_CALC, KC_MUTE, KC_VOLD, KC_VOLU,

        KC_GRV,  KC_1,    KC_2,    KC_3,    KC_4,    KC_5,    KC_6,    KC_7,    KC_8,    KC_9,    KC_0,    KC_MINS, KC_EQL,  KC_BSPC,    KC_INS,  KC_HOME, KC_PGUP,    KC_NUM,  KC_PSLS, KC_PAST, KC_PMNS,
        KC_TAB,  KC_Q,    KC_W,    KC_E,    KC_R,    KC_T,    KC_Y,    KC_U,    KC_I,    KC_O,    KC_P,    KC_LBRC, KC_RBRC,             KC_DEL,  KC_END,  KC_PGDN,    KC_P7,   KC_P8,   KC_P9,   KC_PPLS,
        KC_CAPS, KC_A,    KC_S,    KC_D,    KC_F,    KC_G,    KC_H,    KC_J,    KC_K,    KC_L,    KC_SCLN, KC_QUOT, KC_NUHS, KC_ENT,                                   KC_P4,   KC_P5,   KC_P6,
        KC_LSFT, KC_NUBS, KC_Z,    KC_X,    KC_C,    KC_V,    KC_B,    KC_N,    KC_M,    KC_COMM, KC_DOT,  KC_SLSH,          KC_RSFT,             KC_UP,               KC_P1,   KC_P2,   KC_P3,   KC_PENT,
        KC_LCTL, KC_LGUI, KC_LALT,                            KC_SPC,                             KC_RALT, KC_RGUI, MO(1) ,  KC_RCTL,    KC_LEFT, KC_DOWN, KC_RGHT,    KC_P0,            KC_PDOT
    ),
    [1] = LAYOUT_fullsize_extended_iso(
        RB_CORE_BOOT, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______,
        _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______,
        _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______,
        _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______,
        _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______, _______,
        _______, _______, _______, _______, _______, _______, _______, _______, _______
    ),
};
void keyboard_pre_init_user(void) { gpio_set_pin_output(LED_FN_PIN);gpio_write_pin_high(LED_FN_PIN); }
void keyboard_post_init_user(void) {
    const bool active=usb_device_state_get_configure_state()==USB_DEVICE_STATE_CONFIGURED;
    redblade_usb_watchdog_init(&usb_lighting,active,timer_read32());
    redblade_core_init(active);ready=true;rgb_matrix_set_suspend_state(!active);
    if(!active) redblade_usb_blank();
}
void notify_usb_device_state_change_user(struct usb_device_state state) {
    if(!ready) return;
    const bool configured=state.configure_state==USB_DEVICE_STATE_CONFIGURED;
    usb_lighting_set(redblade_usb_watchdog_poll(&usb_lighting,configured,timer_read32()));
}
void suspend_power_down_user(void) { usb_lighting_set(false);gpio_write_pin_high(LED_FN_PIN); }
void suspend_wakeup_init_user(void) {
    const bool configured=usb_device_state_get_configure_state()==USB_DEVICE_STATE_CONFIGURED;
    usb_lighting_set(redblade_usb_watchdog_poll(&usb_lighting,configured,timer_read32()));
}
bool process_record_user(uint16_t code,keyrecord_t *r) {
    if(!usb_lighting.active) return true;
    return redblade_core_key(code,r);
}
void housekeeping_task_user(void) {
    const bool configured=usb_device_state_get_configure_state()==USB_DEVICE_STATE_CONFIGURED;
    usb_lighting_set(redblade_usb_watchdog_poll(&usb_lighting,configured,timer_read32()));
    if(usb_lighting.active) redblade_core_task(layer_state_is(1));
}
layer_state_t layer_state_set_user(layer_state_t state) {
    // Core has no Windows-lock feature; the physical Windows indicator stays off.
    return state;
}
