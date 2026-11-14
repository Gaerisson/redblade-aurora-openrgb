#include "qmk_fixture.h"
#include <assert.h>
#include "host.h"
#include "usb_device_state.h"
#ifdef REDBLADE_MODIFIER_DIM
#include "redblade_controls.h"
#endif
uint16_t usb_frame_number;
uint8_t usb_configure_state=USB_DEVICE_STATE_CONFIGURED;
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
    if(r->x){assert(!r->buttons && (r->x==8 || r->x==-8));++mouse_moves;mouse_x_total+=r->x;mouse_last_x=r->x;}
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
rgb_t hardwareRGBcommitted[109];
unsigned rgb_fast_commits;
unsigned rgb_full_flushes;
static bool rgb_flush_required=true;
void sn32f2xx_request_flush(void) { rgb_flush_required=true; }
void rgb_matrix_set_color_all(uint8_t r,uint8_t g,uint8_t b) {
    for(unsigned i=0;i<109;++i) rgb_matrix_set_color(i,r,g,b);
}
void rgb_matrix_update_pwm_buffers(void) {
    if (rgb_flush_required) {
#ifdef REDBLADE_MODIFIER_DIM
        _Static_assert(sizeof(rgb_t)==3,"Packed fixture RGB");
        redblade_controls_copy_frame((uint8_t *)hardwareRGBcommitted,(const uint8_t *)rendered);
#else
        for(unsigned i=0;i<109;++i) hardwareRGBcommitted[i]=rendered[i];
#endif
        rgb_flush_required=false;
    }
    ++rgb_full_flushes;
}
void sn32f2xx_commit_color(int index) { assert(index>=0 && index<109);hardwareRGBcommitted[index]=rendered[index];++rgb_fast_commits; }
led_t host_keyboard_led_state(void) { return host_locks; }
void rgb_matrix_set_color(int i,uint8_t r,uint8_t g,uint8_t b) {
    assert(i>=0 && i<109);
    if(rendered[i].r!=r || rendered[i].g!=g || rendered[i].b!=b) rgb_flush_required=true;
    rendered[i]=(rgb_t){.r=r,.g=g,.b=b};
}
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
