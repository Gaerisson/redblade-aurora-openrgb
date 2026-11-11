// Integration coverage for long reactions and physical-modifier highlights.
#include "qmk_fixture.h"
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static void setup(uint8_t rate, uint8_t style, uint8_t highlights, uint8_t layout) {
    uint8_t payload[1696];
    store_fixture_reset();store_fixture_payload(payload);
    uint8_t *p=payload+32;
    p[3]=RB_PROFILE_WHITE;p[7]=15;p[9]=layout;p[10]=style;p[11]=16;p[12]=rate;p[13]=highlights;
    // Deterministic, distinguishable effect slots.
    for(unsigned i=0;i<24;++i) { p[343+3*i]=7*i;p[344+3*i]=255;p[345+3*i]=100; }
    p[343+21*3]=170;p[343+17*3]=32;
    store_fixture_seed(0,1,payload);
    memset(key_down,0,sizeof(key_down));mods=weak_mods=0;host_locks=(led_t){0};
    now=0;enabled=true;value=255;mode=RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
    redblade_store_init();redblade_controls_init();redblade_rgb_init(true);
    now=REDBLADE_BOOT_ANIMATION_MS+1;redblade_rgb_begin_frame();
}
static uint8_t frame(uint8_t sample) {
    uint8_t saturation=0;
    redblade_rgb_begin_frame();
    for(uint8_t i=0;i<109;++i) {
        hsv_t c=redblade_rgb_color(i);
        if(i==sample) saturation=c.s;
    }
    return saturation;
}
static void tap(uint16_t code) {
    keyrecord_t r={.event={.key={.row=2,.col=3},.pressed=true}};
    assert(!redblade_controls_process(code,&r));r.event.pressed=false;
    assert(!redblade_controls_process(code,&r));
}
static bool indicator(uint8_t index, bool fn, hsv_t *c) {
    return redblade_controls_indicator(index,fn,c);
}
static void fade_lifetimes(void) {
    const uint8_t rates[]={0,1,8};
    for(unsigned i=0;i<sizeof(rates);++i) {
        const uint8_t rate=rates[i];
        setup(rate,0,0,0);redblade_rgb_key_flash(42);
        const unsigned frames=rate==8 ? 120 : 15;
        for(unsigned n=0;n<frames;++n) assert(frame(42)>0);
        assert(frame(42)==0);
        // Retrigger near expiry restores the entire configured lifetime.
        redblade_rgb_key_flash(42);
        for(unsigned n=1;n<frames;++n) assert(frame(42)>0);
        redblade_rgb_key_flash(42);
        for(unsigned n=0;n<frames-(rate ? rate : 1);++n) assert(frame(42)>0);
        for(unsigned n=0;n<(rate ? rate : 1)+1U;++n) frame(42);
        assert(frame(42)==0); // A shared subframe clock quantizes retrigger by at most one step.
        redblade_rgb_key_flash(42);assert(frame(42)>0);
        redblade_rgb_profile(RB_PROFILE_OFF);assert(frame(42)==0);
        redblade_rgb_profile(RB_PROFILE_WHITE);assert(frame(42)==0);
        redblade_rgb_key_flash(42);assert(frame(42)>0);
        redblade_rgb_suspend();assert(frame(42)==0);
        redblade_rgb_wake();now+=REDBLADE_BOOT_ANIMATION_MS;assert(frame(42)==0);
    }
    setup(8,0,0,0);redblade_rgb_key_flash(42);
    for(unsigned n=0;n<120;++n) { if(n%3==0) redblade_rgb_key_flash(43);frame(42); }
    assert(frame(42)==0); // Repeated presses elsewhere must not freeze older trails.
}
static void wave_styles(void) {
    for(uint8_t style=3;style<=4;++style) {
        setup(8,style,0,0);redblade_rgb_key_flash(42);
        bool lit=false;
        for(unsigned n=0;n<120;++n) if(frame(42)>0) lit=true;
        assert(lit);assert(frame(42)==0);
        redblade_rgb_key_flash(42);lit=false;
        for(unsigned n=0;n<120;++n) if(frame(42)>0) lit=true;
        assert(lit);assert(frame(42)==0);
    }
}
static void short_pulses(void) {
    for(uint8_t lifetime=1;lifetime<=2;++lifetime) {
        uint8_t payload[1696];store_fixture_reset();store_fixture_payload(payload);
        payload[32+3]=RB_PROFILE_WHITE;payload[32+7]=lifetime;payload[32+10]=4;
        store_fixture_seed(0,1,payload);now=0;enabled=true;value=255;mode=RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
        redblade_store_init();redblade_controls_init();redblade_rgb_init(true);
        now=REDBLADE_BOOT_ANIMATION_MS+1;frame(42);redblade_rgb_key_flash(42);
        for(uint8_t n=0;n<lifetime;++n)assert(frame(42)>0);
        assert(frame(42)==0); // Below three frames use a visible flash rather than aliased zero pulses.
    }
}
static void highlights(void) {
    hsv_t c;
    setup(0,0,0,0);mods=1;assert(!indicator(96,false,&c));
    setup(0,0,1,0);mods=1;
    assert(indicator(96,false,&c) && c.h==170);
    assert(indicator(42,false,&c) && c.h==170); // AZERTY A.
    assert(indicator(96,true,&c) && c.v==0); // Fn focus masks unrelated keys.
    mods=0;weak_mods=1;assert(!indicator(96,false,&c));weak_mods=0;
    mods=0x10;assert(indicator(103,false,&c) && c.h==170);
    mods=2;assert(indicator(78,false,&c) && c.h==200);
    mods=0x20;assert(indicator(90,false,&c) && c.h==200);
    mods=4;assert(indicator(98,false,&c) && c.h==32);
    mods=0x40;assert(indicator(100,false,&c) && c.h==32);
    // Local mode never overlays a fresh host frame.
    redblade_rgb_profile(RB_PROFILE_HOST);
    uint8_t packet[32]={7,0x42,42,0,1,50,255,100};assert(via_command_kb(packet,32));frame(42);
    mods=1;assert(!indicator(96,false,&c));
    setup(0,0,2,1);redblade_rgb_profile(RB_PROFILE_HOST);
    assert(via_command_kb(packet,32));frame(42);mods=1;
    assert(indicator(96,false,&c) && c.h==170);
    assert(indicator(62,false,&c) && c.h==170); // QWERTY A.
    assert(!indicator(42,false,&c));
    mods=0;assert(!indicator(96,false,&c));
    // Higher-priority automation and lock indicators retain their colors.
    tap(RB_GAME);mods=2;assert(indicator(RB_LED_LGUI,false,&c) && c.h==56);
    tap(RB_GAME);mods=0;tap(RB_TURBO_E);mods=2;
    assert(indicator(RB_LED_E,false,&c) && c.h==42);tap(RB_TURBO_E);
    mods=0;redblade_recorder_arm();mods=1;
    assert(indicator(RB_LED_FN,false,&c) && c.h==32);
    redblade_rgb_profile(RB_PROFILE_OFF);assert(!indicator(96,false,&c));
    redblade_rgb_profile(RB_PROFILE_WHITE);redblade_rgb_suspend();assert(!indicator(96,false,&c));
    mods=weak_mods=0;
}
int main(void) {
    fade_lifetimes();wave_styles();short_pulses();highlights();
    puts("PASS: long reaction frame lifetimes, retrigger, wave styles and modifier overlay priorities");
}
