// SPDX-License-Identifier: GPL-2.0-or-later
#pragma once
#include "quantum.h"

void redblade_rgb_init(bool usb_active);
void redblade_rgb_suspend(void);
void redblade_rgb_wake(void);
void redblade_rgb_begin_frame(void);
hsv_t redblade_rgb_color(uint8_t index);
rgb_t redblade_rgb_convert(hsv_t color);
void redblade_rgb_key_flash(uint8_t index);
void redblade_rgb_replay_key(uint8_t keycode);
bool redblade_rgb_key_lit(uint8_t index);

enum redblade_profile { RB_PROFILE_HOST, RB_PROFILE_WHITE, RB_PROFILE_RED, RB_PROFILE_OFF };
enum redblade_setting { RB_SETTING_ANIMATION = 1, RB_SETTING_MEDIA = 2, RB_SETTING_IDLE_WHITE = 4 };
void redblade_rgb_profile(uint8_t profile);
void redblade_rgb_runtime(uint8_t profile, uint8_t master, uint8_t flags);
void redblade_rgb_apply_config(void);
void redblade_rgb_cycle(int8_t direction);
void redblade_rgb_toggle(void);
void redblade_rgb_brightness_step(int8_t direction);
void redblade_rgb_toggle_setting(uint8_t setting);
void redblade_rgb_defaults(void);
bool redblade_rgb_save(void);
void redblade_rgb_replay(void);
uint8_t redblade_rgb_profile_get(void);
uint8_t redblade_rgb_brightness_get(void);
uint8_t redblade_rgb_settings_get(void);
bool redblade_rgb_output_active(void);
bool redblade_rgb_fallback_active(void);
hsv_t redblade_rgb_apply_brightness(hsv_t color);

