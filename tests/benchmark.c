// SPDX-License-Identifier: GPL-2.0-or-later
// Host regression benchmark: actual handler, controls and QMK color code.
// This does not measure MCU execution, USB scheduling or key-input latency.
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include "qmk_fixture.h"
#include <stdio.h>
#include <time.h>

static volatile uint32_t checksum;

static double frames(unsigned scenario) {
    const unsigned iterations=50000;
    redblade_rgb_init(true); redblade_controls_init(); now=2000;
    if (scenario==1) for(unsigned i=0;i<8;++i) redblade_rgb_brightness_step(-1);
    if (scenario==2 || scenario==4) redblade_rgb_profile(RB_PROFILE_WHITE);
    if(scenario==6) mods=0x23;
    clock_t start=clock();
    for(unsigned frame=0;frame<iterations;++frame) {
        now+=34;
        for(unsigned first=0;first<109;first+=9) {
            uint8_t packet[32]={7,0x42,first,0,0};
            packet[4]=109-first<9 ? 109-first : 9;
            for(unsigned n=0;n<packet[4];++n) { packet[5+n*3]=frame+first+n; packet[6+n*3]=220; packet[7+n*3]=180; }
            via_command_kb(packet,32);
        }
        if(scenario==3) { redblade_rgb_replay(); now+=1300; }
        if(scenario==4) for(unsigned led=42;led<=46;++led) redblade_rgb_key_flash(led);
        redblade_rgb_begin_frame();
        for(unsigned first=0;first<109;first+=8) {
            const bool indicators=redblade_controls_indicators_needed(scenario==5);
            for(unsigned i=first;i<first+8 && i<109;++i) {
                hsv_t hsv=redblade_rgb_color(i),status;
                if(indicators && redblade_controls_indicator(i,scenario==5,&status)) hsv=redblade_rgb_apply_brightness(status);
                const rgb_t c=redblade_rgb_convert(hsv);
                checksum+=c.r+c.g+c.b;
            }
        }
    }
    mods=0;
    return ((double)(clock()-start)/CLOCKS_PER_SEC)*1e9/iterations;
}

static double key_events(bool game) {
    const unsigned iterations=2000000;
    redblade_controls_init();
    if(game) { keyrecord_t toggle={.event={.key={.row=0,.col=17},.pressed=true}}; redblade_controls_process(RB_GAME,&toggle); }
    keyrecord_t record={.event={.key={.row=3,.col=2},.pressed=true}};
    clock_t start=clock();
    for(unsigned i=0;i<iterations;++i) {
        checksum+=redblade_controls_process(4,&record);
        redblade_controls_task(false);
    }
    return ((double)(clock()-start)/CLOCKS_PER_SEC)*1e9/iterations;
}

static double recorder_task_cost(bool loop) {
    const unsigned iterations=2000000;
    redblade_controls_init();
    if(loop) {
        redblade_recorder_arm();redblade_recorder_slot(0,false,false);
        redblade_recorder_key(4,true);now+=20;redblade_recorder_key(4,false);redblade_recorder_arm();
        redblade_recorder_slot(0,false,true);
    }
    clock_t start=clock();
    for(unsigned i=0;i<iterations;++i) { ++now;redblade_recorder_task(false);checksum+=redblade_recorder_state(); }
    const double result=((double)(clock()-start)/CLOCKS_PER_SEC)*1e9/iterations;
    redblade_controls_suspend();return result;
}

int main(void) {
    store_fixture_reset();redblade_store_init();
    const double host=frames(0),dim=frames(1),white=frames(2),wave=frames(3),reactive=frames(4),fn_focus=frames(5);
    uint8_t config[REDBLADE_STORE_PAYLOAD_SIZE];
    for(unsigned i=0;i<sizeof(config);++i)config[i]=redblade_store_default_byte(i);
    config[32+3]=1; // Exercise direct reads from a committed onboard local palette.
    store_fixture_seed(0,1,config);redblade_store_init();
    const double custom=frames(0),custom_fn=frames(5);
    const double normal=key_events(false),game=key_events(true);
    const double rec_idle=recorder_task_cost(false),rec_loop=recorder_task_cost(true);
    config[32+10]=1;store_fixture_seed(0,2,config);redblade_store_init();const double ripple=frames(4);
    config[32+10]=2;store_fixture_seed(0,3,config);redblade_store_init();const double horizontal=frames(4);
    config[32+10]=3;store_fixture_seed(0,4,config);redblade_store_init();const double cross=frames(4);
    config[32+10]=4;store_fixture_seed(0,5,config);redblade_store_init();const double pulse=frames(4);
    config[32+10]=0;config[32+12]=8;store_fixture_seed(0,6,config);redblade_store_init();const double long_reaction=frames(4);
    config[32+13]=1;store_fixture_seed(0,7,config);redblade_store_init();const double modifier=frames(6);
    config[32+15]=1;store_fixture_seed(0,8,config);redblade_store_init();const double maximum_modifier=frames(6);
    printf("{\"kind\":\"host_native_regression\",\"firmware\":\"%u.%u.%u\",\"frame_iterations_per_scenario\":50000,"
           "\"host_cpu_ns_per_109_led_frame\":{\"direct\":%.3f,\"dimmed\":%.3f,\"local_white\":%.3f,\"wave_handover\":%.3f,\"reactive_white_five_keys\":%.3f,\"fn_focus\":%.3f,\"onboard_palette\":%.3f,\"onboard_fn_focus\":%.3f,\"onboard_reactive_ripple_five_keys\":%.3f,\"onboard_reactive_wave_five_keys\":%.3f,\"onboard_cross_five_keys\":%.3f,\"onboard_pulse_five_keys\":%.3f,\"onboard_long_reaction\":%.3f,\"modifier_highlights\":%.3f,\"maximum_modifier_highlights\":%.3f},"
           "\"host_cpu_ns_per_key_hook\":{\"game_off\":%.3f,\"game_on\":%.3f},\"checksum\":%u,"
           "\"host_cpu_ns_per_recorder_task\":{\"idle\":%.3f,\"loop_20ms_hold_5ms_gap\":%.3f},"
           "\"cpu_clock_ticks_per_second\":%lu,\"key_hook_iterations\":2000000,"
           "\"physical_key_latency_measured\":false,\"mcu_execution_time_measured\":false}\n",
           REDBLADE_VERSION_MAJOR,REDBLADE_VERSION_MINOR,REDBLADE_VERSION_PATCH,host,dim,white,wave,reactive,fn_focus,custom,custom_fn,ripple,horizontal,cross,pulse,long_reaction,modifier,maximum_modifier,normal,game,(unsigned)checksum,rec_idle,rec_loop,(unsigned long)CLOCKS_PER_SEC);
    return 0;
}
