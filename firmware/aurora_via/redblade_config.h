// SPDX-License-Identifier: GPL-2.0-or-later
#pragma once
#include "quantum.h"

enum redblade_module {
    RB_MODULE_WAVE=1, RB_MODULE_MEDIA=2, RB_MODULE_IDLE=4,
    RB_MODULE_REACTIVE=8, RB_MODULE_FN_FOCUS=16, RB_MODULE_KEY_TURBO=32,
    RB_MODULE_MOUSE_TURBO=64, RB_MODULE_RECORDER=128,
    RB_MODULE_FORWARD=256, RB_MODULE_WIN_LOCK=512
};
enum redblade_effect_color {
    RB_COLOR_WAVE, RB_COLOR_REACTIVE, RB_COLOR_ACCENT, RB_COLOR_MEDIA,
    RB_COLOR_FN_IDLE, RB_COLOR_FN_HELD, RB_COLOR_TURBO, RB_COLOR_FORWARD,
    RB_COLOR_WIN_LOCK, RB_COLOR_FN_GAME, RB_COLOR_MOUSE_LEFT, RB_COLOR_MOUSE_RIGHT,
    RB_COLOR_CAPS_NUM, RB_COLOR_SCROLL, RB_COLOR_RECORDING, RB_COLOR_ONE_SHOT,
    RB_COLOR_LOOP, RB_COLOR_SELECTION, RB_COLOR_LIGHT_HINT, RB_COLOR_GAME_HINT,
    RB_COLOR_TURBO_HINT, RB_COLOR_HOST_HINT, RB_COLOR_ACCENT_HINT, RB_COLOR_OPTION_HINT
};
bool redblade_config_feature(uint16_t feature);
uint8_t redblade_config_byte(uint16_t offset, uint8_t fallback);
hsv_t redblade_config_key_color(uint8_t index, hsv_t fallback);
hsv_t redblade_config_effect(uint8_t effect, hsv_t fallback);
hsv_t redblade_config_effect_scaled(uint8_t effect, hsv_t fallback, uint8_t nominal_peak);
uint32_t redblade_config_timeout(void);
