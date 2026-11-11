// Actual recorder/controls code; QMK transport is replaced by the shared fixture.
#include "redblade_recorder.h"
#include "redblade_controls.h"
#include "redblade_rgb.h"
#include "qmk_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static void key(uint16_t code,bool down,uint32_t elapsed) {
    now+=elapsed;
    keyrecord_t record={.event={.key={.row=2,.col=6},.pressed=down}};
    if(redblade_controls_process(code,&record) && code>=4 && code<=255) {
        if(down) register_code(code); else unregister_code(code);
    }
}
static void control(uint16_t code,uint8_t row,uint8_t col) {
    keyrecord_t record={.event={.key={.row=row,.col=col},.pressed=true}};
    assert(!redblade_controls_process(code,&record));record.event.pressed=false;
    assert(!redblade_controls_process(code,&record));
}
static void start_record(uint8_t slot) { redblade_recorder_arm();redblade_recorder_slot(slot,false,false);assert(redblade_recorder_state()==RB_REC_RECORDING); }
static void end_record(void) { redblade_recorder_arm();assert(redblade_recorder_state()==RB_REC_IDLE); }
static void tick(uint32_t elapsed) { now+=elapsed;redblade_controls_task(false); }
static void no_keys(void) { for(unsigned i=0;i<256;++i) assert(!key_down[i]);assert(!mods); }
static void finish_play(void) {
    unsigned iterations=0;
    while(redblade_recorder_state()==RB_REC_PLAYING && iterations++<200) tick(1000000);
    assert(redblade_recorder_state()==RB_REC_IDLE);no_keys();
}
static void reset(void) {
    redblade_controls_suspend();memset(key_down,0,sizeof(key_down));mods=weak_mods=0;
    redblade_controls_init();redblade_rgb_defaults();now=100;
}

