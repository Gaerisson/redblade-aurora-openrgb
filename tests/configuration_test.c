// Real Full configuration API integration; no USB or physical flash accesses.
#include "qmk_fixture.h"
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static void put32(uint8_t *p,uint32_t value) { for(unsigned i=0;i<4;++i) p[i]=value>>(8*i); }
static void command(uint8_t *p,uint8_t op) {
    p[0]=0xFE;p[1]=0x90;p[2]=op;
    assert(via_command_kb(p,32) && p[0]==0xFE && p[1]==0x90 && p[2]==op);
}
static void read_config(uint8_t *data) {
    for(unsigned offset=0;offset<1696;offset+=24) {
        const uint8_t count=1696-offset<24?1696-offset:24;
        uint8_t p[32]={0};p[4]=offset;p[5]=offset>>8;p[6]=count;command(p,1);
        assert(!p[3] && p[4]==(uint8_t)offset && p[5]==offset>>8 && p[6]==count);
        memcpy(data+offset,p+8,count);
    }
}
static void settle(void) {
    unsigned passes=0;
    while(redblade_store_busy() && passes++<3000) { ++now;redblade_store_task(); }
    assert(passes<3000);
}
static void save_config(const uint8_t *data) {
    uint8_t p[32]={0};const uint32_t generation=redblade_store_generation();
    put32(p+4,store_fixture_crc(data,1696));put32(p+8,generation);p[12]=0xA0;p[13]=6;p[14]=1;
    command(p,2);assert(p[3]==1); // Accepted; erasing/header work is cooperative.
    for(unsigned n=0;n<200;++n) {
        memset(p,0,32);command(p,3);if(p[4]==2) break;
        ++now;redblade_store_task();assert(n<199);
    }
    for(unsigned offset=0;offset<1696;offset+=24) {
        const uint8_t count=1696-offset<24?1696-offset:24;
        memset(p,0,32);p[4]=offset;p[5]=offset>>8;p[6]=count;memcpy(p+8,data+offset,count);
        command(p,4);assert(!p[3]);
    }
    memset(p,0,32);command(p,5);assert(p[3]==1);settle();
    assert(redblade_store_valid() && redblade_store_generation()==generation+1);
    uint8_t returned[1696];read_config(returned);assert(!memcmp(returned,data,1696));
}
static void press(uint16_t code) {
    keyrecord_t r={.event={.key={.row=2,.col=3},.pressed=true}};
    assert(!redblade_controls_process(code,&r));r.event.pressed=false;assert(!redblade_controls_process(code,&r));
}

