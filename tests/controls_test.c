#include "redblade_controls.h"
#include "redblade_rgb.h"
#include "qmk_fixture.h"
#include <assert.h>
#include <stdio.h>

static bool event(uint16_t code, bool pressed, uint8_t row, uint8_t col) {
    keyrecord_t record = {.event = {.key = {.row=row, .col=col}, .pressed=pressed}};
    const bool passthrough = redblade_controls_process(code, &record);
    if (passthrough && code >= 4 && code <= 0xA4) key_down[code] = pressed;
    return passthrough;
}
static void press(uint16_t code) { assert(!event(code,true,0,17)); assert(!event(code,false,0,17)); }
static hsv_t color(uint8_t led) { redblade_rgb_begin_frame(); return redblade_rgb_color(led); }
static void frame(uint8_t led, uint8_t val) {
    uint8_t packet[32] = {7,0x42,led,0,1,20,255,val};
    assert(via_command_kb(packet,32) && packet[0]==7);
}

int main(void) {
    redblade_rgb_init(true); redblade_controls_init(); now=2000;
    assert(event(4,true,3,2) && event(4,false,3,2)); // Ordinary typing untouched.
    mods = MOD_MASK_GUI | 5; weak_mods = MOD_MASK_GUI | 2;
    press(RB_GAME); assert(redblade_game_mode());
    assert(mods == 5 && weak_mods == 2 && keyboard_reports == 1);
    assert(!event(KC_LGUI,true,5,1) && !event(KC_RGUI,false,5,12));
    assert(!event(0x0804,true,3,2) && !event(0x1804,false,3,2)); // Left/right GUI-encoded shortcuts.
    assert(!event(0x01E3,true,3,2)); // Ctrl + a GUI key must not bypass the lock.
    assert(event(5,true,3,3));
    mods=MOD_MASK_SHIFT; press(RB_GAME); assert(!redblade_game_mode()); mods=0;
    assert(event(KC_LGUI,true,5,1));

    assert(!event(RB_PLAY,true,0,18) && key_down[KC_MPLY]);
    assert(!event(0xA8,false,0,18) && !key_down[KC_MPLY]); // Changed Fn-layer release.
    mods=MOD_MASK_SHIFT; assert(!event(RB_PLAY,true,0,18) && key_down[KC_MSTP]);
    mods=0; assert(!event(RB_PLAY,false,0,18) && !key_down[KC_MSTP]);
    const unsigned before = key_down_calls;
    mods=MOD_MASK_SHIFT; assert(!event(RB_PREV,true,0,19));
    assert(redblade_rgb_brightness_get()==239 && key_down_calls==before);
    mods=0; assert(!event(0xA9,false,0,19));
    assert(!event(RB_NEXT,true,0,20) && key_down[KC_MNXT]);
    redblade_controls_suspend(); assert(!key_down[KC_MNXT]);

    redblade_rgb_defaults(); now=3000; frame(0,200); assert(color(0).v==200);
    press(RB_WHITE); assert(color(0).s==0 && color(0).v==64);
    frame(0,150); assert(color(0).s==0 && color(0).v==64); // Host cannot override local profile.
    press(RB_RED); assert(color(0).h==0 && color(0).s==255);
    press(RB_LIGHT_TOGGLE); assert(color(16).v==0 && !redblade_rgb_output_active());
    press(RB_LIGHT_TOGGLE); assert(redblade_rgb_profile_get()==RB_PROFILE_RED);
    press(RB_HOST); assert(color(0).v==150 && color(0).h==20);
    for(unsigned i=0;i<40;++i) press(RB_DIMMER);
    assert(redblade_rgb_brightness_get()==0 && color(16).v==0 && !redblade_rgb_output_active());
    for(unsigned i=0;i<40;++i) press(RB_BRIGHTER);
    assert(redblade_rgb_brightness_get()==255);
    press(RB_DIMMER); assert(color(0).v==141); // 150 * 239 / 255 rounded.
    press(RB_BRIGHTER);
    press(RB_MEDIA_FALLBACK); assert(color(16).v==0);
    press(RB_MEDIA_FALLBACK); assert(color(16).v==64);
    now+=REDBLADE_RGB_TIMEOUT_MS; assert(color(0).v==64);
    press(RB_IDLE_FALLBACK); assert(color(16).v==0 && !redblade_rgb_output_active());
    frame(0,180); assert(color(0).v==180);
    value=0; press(RB_DEFAULTS); assert(eeprom_writes==0 && !redblade_game_mode() && value==255);
    press(RB_ANIMATION); assert(!(redblade_rgb_settings_get() & RB_SETTING_ANIMATION));
    press(RB_RED); press(RB_DIMMER); press(RB_SAVE); assert(eeprom_writes==1);
    const uint8_t saved_settings=redblade_rgb_settings_get();
    redblade_rgb_init(true);
    assert(redblade_rgb_profile_get()==RB_PROFILE_RED && redblade_rgb_brightness_get()==239);
    assert(redblade_rgb_settings_get()==saved_settings && eeprom_writes==1);
    press(RB_DEFAULTS); assert(eeprom_writes==1 && redblade_rgb_brightness_get()==255);
    eeprom_user=0xFFE0FFA7; redblade_rgb_init(true); // Corrupt / unrelated user word.
    assert(redblade_rgb_profile_get()==RB_PROFILE_HOST && redblade_rgb_settings_get()==7);

    mods=0;
    now=UINT32_MAX-1000; assert(!event(RB_BOOT,true,0,0));
    now+=REDBLADE_BOOT_HOLD_MS-1; redblade_controls_task(true); assert(!bootloader_calls);
    ++now; redblade_controls_task(true); assert(bootloader_calls==1);
    redblade_controls_task(true); assert(bootloader_calls==1);
    assert(!event(RB_BOOT,true,0,0)); redblade_controls_task(false);
    now+=4000; redblade_controls_task(true); assert(bootloader_calls==1);
    mods=0;
    assert(!event(RB_BOOT,true,0,0));
    assert(!event(RB_BOOT,false,0,0));
    now+=4000; redblade_controls_task(true); assert(bootloader_calls==1);

    // Turbo is edge-driven, bounded, and never catches up with a burst.
    now=10000; assert(!event(RB_TURBO_E,true,2,3));
    assert(redblade_turbo_key()==KC_E && redblade_turbo_rate()==50 && key_down[KC_E]);
    assert(!event(KC_E,false,2,3)); // Releasing Fn first must not release the generated key.
    now+=9; redblade_controls_task(false); assert(key_down[KC_E]);
    ++now; redblade_controls_task(false); assert(!key_down[KC_E]);
    now+=10; redblade_controls_task(false); assert(key_down[KC_E]);
    const unsigned edges=key_up_calls;
    now+=1000; redblade_controls_task(false); assert(key_up_calls==edges+1 && !key_down[KC_E]);
    assert(event(KC_E,true,2,3) && !redblade_turbo_key()); // Physical target takes over.
    assert(event(KC_E,false,2,3));
    assert(!event(RB_AUTO_W,true,2,2) && redblade_auto_w() && key_down[KC_W]);
    assert(!event(KC_W,false,2,2) && key_down[KC_W]);
    assert(!event(RB_TURBO_F,true,3,4) && key_down[KC_F]);
    assert(event(KC_ESC,true,0,0));
    assert(!redblade_auto_w() && !redblade_turbo_key() && !key_down[KC_W] && !key_down[KC_F]);
    redblade_controls_init();
    assert(event(KC_E,true,2,3)); // A remapped trigger cannot steal a held physical E.
    assert(!event(RB_TURBO_E,true,1,1) && !redblade_turbo_key());
    assert(!event(RB_TURBO_E,false,1,1));
    assert(event(KC_E,false,2,3));
    press(RB_TURBO_RATE); assert(redblade_turbo_rate()==100);
    press(RB_TURBO_RATE); assert(redblade_turbo_rate()==25);
    press(RB_TURBO_RATE); assert(redblade_turbo_rate()==50);
    assert(!event(RB_AUTO_W,true,2,2));
    assert(!event(RB_TURBO_SPACE,true,5,6));
    press(RB_GAME); assert(redblade_game_mode());
    assert(!event(RB_BOOT,true,0,0)); // Quick Fn+Esc stops; releasing Esc cancels boot.
    assert(!event(RB_BOOT,false,0,0));
    now+=4000; redblade_controls_task(true);
    assert(!redblade_game_mode() && !redblade_auto_w() && !redblade_turbo_key() && bootloader_calls==1);
    redblade_controls_suspend();

    now=UINT32_MAX-4; assert(!event(RB_TURBO_E,true,2,3));
    now+=10; redblade_controls_task(false); assert(!key_down[KC_E]);
    press(RB_GAME); assert(!event(RB_AUTO_W,true,2,2));
    redblade_controls_suspend();
    assert(!redblade_game_mode() && !redblade_turbo_key() && !redblade_auto_w() && !key_down[KC_W]);
    assert(!event(RB_BOOT,true,0,0)); assert(!event(0x29,false,0,0));
    now+=4000; redblade_controls_task(true); assert(bootloader_calls==1);

    redblade_rgb_defaults(); now+=2000; frame(0,200);
    hsv_t indicator;
    assert(redblade_controls_indicator(15,true,&indicator) && indicator.v>0);
    assert(redblade_controls_indicator(18,true,&indicator));
    assert(!redblade_controls_indicator(18,false,&indicator));
    press(RB_GAME); assert(redblade_controls_indicator(RB_LED_LGUI,false,&indicator) && indicator.h==0);
    assert(redblade_controls_indicator(RB_LED_RGUI,false,&indicator) && indicator.h==0);
    assert(!redblade_controls_indicator(16,false,&indicator));
    press(RB_DARK); assert(!redblade_controls_indicator(16,true,&indicator));
    uint8_t data[32]={0xFE,0x81}; assert(via_command_kb(data,32));
    assert(data[2]==1 && data[4]==REDBLADE_VERSION_MINOR && data[6]==1 && data[7]==RB_PROFILE_OFF);
    assert(eeprom_writes==1); // All actions except explicit Save remain RAM-only.
    redblade_rgb_defaults(); redblade_controls_init(); mods=0;
    base_keymap[2][5]=0x17; base_keymap[3][5]=0x0A; // T and G, actual physical positions.
    base_keymap[0][2]=0x3A; base_keymap[0][18]=0xA8;
    assert(!event(RB_TURBO_SELECT,true,2,5) && redblade_turbo_selecting());
    assert(!event(0x17,false,2,5)); // Fn may be released before T.
    assert(!event(0x0A,true,3,5) && !redblade_turbo_key() && !key_down[0x0A]);
    assert(!event(0x0A,false,3,5) && redblade_turbo_key()==0x0A && key_down[0x0A]);
    assert(redblade_turbo_led()==g_led_config.matrix_co[3][5]);
    assert(redblade_controls_indicator(RB_LED_FN,false,&indicator) && indicator.h==0);
    press(RB_TURBO_FASTER); press(RB_TURBO_FASTER); assert(redblade_turbo_rate()==60 && redblade_turbo_key()==0x0A);
    const unsigned downs=key_down_calls;
    for(unsigned i=0;i<1000;++i) { ++now; redblade_controls_task(false); }
    assert(key_down_calls-downs>=59 && key_down_calls-downs<=61);
    press(RB_TURBO_SLOWER); assert(redblade_turbo_rate()==55 && redblade_turbo_key()==0x0A);
    for(unsigned i=0;i<50;++i) press(RB_TURBO_SLOWER);
    assert(redblade_turbo_rate()==5);
    for(unsigned i=0;i<50;++i) press(RB_TURBO_FASTER);
    assert(redblade_turbo_rate()==250);
    assert(event(0x0A,true,3,5) && !redblade_turbo_key()); // Any target yields to a real press.
    assert(event(0x0A,false,3,5));
    assert(!event(RB_TURBO_SELECT,true,2,5)); assert(!event(0x17,false,2,5));
    assert(!event(RB_HOST,true,0,2)); // Select F1 while Fn is still held.
    assert(!event(0x3A,false,0,2) && redblade_turbo_key()==0x3A);
    assert(!event(RB_TURBO_SELECT,true,2,5) && !redblade_turbo_key() && !redblade_turbo_selecting());
    assert(!event(0x17,false,2,5));
    assert(!event(RB_TURBO_SELECT,true,2,5)); assert(!event(0x17,false,2,5));
    assert(!event(RB_PLAY,true,0,18) && !key_down[KC_MPLY] && redblade_turbo_selecting());
    assert(!event(0xA8,false,0,18)); // Unsupported consumer choice does not play music.
    base_keymap[1][0]=0x66;
    assert(!event(0x66,true,1,0) && redblade_turbo_selecting());
    assert(!event(0x66,false,1,0)); // Keyboard power is not a repeat target either.
    assert(!event(0x0A,true,3,5)); assert(event(KC_ESC,true,0,0));
    assert(!event(0x0A,false,3,5) && !redblade_turbo_key()); // Stop before selected key release.
    assert(event(KC_ESC,false,0,0));
    now=UINT32_MAX-100;
    assert(!event(RB_TURBO_SELECT,true,2,5)); assert(!event(0x17,false,2,5));
    now+=REDBLADE_TURBO_SELECT_TIMEOUT_MS; redblade_controls_task(false);
    assert(!redblade_turbo_selecting());
    assert(redblade_controls_indicator(RB_LED_FN,true,&indicator) && indicator.h==128);
    assert(redblade_controls_indicator(RB_LED_FN,false,&indicator) && indicator.h==170);
    assert(!event(RB_TURBO_SELECT,true,2,5)); assert(!event(0x17,false,2,5));
    base_keymap[2][2]=KC_W;
    assert(!event(KC_W,true,2,2)); assert(!event(KC_W,false,2,2) && redblade_turbo_key()==KC_W);
    assert(!event(RB_AUTO_W,true,2,2) && !redblade_turbo_key() && redblade_auto_w());
    assert(!event(KC_W,false,2,2));
    redblade_controls_suspend(); assert(!key_down[KC_W] && !redblade_turbo_selecting());
    base_keymap[1][20]=0x56;
    assert(!event(RB_TURBO_SELECT,true,2,5)); assert(!event(0x17,false,2,5));
    assert(!event(0x56,true,1,20)); assert(!event(0x56,false,1,20) && key_down[0x56]);
    assert(!event(RB_TURBO_SLOWER,true,1,20));
    assert(!event(0x56,false,1,20) && key_down[0x56]); // Fn released first: do not steal the generated report.
    assert(event(KC_ESC,true,0,0) && !key_down[0x56]);
    puts("PASS: Windows lock, media release safety, profiles, persistence, Turbo ownership/rates/stop, forward hold, guarded boot, indicators");
}
