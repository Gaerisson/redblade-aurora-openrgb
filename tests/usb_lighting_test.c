// Actual Full/Core QMK hooks against a frozen or advancing hardware SOF counter.
#include "qmk_fixture.h"
#include <assert.h>
#include <stdio.h>
#define QMK_KEYBOARD_H "keymap_stub.h"
#ifdef REDBLADE_TEST_CORE_USB
#include "../firmware/openrgb_core/keymap.c"
#else
#include "../firmware/aurora_via/keymap.c"
#endif
layer_state_t layer_state;
extern unsigned rgb_full_flushes;
static bool qmk_suspended;
void gpio_set_pin_output(uint32_t pin) { assert(pin==LED_FN_PIN); }
// Deliberately does not flush: prove our explicit off path does not rely on a
// later QMK rendering task, which may never run after shutdown/suspension.
void rgb_matrix_set_suspend_state(bool suspend) { qmk_suspended=suspend; }
static void black(void) {
    assert(!usb_lighting.active && qmk_suspended && indicator_pin_high);
    for(unsigned i=0;i<109;++i) {
        assert(!(rendered[i].r || rendered[i].g || rendered[i].b));
        assert(!(hardwareRGBcommitted[i].r || hardwareRGBcommitted[i].g || hardwareRGBcommitted[i].b));
    }
}
static void seed(void) {
    rgb_matrix_set_color_all(91,172,255);
    rgb_matrix_update_pwm_buffers();
}
static void tick(uint32_t ms,bool frames) {
    now+=ms;if(frames) usb_frame_number=(usb_frame_number+ms)&2047;
    housekeeping_task_user();
}
static void notification(uint8_t state) {
    usb_configure_state=state;
    notify_usb_device_state_change_user((struct usb_device_state){state});
}
static void tap(uint16_t code) {
    keyrecord_t key={.event={.key={.row=0,.col=0},.pressed=true}};
    process_record_user(code,&key);key.event.pressed=false;process_record_user(code,&key);
}
#ifndef REDBLADE_TEST_CORE_USB
static void fallback(void) {
    redblade_rgb_begin_frame();
    const hsv_t color=redblade_rgb_color(10);
    assert(!color.s && color.v>0);
}
#endif
int main(void) {
    now=100;usb_frame_number=71;usb_configure_state=USB_DEVICE_STATE_CONFIGURED;
    keyboard_pre_init_user();keyboard_post_init_user();seed();
    assert(usb_lighting.active && !qmk_suspended);
    const unsigned writes=eeprom_writes,flushed=rgb_full_flushes;
    tick(249,false);assert(usb_lighting.active && rgb_full_flushes==flushed);
    tick(1,false);black();assert(rgb_full_flushes==flushed+1);
    // A stale CONFIGURED state, Fn held and repeated host notifications cannot
    // revive lighting, rewrite preferences, or restart a helper without SOFs.
    layer_state=2;
    for(unsigned i=0;i<20;++i) { tick(100,false);notification(USB_DEVICE_STATE_CONFIGURED);black(); }
    assert(rgb_full_flushes==flushed+1 && eeprom_writes==writes);
#ifndef REDBLADE_TEST_CORE_USB
    tap(RB_GAME);tap(RB_TURBO_E);tap(RB_AUTO_W);tap(RB_ANTI_AFK);
    assert(!redblade_game_mode() && !redblade_turbo_key() && !redblade_auto_w() && !redblade_anti_afk());
    assert(!key_down[KC_E] && !key_down[KC_W]);black();
#else
    tap(RB_CORE_BOOT);black();
#endif
    // Holding the boot shortcut while inactive must not arm a delayed reset
    // that fires as soon as SOFs resume. Basic input still passes to QMK.
    keyrecord_t held={.event={.key={.row=0,.col=0},.pressed=true}};
#ifdef REDBLADE_TEST_CORE_USB
    assert(process_record_user(RB_CORE_BOOT,&held));
#else
    assert(process_record_user(RB_BOOT,&held));
#endif
    assert(process_record_user(KC_E,&held));
    held.event.pressed=false;assert(process_record_user(KC_E,&held));
    const unsigned boots=bootloader_calls;
    layer_state=0;
    tick(1,true);assert(usb_lighting.active && !qmk_suspended);
    // Closing Aurora stops RGB packets, but SOFs continue: keep the selected
    // standalone/fallback profile for arbitrarily many RGB timeout periods.
    for(unsigned i=0;i<200;++i) { tick(100,true);assert(usb_lighting.active); }
    assert(bootloader_calls==boots);
#ifndef REDBLADE_TEST_CORE_USB
    fallback();
    tap(RB_TURBO_E);assert(key_down[KC_E] && redblade_turbo_key());
    tick(250,false);black();assert(!key_down[KC_E] && !redblade_turbo_key());
    tick(1,true);assert(usb_lighting.active && !redblade_turbo_key());
    redblade_rgb_profile(RB_PROFILE_RED);
    seed();tick(250,false);black();
    tick(1,true);assert(redblade_rgb_profile_get()==RB_PROFILE_RED);
    for(unsigned i=0;i<60;++i) tick(100,true);
    redblade_rgb_begin_frame();assert(redblade_rgb_color(10).s>200 && redblade_rgb_color(10).v>0);
    redblade_rgb_profile(RB_PROFILE_HOST);
#endif
    // Explicit suspend/unconfiguration cuts all pixels immediately, even with
    // a still-advancing frame counter. Resume never needs an RGB application.
    seed();notification(USB_DEVICE_STATE_SUSPEND);black();
    for(unsigned i=0;i<10;++i) { tick(20,true);black(); }
    notification(USB_DEVICE_STATE_CONFIGURED);assert(usb_lighting.active);
    seed();notification(USB_DEVICE_STATE_RESET);black();
    tick(1000,true);black();notification(USB_DEVICE_STATE_CONFIGURED);assert(usb_lighting.active);
    // Both the 11-bit USB counter and the 32-bit timer wrap safely.
    usb_frame_number=2047;now=UINT32_MAX-100;
    housekeeping_task_user();tick(1,true);assert(usb_frame_number==0 && usb_lighting.active);
    tick(249,false);assert(usb_lighting.active);tick(1,false);black();
    tick(1,true);assert(usb_lighting.active);
    // Long blocked work followed by a different frame is activity, not a
    // catch-up shutdown; the timeout depends on actual observed progression.
    tick(5000,true);assert(usb_lighting.active);
    assert(eeprom_writes==writes);
    // Cold boot into a powered but unconfigured USB port stays dark forever.
    usb_configure_state=USB_DEVICE_STATE_RESET;
    keyboard_post_init_user();black();tick(30000,true);black();
    notification(USB_DEVICE_STATE_CONFIGURED);assert(usb_lighting.active);
    puts("PASS: actual USB hooks, frozen SOF cutoff/full hardware black, Aurora-free fallback, profile retention, suspend/reconnect and counter wraps");
}
