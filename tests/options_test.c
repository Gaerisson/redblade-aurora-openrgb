#include "qmk_fixture.h"
#include "redblade_config.h"
#include "redblade_controls.h"
#include "redblade_store.h"
#include "redblade_rgb.h"
#include "redblade_recorder.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>
static bool event(uint16_t code,bool down,uint8_t row,uint8_t col) {
 keyrecord_t r={.event={.key={.row=row,.col=col},.pressed=down}};
 return redblade_controls_process(code,&r);
}
int main(void) {
 uint8_t data[1696];store_fixture_reset();store_fixture_payload(data);
 data[33]=15;data[40]=5;store_fixture_seed(0,1,data);redblade_store_init();
 redblade_rgb_init(true);redblade_controls_init();
 assert(redblade_config_feature(1024));
 assert(!event(0x7e61,true,3,0));assert(!event(0x7e61,false,3,0));
 // AZERTY: physical M is HID semicolon; HID M is comma and stops the word.
 assert(event(0x33,true,3,1));assert(get_weak_mods() & 2);
 assert(event(0x33,false,3,1));
 mods=2;assert(event(0x10,true,3,2));assert(!(get_weak_mods() & 2));assert(mods==2);mods=0;
 assert(event(4,true,3,1));assert(!(get_weak_mods() & 2));
 assert(!event(0x7e61,true,3,0));assert(!event(0x7e61,false,3,0));
 assert(event(4,true,3,1));assert(get_weak_mods() & 2);
 assert(event(4,false,3,1));
 assert(event(30,true,1,1));assert(!(get_weak_mods() & 2));
 assert(event(0x2A,true,1,2));
 assert(event(5,true,3,2));assert(get_weak_mods() & 2);
 hsv_t color;assert(redblade_controls_indicator(RB_LED_CAPS,false,&color) && color.h==32);
 assert(event(0x2c,true,4,5));
 assert(!(get_weak_mods() & 2));
 now=100;assert(!event(RB_TURBO_E,true,2,3));assert(key_down[KC_E]);
 now+=4999;redblade_controls_task(false);assert(redblade_turbo_key());
 ++now;redblade_controls_task(false);assert(!redblade_turbo_key() && !key_down[KC_E]);
 // Selecting the volume-up position creates mouse Turbo and must release it.
 assert(!event(RB_TURBO_SELECT,true,2,5));assert(!event(RB_TURBO_SELECT,false,2,5));
 base_keymap[0][19]=0xAA;assert(!event(0xAA,true,0,19));assert(!event(0xAA,false,0,19));assert(mouse_buttons);
 now+=5000;redblade_controls_task(false);assert(!mouse_buttons && !redblade_turbo_key());
 assert(!event(RB_AUTO_W,true,2,2));assert(key_down[KC_W]);
 now+=5000;redblade_controls_task(false);assert(!redblade_auto_w() && !key_down[KC_W]);
 redblade_recorder_arm();redblade_recorder_slot(0,false,false);
 now+=5000;redblade_controls_task(false);assert(redblade_recorder_state()==RB_REC_RECORDING);
 redblade_recorder_key(4,true);now+=10;redblade_recorder_key(4,false);redblade_recorder_arm();
 assert(redblade_recorder_slot_bytes(0)>0);
 assert(!event(RB_REC_1,true,2,1));assert(redblade_recorder_state()==RB_REC_PLAYING);
 now+=5000;redblade_controls_task(false);assert(redblade_recorder_state()!=RB_REC_PLAYING && redblade_recorder_slot_bytes(0)>0);
 assert(!event(0x7e61,true,3,0));assert(!event(0x7e61,false,3,0));
 assert(event(4,true,3,1));assert(get_weak_mods() & 2);redblade_controls_suspend();assert(!(get_weak_mods() & 2));

 // QWERTY uses HID M as a letter, and semicolon as punctuation.
 data[41]=1;store_fixture_seed(0,2,data);redblade_store_init();redblade_controls_init();
 assert(!event(0x7e61,true,3,0));assert(!event(0x7e61,false,3,0));
 assert(event(0x10,true,3,1));assert(get_weak_mods() & 2);
 assert(event(0x33,true,3,2));assert(!(get_weak_mods() & 2));
 assert(event(4,true,3,1));assert(!(get_weak_mods() & 2));
 data[41]=0;
 // A legacy byte 8 of zero means 30 seconds, and module bits default off.
 data[40]=0;store_fixture_seed(0,2,data);redblade_store_init();redblade_controls_init();
 now=1000;assert(!event(RB_AUTO_W,true,2,2));now+=5000;redblade_controls_task(false);assert(redblade_auto_w());
 now+=25000;redblade_controls_task(false);assert(!redblade_auto_w());
 data[33]=3;store_fixture_seed(0,3,data);redblade_store_init();redblade_controls_init();
 assert(!event(0x7e61,true,3,0));assert(!event(0x7e61,false,3,0));assert(event(4,true,3,1));assert(!(get_weak_mods() & 2));
 now=1000;assert(!event(RB_AUTO_W,true,2,2));now+=120000;redblade_controls_task(false);assert(redblade_auto_w());redblade_controls_suspend();
 // Anti-AFK uses the existing movement-module gate and safety timeout.
 assert(!event(RB_ANTI_AFK,true,2,8));assert(redblade_anti_afk());
 assert(!event(RB_ANTI_AFK,false,2,8));
 unsigned moves=mouse_moves;now+=30000;redblade_controls_task(false);assert(mouse_moves==moves+1);
 data[33]&=~1;store_fixture_seed(0,4,data);redblade_store_init();redblade_controls_init();
 assert(!event(RB_ANTI_AFK,true,2,8));assert(!redblade_anti_afk());
 data[33]=15;data[40]=5;store_fixture_seed(0,5,data);redblade_store_init();redblade_controls_init();
 assert(!event(RB_ANTI_AFK,true,2,8));assert(redblade_anti_afk());
 moves=mouse_moves;now+=5000;redblade_controls_task(false);assert(!redblade_anti_afk() && mouse_moves==moves);
 puts("options passed");
}
