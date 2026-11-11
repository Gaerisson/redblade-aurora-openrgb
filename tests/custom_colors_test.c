// Exercise every effect-color slot through the actual renderer/indicator paths.
#include "qmk_fixture.h"
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>

static void color(uint8_t index,bool fn,uint8_t h,uint8_t s) {
    hsv_t c;assert(redblade_controls_indicator(index,fn,&c));
    assert(c.h==h && c.s==s && c.v>0);
}
static void press(uint16_t code) {
    keyrecord_t r={.event={.key={.row=2,.col=3},.pressed=true}};
    assert(!redblade_controls_process(code,&r));r.event.pressed=false;assert(!redblade_controls_process(code,&r));
}
static void click_target(uint8_t col) {
    press(RB_TURBO_SELECT);
    base_keymap[0][col]=col==19 ? 0xAA : 0xA9;
    keyrecord_t r={.event={.key={.row=0,.col=col},.pressed=true}};
    assert(!redblade_controls_process(base_keymap[0][col],&r));r.event.pressed=false;
    assert(!redblade_controls_process(base_keymap[0][col],&r));
}
int main(void) {
    uint8_t payload[1696];store_fixture_reset();store_fixture_payload(payload);
    payload[32+3]=1;
    for(unsigned i=0;i<24;++i) { payload[32+343+i*3]=7*i;payload[32+343+i*3+1]=200;payload[32+343+i*3+2]=120; }
    payload[32+16+61*3]=80;payload[32+16+61*3+1]=255;payload[32+16+61*3+2]=100;
    payload[32+16+57*3]=140;payload[32+16+57*3+1]=150;payload[32+16+57*3+2]=200;
    store_fixture_seed(0,1,payload);redblade_store_init();redblade_controls_init();redblade_rgb_init(true);
    now=3000;redblade_rgb_begin_frame();

    redblade_rgb_replay();now+=500;redblade_rgb_begin_frame();
    hsv_t c=redblade_rgb_color(44);assert(c.h==0 && c.s==200); // Wave0.
    redblade_rgb_profile(RB_PROFILE_WHITE);redblade_rgb_key_flash(61);redblade_rgb_begin_frame();
    c=redblade_rgb_color(61);assert(c.h==7 && c.s==200 && c.v==120); // Reactive1.
    redblade_rgb_profile(RB_PROFILE_RED);redblade_rgb_begin_frame();
    c=redblade_rgb_color(0);assert(c.h==14 && c.s==200 && c.v==120); // Accent2.
    redblade_rgb_profile(RB_PROFILE_HOST);
    uint8_t black[32]={7,0x42,16,0,1};assert(via_command_kb(black,32));redblade_rgb_begin_frame();
    c=redblade_rgb_color(16);assert(c.h==21 && c.s==200 && c.v==120); // Media3.
    redblade_rgb_profile(RB_PROFILE_WHITE);
    color(RB_LED_FN,false,28,200);color(RB_LED_FN,true,35,200); // Idle4/held5.
    press(RB_TURBO_E);color(RB_LED_FN,false,42,200);color(RB_LED_E,false,42,200);press(RB_TURBO_E); // Turbo6.
    press(RB_AUTO_W);color(RB_LED_FN,false,49,200);color(RB_LED_W,false,49,200);press(RB_AUTO_W); // Forward7.
    press(RB_GAME);color(RB_LED_LGUI,false,56,200);color(RB_LED_FN,false,63,200);press(RB_GAME); // Lock8/game9.
    now=660;click_target(19);redblade_rgb_begin_frame();color(RB_LED_FN,false,70,200);color(18,false,70,200);
    assert(redblade_controls_indicator(18,false,&c) && c.v==120);redblade_controls_suspend(); // Left10 uses the same palette peak as Fn.
    click_target(20);color(RB_LED_FN,false,77,200);color(19,false,77,200);redblade_controls_suspend(); // Right11.
    host_locks=(led_t){.caps_lock=true,.num_lock=true,.scroll_lock=true};
    color(RB_LED_CAPS,false,84,200);color(RB_LED_NUM,false,84,200);color(RB_LED_SCROLL,false,91,200); // Locks12/13.
    host_locks=(led_t){0};
    assert(!redblade_controls_indicator(RB_LED_CAPS,false,&c));
    redblade_rgb_begin_frame();c=redblade_rgb_color(61);assert(c.h==80 && c.v==100); // Inactive Caps keeps its per-key base.
    assert(redblade_controls_indicator(57,false,&c) && c.h==140 && c.s==150 && c.v==25); // Inactive Num dims that base.
    redblade_recorder_arm();color(RB_LED_FN,false,119,200); // Selection17.
    redblade_recorder_slot(0,false,false);redblade_recorder_task(false);
    redblade_recorder_key(4,true);color(RB_LED_FN,false,98,200); // Recording14.
    now+=20;redblade_recorder_key(4,false);redblade_recorder_arm();
    redblade_recorder_slot(0,false,false);redblade_recorder_task(false);color(RB_LED_FN,false,105,200); // One-shot15.
    redblade_recorder_stop(true);redblade_recorder_slot(0,false,true);redblade_recorder_task(false);
    color(RB_LED_FN,false,112,200);redblade_recorder_stop(true); // Loop16.
    color(2,true,126,200);color(RB_LED_W,true,133,200);color(RB_LED_E,true,140,200); // Hints18..20.
    color(1,true,147,200);color(3,true,154,200);color(5,true,161,200); // Hints21..23.
    redblade_rgb_toggle_setting(RB_SETTING_ANIMATION);
    assert(redblade_controls_indicator(5,true,&c) && !c.s); // Disabled options remain neutral.
    puts("PASS: all24 effect colors, blink/breath scaling, inactive lock palette and colored numpad dimming");
}
