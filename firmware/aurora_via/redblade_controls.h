// SPDX-License-Identifier: GPL-2.0-or-later
#pragma once
#include "quantum.h"

// Stable VIA custom keycodes. Keep in sync with fn-shortcuts.json.
enum redblade_keycodes {
    RB_GAME = SAFE_RANGE, RB_PLAY, RB_PREV, RB_NEXT,
    RB_HOST, RB_WHITE, RB_RED, RB_DARK, RB_LIGHT_TOGGLE,
    RB_BRIGHTER, RB_DIMMER, RB_WAVE, RB_ANIMATION,
    RB_MEDIA_FALLBACK, RB_IDLE_FALLBACK, RB_DEFAULTS,
    RB_PROFILE_NEXT, RB_PROFILE_PREV, RB_SAVE, RB_BOOT,
    RB_TURBO_E, RB_TURBO_F, RB_TURBO_SPACE, RB_AUTO_W, RB_TURBO_RATE,
    RB_TURBO_SELECT, RB_TURBO_SLOWER, RB_TURBO_FASTER, RB_GUIDE,
    RB_REC_ARM, RB_REC_1, RB_REC_2, RB_REC_3
};
enum redblade_game_leds { RB_LED_W = 43, RB_LED_E = 44, RB_LED_R = 45, RB_LED_T = 46, RB_LED_F = 65, RB_LED_SPACE = 99, RB_LED_FN = 102, RB_LED_MINUS = 40, RB_LED_PLUS = 60, RB_LED_LGUI = 97, RB_LED_RGUI = 101, RB_LED_CAPS = 61, RB_LED_LSHIFT = 78, RB_LED_RSHIFT = 90, RB_LED_NUM = 37, RB_LED_SCROLL = 14 };

void redblade_controls_init(void);
void redblade_controls_apply_config(void);
bool redblade_controls_process(uint16_t keycode, keyrecord_t *record);
void redblade_controls_task(bool fn_active);
void redblade_controls_suspend(void);
bool redblade_game_mode(void);
uint8_t redblade_turbo_key(void);
uint8_t redblade_turbo_rate(void);
bool redblade_auto_w(void);
bool redblade_turbo_selecting(void);
uint8_t redblade_turbo_led(void);
bool redblade_guide_active(void);
bool redblade_recorder_rate_context(void);
bool redblade_controls_indicators_needed(bool fn_active);
bool redblade_controls_indicator(uint8_t index, bool fn_active, hsv_t *color);
void redblade_controls_render(uint8_t led_min, uint8_t led_max, bool fn_active);
