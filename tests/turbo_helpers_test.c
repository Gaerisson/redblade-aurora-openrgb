#include "qmk_fixture.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_rgb.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static bool edge(uint16_t c,bool down,uint8_t row,uint8_t col) {
    keyrecord_t r={.event={.key={.row=row,.col=col},.pressed=down}};
    return redblade_controls_process(c,&r);
}
static void tap(uint16_t c,uint8_t row,uint8_t col) { assert(!edge(c,true,row,col));assert(!edge(c,false,row,col)); }
static void tick(uint32_t ms) { now+=ms;redblade_controls_task(false); }
static void reset(void) {
    redblade_controls_suspend();memset(key_down,0,sizeof(key_down));mods=weak_mods=0;
    now=100;redblade_rgb_init(true);redblade_rgb_defaults();redblade_controls_init();
}
static hsv_t cue(uint8_t led) { hsv_t c;assert(redblade_controls_indicator(led,false,&c));return c; }
static void same(hsv_t a,hsv_t b) { assert(a.h==b.h && a.s==b.s && a.v==b.v); }
static void middle(void) {
    reset();assert(redblade_turbo_rate()==60);base_keymap[0][18]=0xA8;
    tap(RB_TURBO_SELECT,2,5);unsigned keys=key_down_calls,reports=mouse_reports;
    assert(!edge(0xA8,true,0,18));assert(!mouse_buttons && mouse_reports==reports);
    assert(!edge(0xA8,false,0,18));assert(mouse_buttons==4 && redblade_turbo_led()==17 && key_down_calls==keys);
    redblade_rgb_begin_frame();same(cue(RB_LED_FN),cue(17));
    assert(!edge(0xA8,true,0,18));assert(!edge(0xA8,false,0,18));assert(!mouse_buttons && !redblade_turbo_key() && key_down_calls==keys);
    tap(RB_TURBO_SELECT,2,5);tap(0xA8,0,18);assert(mouse_buttons==4);
    assert(edge(KC_ESC,true,0,0));assert(!mouse_buttons);
    tap(RB_TURBO_SELECT,2,5);tap(0xA8,0,18);redblade_controls_suspend();assert(!mouse_buttons);
    tap(RB_TURBO_SELECT,2,5);tap(0xA8,0,18);tap(RB_TURBO_E,2,3);assert(!mouse_buttons && redblade_turbo_key()==KC_E);
}
static void frames(void) {
    reset();tap(RB_TURBO_E,2,3);uint8_t led=redblade_turbo_led();
    redblade_rgb_begin_frame();hsv_t initial=cue(RB_LED_FN);same(initial,cue(led));
    tick(8);same(initial,cue(led));same(initial,cue(RB_LED_FN)); // frame remains coherent across edge
    redblade_rgb_begin_frame();same(cue(RB_LED_FN),cue(led));
    // At 60 CPS five actual presses form a visual group. The initial press
    // plus four subsequent down edges must flip it; releases do not count.
    for(int i=0;i<8;i++) tick(9);
    redblade_rgb_begin_frame();hsv_t grouped=cue(led);same(grouped,cue(RB_LED_FN));assert(grouped.v!=initial.v);
    tick(1000);redblade_rgb_begin_frame();same(cue(RB_LED_FN),cue(led)); // One edge only after stall.
    for(int i=0;i<11;i++) tap(RB_TURBO_SLOWER,1,20);
    assert(redblade_turbo_rate()==5);
    tick(100);redblade_rgb_begin_frame();hsv_t a=cue(led);same(a,cue(RB_LED_FN));
    tick(100);redblade_rgb_begin_frame();hsv_t b=cue(led);same(b,cue(RB_LED_FN));assert(a.v!=b.v);
}
static void afk(void) {
    reset();assert(!redblade_anti_afk());unsigned n=mouse_moves;
    tap(RB_ANTI_AFK,2,8);tick(29999);assert(mouse_moves==n);tick(1);assert(mouse_moves==++n && mouse_last_x==1);
    tick(30000);assert(mouse_moves==++n && mouse_last_x==-1);
    tick(90000);assert(mouse_moves==++n && mouse_last_x==1);tick(0);assert(mouse_moves==n);
    assert(edge(4,true,3,1));assert(!redblade_anti_afk());tick(30000);assert(mouse_moves==n);
    tap(RB_ANTI_AFK,2,8);assert(edge(KC_ESC,true,0,0));assert(!redblade_anti_afk());
    tap(RB_ANTI_AFK,2,8);tap(RB_ANTI_AFK,2,8);assert(!redblade_anti_afk());
    tap(RB_ANTI_AFK,2,8);redblade_controls_suspend();assert(!redblade_anti_afk());
    now=UINT32_MAX-10000;tap(RB_ANTI_AFK,2,8);tick(30000);assert(mouse_moves==++n && mouse_last_x==1);
    tap(RB_TURBO_E,2,3);assert(!redblade_anti_afk() && redblade_turbo_key());
    tap(RB_ANTI_AFK,2,8);assert(redblade_anti_afk() && !redblade_turbo_key());
    tap(RB_AUTO_W,2,2);assert(!redblade_anti_afk() && redblade_auto_w());
    tap(RB_ANTI_AFK,2,8);assert(redblade_anti_afk() && !redblade_auto_w() && !key_down[KC_W]);
    tap(RB_REC_ARM,0,17);assert(!redblade_anti_afk() && redblade_recorder_active());
    tap(RB_ANTI_AFK,2,8);assert(redblade_anti_afk() && !redblade_recorder_active());
    for(unsigned i=0;i<3;i++) {
        const uint16_t activity[]={0x0104,0x0206,0x2004};
        if(!redblade_anti_afk())tap(RB_ANTI_AFK,2,8);
        assert(edge(activity[i],true,3,1));assert(!redblade_anti_afk());
    }
    tap(RB_ANTI_AFK,2,8);assert(edge(QK_MOMENTARY+1,true,5,13));assert(redblade_anti_afk());
}
static void standard_effect_phase(void) {
    reset();mode=RGB_MATRIX_SOLID_COLOR;tap(RB_TURBO_E,2,3);
    redblade_controls_render(0,8,false);redblade_controls_render(44,45,false);redblade_controls_render(102,103,false);
    rgb_t bright=rendered[44];assert(bright.r>0 && bright.r==rendered[102].r);
    for(unsigned i=0;i<8;i++)tick(9);
    redblade_controls_render(0,8,false);redblade_controls_render(44,45,false);redblade_controls_render(102,103,false);
    assert(rendered[44].r<bright.r && rendered[44].r==rendered[102].r);
    mode=RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
}
int main(void) { middle();frames();standard_effect_phase();afk();puts("PASS: turbo mouse, frame synchronization and anti-AFK"); }
