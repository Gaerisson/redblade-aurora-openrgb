// SPDX-License-Identifier: GPL-2.0-or-later
#include "redblade_config.h"
#include "redblade_store.h"
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "report.h"
#include <stddef.h>

// Read the committed bank directly. No second palette/cache or EEPROM reads
// are introduced into normal input processing or the eight-LED RGB chunks.
static const uint8_t *profile(void) {
    const uint8_t *data=redblade_store_data();
    return data ? data+32+(uint16_t)data[0]*416 : NULL;
}
uint8_t redblade_config_byte(uint16_t offset,uint8_t fallback) {
    const uint8_t *p=profile();
    return p && offset<416 ? p[offset] : fallback;
}
bool redblade_config_feature(uint16_t feature) {
    if (feature<=RB_MODULE_IDLE) return (redblade_rgb_settings_get() & feature)!=0;
    const uint8_t *p=profile();
    return p ? ((((uint16_t)p[1]<<8)|p[0]) & feature)!=0 : (feature & 0x03FF)!=0;
}
static hsv_t triple(const uint8_t *p) { return (hsv_t){p[0],p[1],p[2]}; }
hsv_t redblade_config_key_color(uint8_t index,hsv_t fallback) {
    const uint8_t *p=profile();
    return p && index<109 ? triple(p+16+(uint16_t)index*3) : fallback;
}
hsv_t redblade_config_effect(uint8_t effect,hsv_t fallback) {
    const uint8_t *p=profile();
    return p && effect<24 ? triple(p+343+(uint16_t)effect*3) : fallback;
}
hsv_t redblade_config_effect_scaled(uint8_t effect,hsv_t fallback,uint8_t nominal_peak) {
    hsv_t color=redblade_config_effect(effect,(hsv_t){fallback.h,fallback.s,nominal_peak});
    const uint16_t value=nominal_peak ? (uint16_t)color.v*fallback.v/nominal_peak : 0;
    color.v=value>255 ? 255 : value;
    return color;
}
uint32_t redblade_config_timeout(void) { return (uint32_t)redblade_config_byte(4,5)*1000UL; }

uint8_t redblade_store_default_byte(uint16_t offset) {
    static const uint8_t PROGMEM metadata[16]={0xFF,3,255,0,5,50,2,15,30,0,0,0,0,0,0,0};
    static const uint8_t PROGMEM effects[72]={
        0,255,255, 0,255,255, 0,255,255, 0,0,64,
        170,255,24, 128,255,96, 0,255,128, 85,255,96,
        0,255,128, 200,255,80, 32,255,128, 200,255,128,
        85,255,128, 32,255,128, 0,255,128, 85,255,128,
        128,255,128, 32,255,128, 0,0,80, 85,255,80,
        0,255,96, 170,255,80, 0,255,80, 85,255,64
    };
    if (offset<32 || offset>=1696) return 0;
    const uint16_t i=(offset-32)%416;
    if (i<16) {
        // Import an unsaved legacy user's current simple lighting preferences
        // into slot1; other default slots remain factory profiles.
        if (offset<448) {
            if (i==0) return 0xF8 | redblade_rgb_settings_get();
            if (i==2) return redblade_rgb_brightness_get();
            if (i==3) return redblade_rgb_profile_get();
        }
        return pgm_read_byte(&metadata[i]);
    }
    if (i<343) return (i-16)%3==2 ? 64 : 0;
    if (i<415) return pgm_read_byte(&effects[i-343]);
    return 0;
}
void redblade_store_stop_helpers(void) { redblade_controls_suspend(); }
bool redblade_store_allow_begin(void) {
    redblade_controls_suspend();
    return !has_anykey() && !get_mods() && !get_weak_mods();
}
void redblade_store_runtime(uint8_t mode,uint8_t master,uint8_t flags) {
    redblade_rgb_runtime(mode,master,flags);
}
void redblade_store_applied(bool from_host) {
    if (from_host) {
        redblade_controls_apply_config();
        redblade_rgb_apply_config();
    }
}
