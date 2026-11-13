#pragma once
#include "quantum.h"
extern uint8_t mode, speed, hue, saturation, value, mods, weak_mods;
extern bool enabled, key_down[256];
extern uint16_t base_keymap[6][21];
extern led_t host_locks;
extern rgb_t rendered[109];
extern rgb_t hardwareRGBcommitted[109];
extern unsigned rgb_fast_commits;
void rgb_matrix_set_color_all(uint8_t r,uint8_t g,uint8_t b);
void rgb_matrix_update_pwm_buffers(void);
extern uint32_t now, eeprom_user;
extern unsigned responses, eeprom_writes, keyboard_reports, bootloader_calls, key_down_calls, key_up_calls;
extern uint8_t mouse_buttons;
extern bool indicator_pin_high;
extern unsigned mouse_downs,mouse_reports;
extern unsigned mouse_moves;
extern int mouse_x_total;
extern int8_t mouse_last_x;
bool via_command_kb(uint8_t *data, uint8_t length);