int main(void) {
    reset();
    // The real Fn controls arm, select and stop without recording themselves.
    control(RB_REC_ARM,0,17);assert(redblade_recorder_state()==RB_REC_ARMED);
    control(RB_REC_1,1,1);assert(redblade_recorder_state()==RB_REC_RECORDING);
    key(4,true,1000);key(4,false,35);key(5,true,700);key(5,false,60);
    control(RB_REC_ARM,0,17);assert(redblade_recorder_slot_bytes(0)==10);
    assert(eeprom_writes==0 && redblade_recorder_used()==10);
    control(RB_REC_1,1,1);assert(redblade_recorder_state()==RB_REC_PLAYING);
    tick(0);assert(key_down[4]);tick(34);assert(key_down[4]);tick(1);assert(!key_down[4]);
    tick(699);assert(!key_down[5]);tick(1);assert(key_down[5]);tick(60);no_keys();
    assert(redblade_recorder_state()==RB_REC_IDLE);

    // A physical VIA Shift+A takes ownership before QMK registers that chord.
    redblade_recorder_slot(0,false,false);tick(0);assert(key_down[4]);
    keyrecord_t chord={.event={.key={.row=3,.col=1},.pressed=true}};
    assert(redblade_controls_process(0x0204,&chord));
    assert(redblade_recorder_state()==RB_REC_IDLE && !key_down[4]);
    register_code(0xE1);register_code(4);tick(1000);assert(key_down[4]);
    unregister_code(4);unregister_code(0xE1);
    mods=MOD_MASK_SHIFT;redblade_recorder_slot(0,false,true);assert(redblade_recorder_state()==RB_REC_WAITING);
    assert(redblade_controls_process(0x0105,&chord));assert(redblade_recorder_state()==RB_REC_IDLE);mods=0;

    // Live speed changes, including boundaries and recorder/Turbo context.
    control(RB_REC_1,1,1);tick(0);assert(key_down[4]);
    for(unsigned i=0;i<4;++i) control(RB_TURBO_FASTER,2,20);
    assert(redblade_recorder_speed_quarters()==8 && redblade_turbo_rate()==60);
    tick(17);assert(key_down[4]);tick(1);assert(!key_down[4]);
    tick(350);assert(key_down[5]);tick(30);no_keys();
    for(unsigned i=0;i<30;++i) redblade_recorder_speed_step(-1);
    assert(redblade_recorder_speed_quarters()==2);
    for(unsigned i=0;i<30;++i) redblade_recorder_speed_step(1);
    assert(redblade_recorder_speed_quarters()==0);redblade_recorder_speed_reset();

    // Missed deadlines produce one edge, rather than a burst of old events.
    redblade_recorder_slot(0,false,false);tick(0);tick(10000);
    assert(!key_down[4] && !key_down[5] && redblade_recorder_state()==RB_REC_PLAYING);
    tick(699);assert(!key_down[5]);tick(1);assert(key_down[5]);
    key(6,true,0);assert(!key_down[5] && key_down[6] && redblade_recorder_state()==RB_REC_IDLE);
    key(6,false,0);no_keys();

    // Ctrl+C ownership, physical takeover and three slots/compaction.
    start_record(1);key(0xE0,true,0);key(6,true,10);key(6,false,20);key(0xE0,false,10);end_record();
    assert(redblade_recorder_slot_bytes(1)==8);
    redblade_recorder_slot(1,false,false);tick(0);assert(mods==1);
    key(0xE1,true,0);assert(mods==2 && !key_down[0xE0]);key(0xE1,false,0);no_keys();
    start_record(2);key(0xE3,true,0);key(8,true,5);key(8,false,20);key(0xE3,false,5);end_record();
    redblade_recorder_slot(2,true,false);assert(redblade_recorder_error()==RB_REC_GUI_LOCKED);no_keys();
    key(0xE1,true,0);redblade_recorder_slot(2,true,true);assert(redblade_recorder_state()==RB_REC_WAITING);
    key(0xE1,false,0);tick(0);assert(redblade_recorder_error()==RB_REC_GUI_LOCKED);no_keys();
    redblade_recorder_slot(2,false,false);tick(0);assert(mods==8);
    control(RB_GAME,5,1);assert(!mods && redblade_game_mode());control(RB_GAME,5,1);
    start_record(0);key(7,true,0);key(7,false,500);end_record();
    assert(redblade_recorder_slot_bytes(0)==6 && redblade_recorder_slot_bytes(1)==8 && redblade_recorder_slot_bytes(2)==8);
    for(unsigned slot=0;slot<3;++slot) { redblade_recorder_slot(slot,false,false);finish_play(); }

    // Refuse a nonempty keyboard instead of taking a real held key/modifier.
    key(4,true,0);redblade_recorder_arm();redblade_recorder_slot(0,false,false);
    assert(redblade_recorder_error()==RB_REC_BUSY && redblade_recorder_slot_bytes(0)==6);
    key(4,false,0);weak_mods=1;redblade_recorder_slot(0,false,false);assert(redblade_recorder_error()==RB_REC_BUSY);weak_mods=0;

    // Loop is explicit, has a release gap, supports speed changes and toggles off.
    start_record(0);key(4,true,0);key(4,false,20);end_record();
    redblade_recorder_slot(0,false,true);assert(redblade_recorder_loop());
    tick(0);assert(key_down[4]);tick(20);assert(!key_down[4]);
    redblade_recorder_speed_step(1); // Live speed must retain the inter-loop release gap.
    tick(REDBLADE_RECORDER_LOOP_GAP_MS-1);assert(!key_down[4]);tick(1);assert(key_down[4]);
    redblade_recorder_slot(0,false,true);assert(!redblade_recorder_loop());no_keys();
    redblade_recorder_slot(0,false,true);tick(0);key(KC_ESC,true,0);assert(!redblade_recorder_active());key(KC_ESC,false,0);no_keys();
    key(0xE1,true,0);control(RB_REC_1,1,1);
    assert(redblade_recorder_state()==RB_REC_WAITING && !key_down[4]);
    key(0xE1,false,0);redblade_controls_task(true);assert(redblade_recorder_state()==RB_REC_WAITING);
    tick(0);assert(redblade_recorder_state()==RB_REC_PLAYING);tick(0);assert(key_down[4]);
    key(0xE1,true,0);assert(!key_down[4] && mods==2);key(QK_MOMENTARY+1,true,0);control(RB_REC_1,1,1);
    assert(redblade_recorder_state()==RB_REC_IDLE);key(0xE1,false,0);no_keys();

    // An automatic full/timeout stop saves balanced events but never releases
    // a still-physical key from the live keyboard report while recording.
    reset();start_record(0);
    for(unsigned i=0;i<500 && redblade_recorder_state()==RB_REC_RECORDING;++i) {key(4,true,1);key(4,false,1);}
    assert(redblade_recorder_error()==RB_REC_FULL && redblade_recorder_used()<=REDBLADE_RECORDER_BYTES);
    redblade_recorder_slot(0,false,false);finish_play();
    reset();start_record(0);
    for(unsigned code=4;code<=119 && redblade_recorder_state()==RB_REC_RECORDING;++code) if(code!=KC_ESC && code!=0x66) key(code,true,1);
    assert(redblade_recorder_error()==RB_REC_FULL && redblade_recorder_used()<=REDBLADE_RECORDER_BYTES);
    for(unsigned code=4;code<=119;++code) if(key_down[code]) key(code,false,0);
    redblade_recorder_slot(0,false,false);finish_play();
    reset();start_record(0);key(4,true,0);tick(REDBLADE_RECORDER_MAX_MS);
    assert(redblade_recorder_error()==RB_REC_TIMEOUT && key_down[4]);key(4,false,0);
    assert(redblade_recorder_slot_bytes(0)==6);redblade_recorder_slot(0,false,false);tick(0);assert(key_down[4]);tick(REDBLADE_RECORDER_MAX_MS);no_keys();

    // Timer wrap, release-at-stop, RGB fade indicators and metadata-only reads.
    reset();now=UINT32_MAX-50;start_record(0);key(4,true,0);key(4,false,100);end_record();
    redblade_recorder_slot(0,false,false);tick(0);assert(key_down[4]);tick(99);assert(key_down[4]);tick(1);no_keys();
    redblade_recorder_slot(0,false,true);hsv_t c;assert(redblade_recorder_indicator(16,false,&c) && c.h==128);
    redblade_controls_suspend();assert(redblade_recorder_used() && !redblade_recorder_loop());no_keys();
    redblade_recorder_init();assert(!redblade_recorder_used());
    for(unsigned slot=0;slot<3;++slot) assert(!redblade_recorder_slot_bytes(slot));
    uint8_t data[32]={0xFE,0x82};assert(via_command_kb(data,32));
    assert(data[2]==1 && data[6]==RB_REC_IDLE && data[8]==4 && data[16]==0 && !memcmp(data+24,"RDBLFS01",8));
    assert(eeprom_writes==0);
    puts("PASS: timed 3-slot recording, long delays, live speed, modifiers, ownership, no catch-up, loop chord/release/toggle, capacity/timeout, compaction, sleep stop/reset wipe and metadata-only diagnostics");
}
