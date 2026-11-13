#pragma once
#include "quantum.h"
// Keymap contents are validated separately; this suite runs actual QMK hooks.
#define LAYOUT_fullsize_extended_iso(...) {{0}}
typedef uint32_t layer_state_t;
extern layer_state_t layer_state;
static inline bool layer_state_cmp(layer_state_t state,uint8_t layer) { return (state & (1UL<<layer))!=0; }
static inline bool layer_state_is(uint8_t layer) { return layer_state_cmp(layer_state,layer); }
void gpio_set_pin_output(uint32_t pin);
void gpio_write_pin(uint32_t pin,bool high);
static inline void gpio_write_pin_high(uint32_t pin) { gpio_write_pin(pin,true); }
static inline void gpio_write_pin_low(uint32_t pin) { gpio_write_pin(pin,false); }
void rgb_matrix_set_suspend_state(bool suspended);
void rgb_matrix_set_color_all(uint8_t r,uint8_t g,uint8_t b);
void rgb_matrix_update_pwm_buffers(void);
