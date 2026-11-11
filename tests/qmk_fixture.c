#include "qmk_fixture.h"
#include <assert.h>
#include "host.h"
uint8_t mouse_buttons;
unsigned mouse_downs,mouse_reports;
unsigned mouse_moves;
int mouse_x_total;
int8_t mouse_last_x;
bool indicator_pin_high=true;
void gpio_write_pin(uint32_t pin,bool high) { assert(pin==LED_FN_PIN);indicator_pin_high=high; }
void host_mouse_send(report_mouse_t *r) {
    assert(!r->y && !r->v && !r->h);
    assert(r->buttons<=4);if(r->buttons && !mouse_buttons) ++mouse_downs;
    if(r->x){assert(!r->buttons && (r->x==1 || r->x==-1));++mouse_moves;mouse_x_total+=r->x;mouse_last_x=r->x;}
    mouse_buttons=r->buttons;++mouse_reports;
}
led_config_t g_led_config = {.matrix_co = {
#include "led_matrix.inc"
}, .point = {
#include "led_points.inc"
}};
uint8_t mode = RGB_MATRIX_CUSTOM_REDBLADE_DIRECT, speed, hue, saturation, value = 255, mods, weak_mods;
bool enabled = true, key_down[256];
uint16_t base_keymap[6][21];
led_t host_locks;
rgb_t rendered[109];
led_t host_keyboard_led_state(void) { return host_locks; }
void rgb_matrix_set_color(int i,uint8_t r,uint8_t g,uint8_t b) { assert(i>=0 && i<109); rendered[i]=(rgb_t){.r=r,.g=g,.b=b}; }
bool is_key_pressed(uint8_t key) { return key_down[key]; }
uint16_t dynamic_keymap_get_keycode(uint8_t layer, uint8_t row, uint8_t col) { (void)layer; assert(row<6 && col<21); return base_keymap[row][col]; }
uint32_t now, eeprom_user = UINT32_MAX;
unsigned responses, eeprom_writes, keyboard_reports, bootloader_calls, key_down_calls, key_up_calls;
uint32_t timer_read32(void) { return now; }
uint32_t eeconfig_read_user(void) { return eeprom_user; }
void eeconfig_update_user(uint32_t v) { eeprom_user = v; ++eeprom_writes; }
uint8_t get_mods(void) { return mods; }
uint8_t get_weak_mods(void) { return weak_mods; }
uint8_t has_anykey(void) { for(unsigned i=4;i<=0xA4;++i) if(key_down[i]) return 1; return 0; }
void add_weak_mods(uint8_t m) { weak_mods |= m; }
void del_mods(uint8_t m) { mods &= ~m; }
void del_weak_mods(uint8_t m) { weak_mods &= ~m; }
void send_keyboard_report(void) { ++keyboard_reports; }
void register_code(uint8_t code) { key_down[code] = true; if(code>=0xE0 && code<=0xE7) mods|=1<<(code-0xE0); ++key_down_calls; }
void unregister_code(uint8_t code) { key_down[code] = false; if(code>=0xE0 && code<=0xE7) mods&=~(1<<(code-0xE0)); ++key_up_calls; }
void reset_keyboard(void) { ++bootloader_calls; }
bool rgb_matrix_is_enabled(void) { return enabled; }
uint8_t rgb_matrix_get_mode(void) { return mode; }
uint8_t rgb_matrix_get_speed(void) { return speed; }
uint8_t rgb_matrix_get_hue(void) { return hue; }
uint8_t rgb_matrix_get_sat(void) { return saturation; }
uint8_t rgb_matrix_get_val(void) { return value; }
void rgb_matrix_enable_noeeprom(void) { enabled = true; }
void rgb_matrix_disable_noeeprom(void) { enabled = false; }
void rgb_matrix_mode_noeeprom(uint8_t m) { mode = m; }
void rgb_matrix_set_speed_noeeprom(uint8_t s) { speed = s; }
void rgb_matrix_sethsv_noeeprom(uint8_t h, uint8_t s, uint8_t v) { hue=h; saturation=s; value=v; }
void raw_hid_send(uint8_t *data, uint8_t length) { (void)data; assert(length == 32); ++responses; }
