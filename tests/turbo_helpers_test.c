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
static bool rgb_same(rgb_t a,rgb_t b) { return a.r==b.r && a.g==b.g && a.b==b.b; }
static void frames(void) {
    const uint8_t rates[]={5,60,250};
    for(unsigned rate=0;rate<3;rate++) {
        reset();
        while(redblade_turbo_rate()>rates[rate]) tap(RB_TURBO_SLOWER,1,20);
        while(redblade_turbo_rate()<rates[rate]) tap(RB_TURBO_FASTER,1,19);
        unsigned commits=rgb_fast_commits;
        tap(RB_TURBO_E,2,3);uint8_t led=redblade_turbo_led();
        assert(key_down[KC_E]);same(cue(RB_LED_FN),cue(led));assert(cue(led).v==128);
        assert(rgb_fast_commits==commits+2);
        assert(rgb_same(hardwareRGBcommitted[RB_LED_FN],hardwareRGBcommitted[led]));
        assert(rgb_same(hardwareRGBcommitted[led],rendered[led]));
        // No RGB frame is requested: each actual HID edge must reach both LEDs.
        for(unsigned i=0;i<6;i++) {
            commits=rgb_fast_commits;bool was_down=key_down[KC_E];
            tick(rates[rate]==5 ? 100 : rates[rate]==60 ? 9 : 2);
            assert(key_down[KC_E]!=was_down);
            same(cue(RB_LED_FN),cue(led));assert(cue(led).v==(key_down[KC_E] ? 128 : 0));
            assert(rgb_fast_commits==commits+2);
            assert(rgb_same(hardwareRGBcommitted[RB_LED_FN],hardwareRGBcommitted[led]));
            assert(rgb_same(hardwareRGBcommitted[led],rendered[led]));
        }
        commits=rgb_fast_commits;bool was_down=key_down[KC_E];tick(1000);
        assert(key_down[KC_E]!=was_down && rgb_fast_commits==commits+2);
        tick(0);assert(rgb_fast_commits==commits+2);
        tap(RB_TURBO_E,2,3);commits=rgb_fast_commits;tick(1000);assert(rgb_fast_commits==commits);
        tap(RB_TURBO_E,2,3);redblade_controls_suspend();commits=rgb_fast_commits;
        tick(1000);assert(rgb_fast_commits==commits);
    }
}
static void inactive_rgb(void) {
    reset();tap(RB_TURBO_E,2,3);redblade_rgb_profile(RB_PROFILE_OFF);
    unsigned commits=rgb_fast_commits;bool down=key_down[KC_E];tick(9);
    assert(key_down[KC_E]!=down && rgb_fast_commits==commits);
    reset();tap(RB_TURBO_E,2,3);redblade_rgb_suspend();
    commits=rgb_fast_commits;down=key_down[KC_E];tick(9);
    assert(key_down[KC_E]!=down && rgb_fast_commits==commits);
}
static void afk(void) {
    reset();assert(!redblade_anti_afk());unsigned n=mouse_moves;
    tap(RB_ANTI_AFK,2,8);tick(29999);assert(mouse_moves==n && !key_down[KC_W]);
    tick(1);assert(mouse_moves==++n && mouse_last_x==8 && key_down[KC_W]);
    tick(79);assert(key_down[KC_W]);tick(1);assert(!key_down[KC_W] && !key_down[KC_S]);
    tick(39);assert(!key_down[KC_S]);tick(1);assert(key_down[KC_S]);
    tick(79);assert(key_down[KC_S]);tick(1);assert(!key_down[KC_S]);
    tick(29999);assert(mouse_moves==n);tick(1);assert(mouse_moves==++n && mouse_last_x==-8 && key_down[KC_W]);
    // A delayed scan advances just one phase and does not burst through W/S.
    tick(90000);assert(!key_down[KC_W] && !key_down[KC_S] && mouse_moves==n);
    tick(0);assert(!key_down[KC_S]);tick(40);assert(key_down[KC_S]);
    assert(edge(4,true,3,1));assert(!redblade_anti_afk() && !key_down[KC_S]);
    tick(30000);assert(mouse_moves==n);
    tap(RB_ANTI_AFK,2,8);tick(30000);assert(mouse_moves==++n && key_down[KC_W]);
    assert(edge(KC_ESC,true,0,0));assert(!redblade_anti_afk() && !key_down[KC_W]);
    tap(RB_ANTI_AFK,2,8);tap(RB_ANTI_AFK,2,8);assert(!redblade_anti_afk());
    tap(RB_ANTI_AFK,2,8);tick(30000);assert(mouse_moves==++n && key_down[KC_W]);
    redblade_controls_suspend();assert(!redblade_anti_afk() && !key_down[KC_W]);
    now=UINT32_MAX-10000;tap(RB_ANTI_AFK,2,8);tick(30000);assert(mouse_moves==++n && mouse_last_x==8);
    tap(RB_TURBO_E,2,3);assert(!redblade_anti_afk() && redblade_turbo_key() && !key_down[KC_W]);
    tap(RB_ANTI_AFK,2,8);assert(redblade_anti_afk() && !redblade_turbo_key());
    tap(RB_AUTO_W,2,2);assert(!redblade_anti_afk() && redblade_auto_w());
    tap(RB_ANTI_AFK,2,8);assert(redblade_anti_afk() && !redblade_auto_w() && !key_down[KC_W]);
    tap(RB_REC_ARM,0,17);assert(!redblade_anti_afk() && redblade_recorder_active());
    tap(RB_ANTI_AFK,2,8);assert(redblade_anti_afk() && !redblade_recorder_active());
    const uint16_t activity[]={0x0104,0x0206,0x2004};
    for(unsigned i=0;i<3;i++) {
        if(!redblade_anti_afk())tap(RB_ANTI_AFK,2,8);
        assert(edge(activity[i],true,3,1));assert(!redblade_anti_afk());
    }
    assert(edge(QK_MOMENTARY+1,true,5,13));
    tap(RB_ANTI_AFK,2,8);assert(redblade_anti_afk());
    assert(edge(QK_MOMENTARY+1,false,5,13));assert(redblade_anti_afk());
    tick(30000);assert(key_down[KC_W]);mods=2;
    assert(edge(0x2004,true,3,1));assert(!redblade_anti_afk() && !key_down[KC_W] && mods==2);mods=0;
    mods=1;tap(RB_ANTI_AFK,2,8);assert(!redblade_anti_afk());mods=0;
    weak_mods=2;tap(RB_ANTI_AFK,2,8);assert(!redblade_anti_afk());weak_mods=0;
    key_down[4]=true;tap(RB_ANTI_AFK,2,8);assert(!redblade_anti_afk());key_down[4]=false;
}
static void standard_effect_phase(void) {
    reset();mode=RGB_MATRIX_SOLID_COLOR;tap(RB_TURBO_E,2,3);
    redblade_controls_render(44,45,false);redblade_controls_render(102,103,false);
    rgb_t bright=rendered[44];assert(bright.r>0 && rgb_same(bright,rendered[102]));
    tick(8);redblade_controls_render(44,45,false);redblade_controls_render(102,103,false);
    assert(!rendered[44].r && rgb_same(rendered[44],rendered[102]));
    mode=RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
}
static void focus_and_flush(void) {
    const uint16_t usages[]={4,0x39,0x1e};
    const keypos_t positions[]={{.row=3,.col=1},{.row=3,.col=0},{.row=1,.col=1}};
    for(unsigned n=0;n<3;n++) {
        reset();const keypos_t pos=positions[n];base_keymap[pos.row][pos.col]=usages[n];
        tap(RB_TURBO_SELECT,2,5);tap(usages[n],pos.row,pos.col);
        const uint8_t led=redblade_turbo_led();hsv_t target,fn;
        assert(redblade_controls_indicator(led,true,&target));assert(redblade_controls_indicator(RB_LED_FN,true,&fn));
        same(target,fn);assert(target.v>0);
        for(unsigned start=0;start<80;start+=8)redblade_controls_render(start,start+8,true);
        tick(8); // Release updates both staging pixels before the frame ends.
        for(unsigned start=80;start<109;start+=8)redblade_controls_render(start,start+8<109?start+8:109,true);
        memcpy(hardwareRGBcommitted,rendered,sizeof(rendered)); // Ordinary frame flush.
        assert(!hardwareRGBcommitted[led].r && !hardwareRGBcommitted[RB_LED_FN].r);
        tick(8);
        for(unsigned start=0;start<109;start+=8)redblade_controls_render(start,start+8<109?start+8:109,true);
        memcpy(hardwareRGBcommitted,rendered,sizeof(rendered));
        assert(hardwareRGBcommitted[led].r>0 && hardwareRGBcommitted[led].r==hardwareRGBcommitted[RB_LED_FN].r);
    }
}
int main(void) { middle();frames();inactive_rgb();standard_effect_phase();focus_and_flush();afk();puts("PASS: turbo mouse, frame synchronization and anti-AFK"); }