int main(void) {
    store_fixture_reset();redblade_store_init();
    redblade_rgb_init(true);redblade_controls_init();
    uint8_t caps[32]={0xFE,0x90,0};
    assert(via_command_kb(caps,32));
    assert(caps[3]==0 && caps[4]==1 && caps[5]==4 && caps[6]==109 && caps[7]==24);
    assert(caps[8]==0xA0 && caps[9]==6 && caps[10]==0xA0 && caps[11]==1);
    assert(!memcmp(caps+24,"RDBLFS01",8));
    uint8_t payload[1696];read_config(payload);
    assert(!payload[0] && payload[32]==255 && payload[33]==3 && payload[34]==255 && payload[37]==50);
    assert(!eeprom_writes);

    // An app save stops helpers but retains completed temporary Recorder slots.
    redblade_recorder_arm();redblade_recorder_slot(0,false,false);
    redblade_recorder_key(4,true);now+=20;redblade_recorder_key(4,false);redblade_recorder_arm();
    const uint16_t recorded=redblade_recorder_slot_bytes(0);assert(recorded>=4);
    const unsigned selected=32+2*416;payload[0]=2;
    payload[selected]=0xDF;payload[selected+1]=3; // Disable keyboard Turbo only.
    payload[selected+2]=200;payload[selected+3]=1;payload[selected+4]=10;payload[selected+5]=80;payload[selected+6]=8;
    payload[selected+16+66*3]=170;payload[selected+16+66*3+1]=255;payload[selected+16+66*3+2]=150;
    payload[selected+343+5*3]=40;payload[selected+343+5*3+1]=255;payload[selected+343+5*3+2]=100;
    value=0;save_config(payload); // An explicit saved profile must recover an old scalar brightness0.
    assert(redblade_recorder_slot_bytes(0)==recorded);
    assert(redblade_turbo_rate()==80 && redblade_recorder_speed_quarters()==32);
    assert(redblade_rgb_profile_get()==1 && redblade_rgb_brightness_get()==200);
    redblade_rgb_begin_frame();const hsv_t custom=redblade_rgb_color(66);
    assert(custom.h==170 && custom.s==255 && custom.v==118);
    redblade_controls_render(0,109,true);
    assert(!rendered[RB_LED_E].r && !rendered[RB_LED_E].g && !rendered[RB_LED_E].b);
    assert(rendered[RB_LED_FN].r==78 && rendered[RB_LED_FN].g>0 && !rendered[RB_LED_FN].b);
    press(RB_TURBO_E);assert(!redblade_turbo_key() && !key_down[KC_E]);
    assert(!eeprom_writes); // The new profiles never consume/write VIA EEPROM.
    uint8_t stream[32]={7,0x42,66,0,1,10,255,100};assert(via_command_kb(stream,32));
    now+=6000;redblade_rgb_begin_frame();
    uint8_t diagnostic[32]={0xFE,0x80};assert(via_command_kb(diagnostic,32));
    assert((diagnostic[6]&1) && diagnostic[12]==0x10 && diagnostic[13]==0x27); // Selected timeout is10s, not5s.

    // Fn+F9 journals only live simple lighting, keeping every color/other slot.
    redblade_rgb_profile(RB_PROFILE_RED);redblade_rgb_brightness_step(-1);
    redblade_rgb_toggle_setting(RB_SETTING_MEDIA);
    assert(redblade_rgb_save() && redblade_store_busy());settle();
    uint8_t saved[1696];read_config(saved);
    assert(saved[selected+2]==184 && saved[selected+3]==2 && (saved[selected]&7)==5);
    for(unsigned i=0;i<1696;++i) if(i!=selected && i!=selected+2 && i!=selected+3) assert(saved[i]==payload[i]);
    assert(!eeprom_writes);
    // Reboot uses the committed profile, not a conflicting legacy user word.
    eeprom_user=0x010000A7;redblade_store_init();redblade_controls_init();redblade_rgb_init(true);
    assert(redblade_rgb_profile_get()==2 && redblade_rgb_brightness_get()==184 && redblade_rgb_settings_get()==5);
    const uint32_t saved_generation=redblade_store_generation();
    uint8_t preview[32]={0};preview[4]=0;preview[5]=222;preview[6]=7;command(preview,7);assert(!preview[3]);
    assert(redblade_rgb_profile_get()==0 && redblade_rgb_brightness_get()==222 && redblade_store_generation()==saved_generation);
    read_config(payload);assert(!memcmp(payload,saved,1696));
    press(RB_AUTO_W);assert(redblade_auto_w());
    uint8_t stop[32]={0};command(stop,8);assert(!stop[3] && !redblade_auto_w() && !key_down[KC_W]);
    // Temporary preview must also preserve Off's hidden restore target.
    redblade_rgb_profile(RB_PROFILE_WHITE);redblade_rgb_profile(RB_PROFILE_OFF);
    memset(preview,0,32);preview[4]=0;preview[5]=255;preview[6]=7;command(preview,7);
    memset(preview,0,32);preview[4]=3;preview[5]=255;preview[6]=7;command(preview,7);
    redblade_rgb_toggle();assert(redblade_rgb_profile_get()==RB_PROFILE_WHITE);
    puts("PASS: Studio schema, full profile read/save, palette/effects, module gating, retained Recorder, journaled F9, reboot and runtime-only preview");
}
