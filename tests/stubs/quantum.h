#pragma once
#include <stdbool.h>
#include <stdint.h>
#define PROGMEM
#define pgm_read_byte(p) (*(const uint8_t *)(p))
#define RGB_MATRIX_LED_COUNT 109
#define RAW_EPSIZE 32
#define RGB_MATRIX_MAXIMUM_BRIGHTNESS 255
#define RGB_MATRIX_CUSTOM_REDBLADE_DIRECT 91
#define RGB_MATRIX_SOLID_COLOR 1
#define SAFE_RANGE 0x7E40
#define QK_MODS 0x0100
#define QK_MODS_MAX 0x1FFF
#define QK_MODS_GET_MODS(kc) (((kc) >> 8) & 0x1F)
#define QK_MODS_GET_BASIC_KEYCODE(kc) ((kc) & 0xFF)
#define KC_LGUI 0xE3
#define KC_E 0x08
#define KC_F 0x09
#define KC_W 0x1A
#define KC_SPC 0x2C
#define KC_ESC 0x29
#define MATRIX_ROWS 6
#define MATRIX_COLS 21
#define LED_FN_PIN 28 // B12 fixture identifier; actual board config supplies B12.
void gpio_write_pin(uint32_t pin,bool high);
#define QK_MOMENTARY 0x5220
#define QK_MOMENTARY_MAX 0x523F
#define KC_RGUI 0xE7
#define KC_MNXT 0xAB
#define KC_MPRV 0xAC
#define KC_MSTP 0xAD
#define KC_MPLY 0xAE
#define MOD_MASK_SHIFT 0x22
#define MOD_MASK_GUI 0x88
#ifndef REDBLADE_VERSION_MAJOR
#    include "../../firmware/aurora_via/config.h"
#endif
#include "../upstream/color.h"
typedef struct { uint8_t col, row; } keypos_t;
typedef struct { bool num_lock, caps_lock, scroll_lock; } led_t;
led_t host_keyboard_led_state(void);
void rgb_matrix_set_color(int index, uint8_t r, uint8_t g, uint8_t b);
typedef struct { struct { keypos_t key; bool pressed; } event; } keyrecord_t;
typedef struct {
    uint8_t matrix_co[6][21];
    struct { uint8_t x, y; } point[109];
    uint8_t flags[109];
} led_config_t;
extern led_config_t g_led_config;
bool rgb_matrix_is_enabled(void);
uint8_t rgb_matrix_get_mode(void);
uint8_t rgb_matrix_get_speed(void);
uint8_t rgb_matrix_get_hue(void);
uint8_t rgb_matrix_get_sat(void);
uint8_t rgb_matrix_get_val(void);
void rgb_matrix_enable_noeeprom(void);
void rgb_matrix_disable_noeeprom(void);
void rgb_matrix_mode_noeeprom(uint8_t mode);
void rgb_matrix_set_speed_noeeprom(uint8_t speed);
void rgb_matrix_sethsv_noeeprom(uint8_t hue, uint8_t sat, uint8_t val);
uint32_t timer_read32(void);
uint32_t eeconfig_read_user(void);
void eeconfig_update_user(uint32_t value);
uint8_t get_mods(void);
uint8_t get_weak_mods(void);
uint8_t has_anykey(void);
void add_weak_mods(uint8_t mods);
void del_mods(uint8_t mods);
void del_weak_mods(uint8_t mods);
void send_keyboard_report(void);
void register_code(uint8_t code);
void unregister_code(uint8_t code);
void reset_keyboard(void);
bool is_key_pressed(uint8_t key);
uint16_t dynamic_keymap_get_keycode(uint8_t layer, uint8_t row, uint8_t col);

