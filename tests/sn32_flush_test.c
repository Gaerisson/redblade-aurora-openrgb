// Simulate a PWM interrupt immediately after the production flush's memcpy.
// A complete raw frame must never become visible before background scaling.
#include "qmk_fixture.h"
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

rgb_t led_state[109], led_state_buf[109];
bool led_state_buf_update_required;
void sn32f2xx_flush(void);
static bool dim_expected;
static unsigned interrupt_samples;

static void sample_pwm(void) {
    ++interrupt_samples;
    // F10 is background for all modifier combinations, in both lighting modes.
    const rgb_t c=led_state[10];
    if (dim_expected) {
        if (c.r>30 || c.g>60 || c.b>15) {
            fprintf(stderr,"PWM observed undimmed background: %u,%u,%u\n",c.r,c.g,c.b);
            assert(!"An intermediate bright frame reached the live PWM buffer");
        }
    }
}
void *observing_memcpy(void *dst,const void *src,size_t size) {
    void *result=memcpy(dst,src,size);
    if (dst==led_state) sample_pwm();
    return result;
}
static void overlay(void) {
    for(uint8_t i=0;i<109;i+=8) redblade_controls_render(i,i+8<109?i+8:109,false);
    memcpy(led_state_buf,rendered,sizeof(led_state_buf));
}
static void run(bool host) {
    uint8_t payload[1696];
    store_fixture_reset();store_fixture_payload(payload);
    payload[32+3]=host?RB_PROFILE_HOST:RB_PROFILE_WHITE;
    payload[32+13]=2;payload[32+14]=70;
    store_fixture_seed(0,1,payload);redblade_store_init();assert(redblade_store_valid());
    now=100;mods=weak_mods=0;enabled=true;value=255;
    mode=RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
    redblade_controls_init();redblade_rgb_init(true);
    now+=REDBLADE_BOOT_ANIMATION_MS;redblade_rgb_begin_frame();
    if(host) {
        uint8_t packet[32]={7,0x42,42,0,1,50,255,100};
        assert(via_command_kb(packet,32));redblade_rgb_begin_frame();
    }
    assert(redblade_rgb_fallback_active()!=host);
    rgb_matrix_set_color_all(100,200,50);mods=1;overlay();
    // Begin with the previously dimmed frame. Each subsequent full flush must
    // stay dim throughout publication, not just at its return value.
    for(unsigned i=0;i<109;++i) led_state[i]=(rgb_t){30,60,15};
    dim_expected=true;
    for(unsigned frame=0;frame<20;++frame) {
        led_state_buf_update_required=true;sn32f2xx_flush();sample_pwm();
        assert(!led_state_buf_update_required);
        assert(led_state[10].r==30 && led_state[10].g==60 && led_state[10].b==15);
        assert(led_state_buf[10].r==100 && led_state_buf[10].g==200 && led_state_buf[10].b==50);
        assert(!memcmp(&led_state[96],&led_state_buf[96],sizeof(rgb_t))); // Ctrl cue
    }
    dim_expected=false;mods=0;overlay();led_state_buf_update_required=true;
    sn32f2xx_flush();sample_pwm();
    assert(!memcmp(&led_state[10],&led_state_buf[10],sizeof(rgb_t)));
}
int main(void) {
    run(false);run(true);assert(interrupt_samples>=42);
    puts("PASS: real patched Sonix flush never publishes a bright intermediate frame; local/host, repeated flush, raw preservation and release");
}
