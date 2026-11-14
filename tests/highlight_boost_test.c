// Exercise the real journal, modifier overlay, master and final dimmed copy.
#include "qmk_fixture.h"
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static void seed(uint8_t boost,uint8_t highlight) {
    uint8_t payload[1696];store_fixture_reset();store_fixture_payload(payload);
    uint8_t *p=payload+32;p[3]=RB_PROFILE_WHITE;p[13]=highlight;p[14]=95;p[15]=boost;
    // Deliberately dark but fully saturated red/green prove the peak lift.
    p[343+21*3]=0;p[343+21*3+1]=255;p[343+21*3+2]=80;
    p[343+17*3]=85;p[343+17*3+1]=255;p[343+17*3+2]=128;
    store_fixture_seed(0,1,payload);redblade_store_init();
}
static void setup(uint8_t boost,uint8_t highlight) {
    seed(boost,highlight);assert(redblade_store_valid());
    now=100;mods=weak_mods=0;enabled=true;value=255;mode=RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
    memset(key_down,0,sizeof(key_down));redblade_controls_init();redblade_rgb_init(true);
    now+=REDBLADE_BOOT_ANIMATION_MS;redblade_rgb_begin_frame();rgb_matrix_set_color_all(100,200,50);
}
static void overlay(bool fn) {
    for(uint8_t i=0;i<109;i+=8)redblade_controls_render(i,i+8<109?i+8:109,fn);
    rgb_matrix_update_pwm_buffers();
}
static void pixel(uint8_t i,uint8_t r,uint8_t g,uint8_t b) {
    const rgb_t c=hardwareRGBcommitted[i];assert(c.r==r && c.g==g && c.b==b);
}
static void peak(uint8_t held,uint8_t index,uint8_t expected) {
    mods=held;hsv_t c;assert(redblade_controls_indicator(index,false,&c));assert(c.v==expected);
}
int main(void) {
    seed(0,2);assert(redblade_store_valid());
    seed(1,2);assert(redblade_store_valid());
    seed(2,2);assert(!redblade_store_valid());seed(255,2);assert(!redblade_store_valid());
    setup(0,2);peak(1,96,80);peak(2,78,80);peak(4,98,80);
    setup(1,2);
    const uint8_t masks[]={1,0x10,2,0x20,4,0x40};
    const uint8_t indices[]={96,103,78,90,98,100};
    for(unsigned i=0;i<sizeof(masks);++i)peak(masks[i],indices[i],255);
    mods=1;overlay(false);pixel(96,255,0,0);pixel(10,5,10,2);
    for(unsigned i=0;i<10;++i){overlay(false);pixel(96,255,0,0);pixel(10,5,10,2);}
    // The existing global scalar still limits the peak: no electrical boost.
    redblade_rgb_runtime(RB_PROFILE_WHITE,128,7);overlay(false);pixel(96,128,0,0);
    value=64;overlay(false);pixel(96,32,0,0);value=255;
    redblade_rgb_runtime(RB_PROFILE_WHITE,255,7);mods=4;overlay(false);pixel(98,0,255,0);
    // Fn hints keep their configured level; boost is only a physical modifier overlay.
    mods=1;overlay(true);pixel(1,80,0,0);pixel(10,0,0,0);
    setup(1,0);mods=1;hsv_t c;assert(!redblade_controls_indicator(96,false,&c));
    setup(1,1);redblade_rgb_profile(RB_PROFILE_HOST);
    uint8_t packet[32]={7,0x42,42,0,1,50,255,100};assert(via_command_kb(packet,32));
    redblade_rgb_begin_frame();mods=1;assert(!redblade_controls_indicator(96,false,&c));
    setup(1,2);redblade_rgb_profile(RB_PROFILE_HOST);assert(via_command_kb(packet,32));
    redblade_rgb_begin_frame();mods=1;overlay(false);pixel(96,255,0,0);pixel(10,5,10,2);
    // A non-black Alt palette value of 1 rounds to zero in legacy scaling,
    // but maximum mode must still light it; only an explicit zero stays off.
    uint8_t payload[1696];memcpy(payload,redblade_store_data(),sizeof(payload));payload[32+343+17*3+2]=1;
    store_fixture_reset();store_fixture_seed(0,2,payload);redblade_store_init();peak(4,98,255);
    // Black remains an explicit off choice even with maximum intensity enabled.
    payload[32+343+21*3+2]=0;
    store_fixture_reset();store_fixture_seed(0,2,payload);redblade_store_init();
    peak(1,96,0);overlay(false);pixel(96,0,0,0);
    redblade_rgb_profile(RB_PROFILE_OFF);overlay(false);assert(!redblade_controls_indicator(96,false,&c));
    redblade_controls_suspend();redblade_rgb_suspend();assert(!redblade_controls_indicator(96,false,&c));
    puts("PASS: highlight maximum bool/legacy, exact 255 RGB, all modifiers, master caps, dim stability, Fn/host/local/black/Off/suspend gates");
}
