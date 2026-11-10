// SPDX-License-Identifier: GPL-2.0-or-later
#pragma once
#include "quantum.h"

enum redblade_recorder_state { RB_REC_IDLE, RB_REC_ARMED, RB_REC_RECORDING, RB_REC_PLAYING, RB_REC_FEEDBACK, RB_REC_WAITING };
enum redblade_recorder_error { RB_REC_OK, RB_REC_EMPTY, RB_REC_BUSY, RB_REC_GUI_LOCKED, RB_REC_FULL, RB_REC_TIMEOUT, RB_REC_INVALID };
void redblade_recorder_init(void);
void redblade_recorder_suspend(void);
void redblade_recorder_arm(void);
void redblade_recorder_slot(uint8_t slot, bool gui_locked, bool repeat);
void redblade_recorder_key(uint16_t keycode, bool pressed);
void redblade_recorder_task(bool fn_active);
void redblade_recorder_stop(bool save_recording);
void redblade_recorder_speed_step(int8_t direction);
void redblade_recorder_speed_reset(void);
void redblade_recorder_speed_configure(uint8_t index);
uint8_t redblade_recorder_state(void);
uint8_t redblade_recorder_selected(void);
uint8_t redblade_recorder_speed_quarters(void);
uint8_t redblade_recorder_error(void);
uint16_t redblade_recorder_slot_bytes(uint8_t slot);
uint16_t redblade_recorder_used(void);
bool redblade_recorder_active(void);
bool redblade_recorder_loop(void);
bool redblade_recorder_indicator(uint8_t index, bool fn_active, hsv_t *color);
