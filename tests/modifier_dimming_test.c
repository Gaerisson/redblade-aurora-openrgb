// Production journal/configuration + renderer + final output buffer.
#include "qmk_fixture.h"
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static void seed(uint8_t dim,uint8_t highlight,uint8_t layout) {
    uint8_t payload[1696];
    store_fixture_reset();store_fixture_payload(payload);
    payload[32+3]=RB_PROFILE_WHITE;payload[32+9]=layout;
    payload[32+13]=highlight;payload[32+14]=dim;
    store_fixture_seed(0,1,payload);redblade_store_init();
}
static void ranges(void) {
    const uint8_t accepted[]={0,70,100};
    for(unsigned i=0;i<sizeof(accepted);++i) { seed(accepted[i],2,0);assert(redblade_store_valid()); }
    seed(101,2,0);assert(!redblade_store_valid());
    seed(255,2,0);assert(!redblade_store_valid());
}
static void setup(uint8_t dim,uint8_t highlight,uint8_t layout) {
    seed(dim,highlight,layout);assert(redblade_store_valid());
    memset(key_down,0,sizeof(key_down));mods=weak_mods=0;
    now=100;enabled=true;value=255;mode=RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
    redblade_controls_init();redblade_rgb_init(true);
    now+=REDBLADE_BOOT_ANIMATION_MS;redblade_rgb_begin_frame();
    rgb_matrix_set_color_all(100,200,50);
}
static void overlay(bool fn) {
    for(uint8_t i=0;i<109;i+=8) redblade_controls_render(i,i+8<109?i+8:109,fn);
    rgb_matrix_update_pwm_buffers();
}
static void pixel(uint8_t index,uint8_t r,uint8_t g,uint8_t b) {
    const rgb_t c=hardwareRGBcommitted[index];assert(c.r==r && c.g==g && c.b==b);
}
static void dim_background(void) {
    setup(70,2,0);mods=1;overlay(false);
    pixel(10,30,60,15);
    // Source colors remain intact, so repeated static/native flushes cannot
    // compound the dim or lose the selected background on modifier release.
    assert(rendered[10].r==100 && rendered[10].g==200 && rendered[10].b==50);
    for(unsigned i=0;i<20;++i) { overlay(false);pixel(10,30,60,15); }
    mods=0;overlay(false);pixel(10,100,200,50);
}
static void same_as_raw(uint8_t index) {
    const rgb_t raw=rendered[index];pixel(index,raw.r,raw.g,raw.b);
    assert(raw.r || raw.g || raw.b);
}
static void modifier_combinations(void) {
    const uint8_t held[]={1,0x10,2,0x20,4,0x40,0x77};
    const uint8_t keys[]={96,103,78,90,98,100,96};
    for(unsigned i=0;i<sizeof(held);++i) {
        setup(70,2,0);mods=held[i];overlay(false);
        pixel(10,30,60,15);same_as_raw(keys[i]);
    }
    setup(70,2,0);mods=0x77;overlay(false);
    same_as_raw(42);same_as_raw(82);same_as_raw(41);same_as_raw(4);
    same_as_raw(96);same_as_raw(103);same_as_raw(78);same_as_raw(90);same_as_raw(98);same_as_raw(100);
    // Useful-key sets follow physical AZERTY/QWERTY positions.
    setup(70,2,0);mods=1;overlay(false);same_as_raw(42);pixel(62,30,60,15);
    setup(70,2,1);mods=1;overlay(false);same_as_raw(62);pixel(42,30,60,15);
    setup(70,2,0);weak_mods=0x77;overlay(false);pixel(10,100,200,50);
    mods=8;overlay(false);pixel(10,100,200,50); // Windows alone is not a highlight trigger.
}
static void modes_and_strength(void) {
    setup(0,2,0);mods=1;overlay(false);pixel(10,100,200,50);
    setup(70,0,0);mods=1;overlay(false);pixel(10,100,200,50);
    setup(1,2,0);mods=1;overlay(false);pixel(10,99,198,49);
    setup(99,2,0);mods=1;overlay(false);pixel(10,1,2,0);
    setup(100,2,0);mods=1;overlay(false);pixel(10,0,0,0);same_as_raw(96);
    setup(70,1,0);mods=1;overlay(false);pixel(10,30,60,15);
    redblade_rgb_profile(RB_PROFILE_HOST);
    uint8_t packet[32]={7,0x42,42,0,1,50,255,100};assert(via_command_kb(packet,32));
    redblade_rgb_begin_frame();overlay(false);pixel(10,100,200,50);
    setup(70,2,0);redblade_rgb_profile(RB_PROFILE_HOST);
    assert(via_command_kb(packet,32));redblade_rgb_begin_frame();mods=1;
    overlay(false);pixel(10,30,60,15);
    // Native static effects use the same final-output transform.
    mode=RGB_MATRIX_SOLID_COLOR;overlay(false);pixel(10,30,60,15);
    for(unsigned i=0;i<10;++i) { overlay(false);pixel(10,30,60,15); }
    mods=0;overlay(false);pixel(10,100,200,50);
    setup(70,2,0);mods=1;overlay(true);same_as_raw(1);pixel(10,0,0,0);
    setup(70,2,0);mods=1;overlay(false);redblade_rgb_profile(RB_PROFILE_OFF);
    rgb_matrix_set_color_all(0,0,0);overlay(false);pixel(10,0,0,0);pixel(96,0,0,0);
    setup(70,2,0);mods=1;overlay(false);redblade_controls_suspend();redblade_rgb_suspend();
    rgb_matrix_set_color_all(0,0,0);overlay(false);pixel(10,0,0,0);pixel(96,0,0,0);
}
static void tap(uint16_t code) {
    keyrecord_t key={.event={.key={.row=2,.col=3},.pressed=true}};
    assert(!redblade_controls_process(code,&key));key.event.pressed=false;
    assert(!redblade_controls_process(code,&key));
}
static void priorities(void) {
    setup(70,2,0);tap(RB_TURBO_E);mods=1;overlay(false);
    same_as_raw(RB_LED_E);same_as_raw(RB_LED_FN);pixel(10,30,60,15);
    now+=10;redblade_controls_task(false);overlay(false);
    pixel(RB_LED_E,0,0,0);pixel(RB_LED_FN,0,0,0);pixel(10,30,60,15);
    setup(70,2,0);tap(RB_GAME);mods=1;overlay(false);
    same_as_raw(RB_LED_LGUI);same_as_raw(RB_LED_RGUI);pixel(10,30,60,15);
    setup(70,2,0);redblade_recorder_arm();mods=1;overlay(false);
    same_as_raw(16);same_as_raw(21);same_as_raw(RB_LED_FN);pixel(10,30,60,15);
    setup(70,2,0);host_locks=(led_t){.caps_lock=1,.num_lock=1,.scroll_lock=1};mods=1;overlay(false);
    same_as_raw(RB_LED_CAPS);same_as_raw(RB_LED_NUM);same_as_raw(RB_LED_SCROLL);
    // Numpad background must dim even while its active Num indicator is kept.
    const rgb_t raw=rendered[57];pixel(57,raw.r*30/100,raw.g*30/100,raw.b*30/100);
    host_locks=(led_t){0};mods=weak_mods=0;
}
int main(void) {
    ranges();dim_background();modifier_combinations();modes_and_strength();priorities();
    puts("PASS: modifier dim range, combinations/layouts, exact output, static noncompounding/release, host/Fn/Off/USB gates and priority cues");
}
