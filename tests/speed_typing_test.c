// Real cooperative schedulers and renderer; only QMK transport is simulated.
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_rgb.h"
#include "qmk_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
typedef struct { uint8_t iter; } effect_params_t;
#define RGB_MATRIX_EFFECT(name)
#define RGB_MATRIX_CUSTOM_EFFECT_IMPLS
#define RGB_MATRIX_USE_LIMITS(min,max) const uint8_t min=params->iter*8; const uint8_t max=min+8<109?min+8:109
static bool rgb_matrix_check_finished_leds(uint8_t max) { return max<109; }
#include "rgb_matrix_user.inc"
static void render(void) {
    effect_params_t p={0};
    do { REDBLADE_DIRECT(&p);redblade_controls_render(p.iter*8,p.iter*8+8<109?p.iter*8+8:109,false); }
    while(++p.iter<14);
}

static bool edge(uint16_t code,bool down,uint8_t row,uint8_t col) {
    keyrecord_t r={.event={.key={.row=row,.col=col},.pressed=down}};
    return redblade_controls_process(code,&r);
}
static void tap(uint16_t code,uint8_t row,uint8_t col) {
    assert(!edge(code,true,row,col));assert(!edge(code,false,row,col));
}
static void tick(uint32_t dt) { now+=dt;redblade_controls_task(false); }
static void reset(void) {
    redblade_controls_suspend();memset(key_down,0,sizeof(key_down));mods=weak_mods=0;
    now=100;redblade_controls_init();redblade_rgb_init(true);redblade_rgb_defaults();
}
static void record_a(void) {
    tap(RB_REC_ARM,0,17);tap(RB_REC_1,1,1);
    assert(edge(4,true,3,1));register_code(4);now+=800;
    assert(edge(4,false,3,1));unregister_code(4);tap(RB_REC_ARM,0,17);
}
static void persistent_turbo_hold_and_fraction(void) {
    reset();tap(RB_TURBO_E,2,3);
    assert(!edge(RB_TURBO_FASTER,true,2,20));
    tick(399);assert(redblade_turbo_rate()==65);
    tick(1);assert(redblade_turbo_rate()==70);
    for(unsigned i=0;i<30;++i) tick(150);
    assert(redblade_turbo_rate()==250);
    // Fn goes up first: the base-key release must be swallowed, without reset.
    assert(!edge(0x57,false,2,20));tick(1000);assert(redblade_turbo_rate()==250);
    tap(RB_TURBO_SLOWER,1,20);assert(redblade_turbo_rate()==225);
    tap(RB_TURBO_SLOWER,1,20);tap(RB_TURBO_SLOWER,1,20);
    assert(redblade_turbo_rate()==175);
    unsigned downs=key_down_calls;
    for(unsigned i=0;i<4000;++i) tick(1);
    assert(key_down_calls-downs>=699 && key_down_calls-downs<=701);
    for(unsigned i=0;i<3;++i) tap(RB_TURBO_SLOWER,1,20);
    assert(redblade_turbo_rate()==100);
    tap(RB_TURBO_SLOWER,1,20);assert(redblade_turbo_rate()==95);
    // Plain + is a live rate control; its physical release never types.
    tap(0x57,2,20);assert(redblade_turbo_rate()==100 && redblade_turbo_key());
    assert(edge(KC_ESC,true,0,0));assert(!redblade_turbo_key());
    assert(edge(0x57,true,2,20)); // Outside automation it is an ordinary key.
}
static void persistent_macro_hyper_and_reverse(void) {
    reset();record_a();
    assert(edge(0x57,true,2,20)); // Recording/idle must not hijack numpad input.
    tap(RB_REC_1,1,1);
    assert(!edge(RB_TURBO_FASTER,true,2,20));tick(400);
    for(unsigned i=0;i<8;++i) tick(150);
    assert(redblade_recorder_speed_quarters()==0); // Hyper metadata sentinel.
    assert(!edge(0x57,false,2,20));tick(500);
    assert(redblade_recorder_speed_quarters()==0);
    tap(RB_TURBO_SLOWER,1,20);assert(redblade_recorder_speed_quarters()==32);
    tap(RB_TURBO_SLOWER,1,20);assert(redblade_recorder_speed_quarters()==16);
    tap(RB_TURBO_SLOWER,1,20);assert(redblade_recorder_speed_quarters()==8);
    tap(RB_TURBO_SLOWER,1,20);assert(redblade_recorder_speed_quarters()==7);
    for(unsigned i=0;i<10;++i) tap(RB_TURBO_FASTER,2,20);
    tap(RB_REC_1,1,1);tick(0);assert(!key_down[4]);
    tick(1);assert(key_down[4]);tick(0);assert(key_down[4]);
    tick(1);assert(!key_down[4] && !redblade_recorder_active());
    mods=MOD_MASK_SHIFT;tap(RB_REC_1,1,1);mods=0;tick(0);tick(1);
    assert(key_down[4]);tick(1);assert(!key_down[4]);
    tap(RB_TURBO_SLOWER,1,20);tap(RB_TURBO_FASTER,2,20);
    tick(4);assert(!key_down[4]);tick(1);assert(key_down[4]);
    assert(edge(KC_ESC,true,0,0));assert(!key_down[4]);
}
static void white_keys_and_replayed_letters_respect_host_and_off(void) {
    reset();now=6000;render();
    assert(rendered[62].r==64 && rendered[62].g==64);
    assert(edge(4,true,3,1));assert(edge(4,false,3,1));render();
    assert(rendered[62].r==255 && rendered[62].g==0 && rendered[62].b==0);
    assert(rendered[63].r==64 && rendered[63].g==64);
    render();assert(rendered[62].r<255 && rendered[62].r>64 && rendered[62].g>0);
    for(unsigned i=0;i<16;++i) render();
    assert(rendered[62].r==64 && rendered[62].g==64);
    // Record/replay uses the same reactive trail, including Hyper.
    record_a();for(unsigned i=0;i<16;++i) render();
    tap(RB_REC_1,1,1);for(unsigned i=0;i<10;++i) tap(RB_TURBO_FASTER,2,20);
    tick(1);tick(1);render();assert(rendered[62].r==255 && rendered[62].g==0);
    uint8_t stream[32]={7,0x42,62,0,1,85,255,90};assert(via_command_kb(stream,32));
    assert(edge(4,true,3,1));assert(edge(4,false,3,1));render();
    assert(rendered[62].g==90 && rendered[62].r==0);
    now+=REDBLADE_RGB_TIMEOUT_MS;render();
    assert(rendered[62].r==64 && rendered[62].g==64); // No old trail returns.
    assert(edge(4,true,3,1));redblade_rgb_profile(RB_PROFILE_OFF);render();
    for(unsigned i=0;i<109;++i) assert(!rendered[i].r && !rendered[i].g && !rendered[i].b);
    redblade_rgb_profile(RB_PROFILE_WHITE);render();
    assert(rendered[62].r==64 && rendered[62].g==64);
    assert(edge(4,true,3,1));redblade_rgb_suspend();render();
    redblade_rgb_wake();now+=REDBLADE_BOOT_ANIMATION_MS;render();
    assert(rendered[62].r==64 && rendered[62].g==64);
    host_locks=(led_t){.num_lock=true,.caps_lock=true};
    assert(edge(0x5F,true,2,17));render();
    assert(rendered[57].r==255 && rendered[57].g==0); // Numpad digits also react.
    assert(edge(0x39,true,3,0));render();assert(rendered[61].g==128 && !rendered[61].r);
    assert(edge(4,true,3,1));
    for(unsigned i=0;i<16;++i) redblade_rgb_brightness_step(-1);
    render();for(unsigned i=0;i<16;++i) redblade_rgb_brightness_step(1);
    render();assert(rendered[62].r==64 && rendered[62].g==64);
}
static void mouse_turbo_uses_own_report_colors_and_stops(void) {
    reset();base_keymap[0][19]=0xAA;base_keymap[0][20]=0xA9;
    tap(RB_TURBO_SELECT,2,5);
    assert(!edge(RB_PREV,true,0,19) && !mouse_buttons);
    assert(!edge(0xAA,false,0,19));assert(mouse_buttons==1 && redblade_turbo_key()==0xD1);
    hsv_t c;assert(redblade_controls_indicator(18,false,&c) && c.h==32);
    assert(redblade_controls_indicator(RB_LED_FN,false,&c) && c.h==32);
    unsigned downs=mouse_downs;
    for(unsigned i=0;i<1000;++i) tick(1);
    assert(mouse_downs-downs>=59 && mouse_downs-downs<=61);
    tap(RB_TURBO_SELECT,2,5);assert(!mouse_buttons && !redblade_turbo_key());
    tap(RB_TURBO_SELECT,2,5);tap(RB_NEXT,0,20);
    assert(mouse_buttons==2 && redblade_turbo_key()==0xD2);
    assert(redblade_controls_indicator(19,false,&c) && c.h==200);
    assert(redblade_controls_indicator(RB_LED_FN,false,&c) && c.h==200);
    // Same physical volume key stops its click target instead of changing audio.
    unsigned keydowns=key_down_calls;
    assert(!edge(0xA9,true,0,20));assert(!edge(0xA9,false,0,20));
    assert(!mouse_buttons && !redblade_turbo_key() && key_down_calls==keydowns);
    tap(RB_TURBO_SELECT,2,5);tap(RB_PREV,0,19);
    tap(RB_TURBO_E,2,3);assert(!mouse_buttons && redblade_turbo_key()==KC_E);
    tap(RB_TURBO_SELECT,2,5);tap(RB_TURBO_SELECT,2,5);tap(RB_NEXT,0,20);
    assert(mouse_buttons==2);assert(edge(KC_ESC,true,0,0));assert(!mouse_buttons);
    tap(RB_TURBO_SELECT,2,5);tap(RB_PREV,0,19);redblade_controls_suspend();assert(!mouse_buttons);
    reset();assert(edge(0xAA,true,0,19)); // Unselected volume keeps normal behavior.
}
int main(void) {
    persistent_turbo_hold_and_fraction();persistent_macro_hyper_and_reverse();
    white_keys_and_replayed_letters_respect_host_and_off();
    mouse_turbo_uses_own_report_colors_and_stops();
    puts("PASS: latched high speeds, fraction, Hyper loop gap, reactive white, host priority/off, mouse click ownership/colors/stops");
}
