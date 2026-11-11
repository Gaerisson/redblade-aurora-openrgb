// Exercise the actual custom-effect include and pinned QMK color conversion.
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "qmk_fixture.h"
#include <assert.h>
#include <stdio.h>
typedef struct { uint8_t iter; } effect_params_t;
#define pixels rendered
#define RGB_MATRIX_EFFECT(name)
#define RGB_MATRIX_CUSTOM_EFFECT_IMPLS
#define RGB_MATRIX_USE_LIMITS(min,max) const uint8_t min=params->iter*8; const uint8_t max=min+8<109?min+8:109
static bool rgb_matrix_check_finished_leds(uint8_t max) { return max<109; }
#include "rgb_matrix_user.inc"
static void render(void) {
    effect_params_t params={0};
    while(REDBLADE_DIRECT(&params)) ++params.iter;
}
static unsigned red_center(void) {
    unsigned weighted=0,total=0;
    for(unsigned i=0;i<109;++i) { assert(pixels[i].r>=pixels[i].g && pixels[i].r>=pixels[i].b); weighted+=pixels[i].r*g_led_config.point[i].x;total+=pixels[i].r; }
    assert(total>1000); return weighted/total;
}
static bool fn_key(unsigned i) {
    // Shipped 33 bindings plus Fn itself; independently checked against the map.
    const uint8_t keys[]={0,1,2,3,4,5,6,7,8,9,15,16,17,18,19,21,22,23,33,36,40,43,44,45,46,56,60,65,97,99,101,102,104,106};
    for (unsigned k=0;k<sizeof(keys);++k) if (keys[k]==i) return true;
    return false;
}
static void overlay(bool held) {
    for (uint8_t start=0;start<109;start+=8) redblade_controls_render(start,start+8<109?start+8:109,held);
}
static void assert_fn_focus(void) {
    unsigned lit=0;
    for (unsigned i=0;i<109;++i) {
        const bool on=pixels[i].r || pixels[i].g || pixels[i].b;
        assert(on==fn_key(i));
        lit+=on;
    }
    assert(lit==34);
}
int main(void) {
    redblade_rgb_init(true); redblade_controls_init();
    // Reproduce OpenRGB's Direct request with all unused scalar fields zero.
    uint8_t direct[32]={7,0x41,1};
    assert(via_command_kb(direct,32) && value==255);
    now=500; render(); const unsigned left=red_center();
    now=1000; render(); assert(red_center()>left+30);
    now=2000; render();
    for(unsigned i=16;i<=19;++i) assert(pixels[i].r==64 && pixels[i].g==64 && pixels[i].b==64);
    uint8_t packet[32]={7,0x42,44,0,1,0,255,80}; assert(via_command_kb(packet,32));
    render(); assert(pixels[44].r==80 && pixels[44].g<2 && pixels[44].b==0);
    redblade_rgb_replay(); now+=500; render(); red_center();
    redblade_rgb_suspend(); render();overlay(true);
    for(unsigned i=0;i<109;++i) assert(!pixels[i].r && !pixels[i].g && !pixels[i].b);
    redblade_rgb_wake(); now+=REDBLADE_RGB_TIMEOUT_MS; render();
    redblade_controls_render(0,109,false);
    unsigned dim_keys=0;
    for(unsigned i=0;i<109;++i) if(pixels[i].r==8 && pixels[i].g==8 && pixels[i].b==8) ++dim_keys;
    assert(dim_keys==17 && pixels[RB_LED_NUM].r==8); // Entire numpad very dim when Num Lock is off.
    host_locks=(led_t){.num_lock=true,.caps_lock=true,.scroll_lock=true};
    render(); redblade_controls_render(0,109,false);
    assert(pixels[RB_LED_CAPS].g==128 && !pixels[RB_LED_CAPS].r);
    assert(pixels[RB_LED_NUM].g==128 && !pixels[RB_LED_NUM].r);
    assert(pixels[57].r==64 && pixels[57].g==64 && pixels[RB_LED_SCROLL].r==128);
    assert(pixels[RB_LED_FN].b==24 && pixels[103].r==64 && pixels[103].b==64); // Actual Fn, not Right Ctrl.
    uint8_t stream[32]={7,0x42,RB_LED_CAPS,0,1,0,255,200}; assert(via_command_kb(stream,32));
    render(); redblade_controls_render(0,109,false);
    assert(pixels[RB_LED_CAPS].r==200 && !pixels[RB_LED_CAPS].g); // Fresh host takes priority over lock fallback.
    keyrecord_t game={.event={.key={.row=5,.col=1},.pressed=true}};
    redblade_controls_process(RB_GAME,&game); redblade_controls_render(0,109,false);
    assert(pixels[RB_LED_LGUI].r==128 && !pixels[RB_LED_LGUI].g && pixels[RB_LED_RGUI].r==128);

    // Fn temporarily masks all non-shortcuts; the incoming frame is preserved.
    redblade_controls_init();redblade_rgb_defaults();now+=REDBLADE_BOOT_ANIMATION_MS;
    for (uint8_t first=0;first<109;first+=9) {
        uint8_t p[32]={7,0x42,first,0,109-first<9?109-first:9};
        for (unsigned j=0;j<p[4];++j) { p[5+j*3]=100;p[6+j*3]=150;p[7+j*3]=180; }
        assert(via_command_kb(p,32));
    }
    rgb_t original[109];render();overlay(false);
    for (unsigned i=0;i<109;++i) original[i]=pixels[i];
    redblade_controls_render(8,16,true);
    for (unsigned i=0;i<109;++i) if(i<8 || i>=16) assert(pixels[i].r==original[i].r && pixels[i].g==original[i].g && pixels[i].b==original[i].b);
    render();overlay(true);assert_fn_focus();
    render();overlay(false);
    for (unsigned i=0;i<109;++i) assert(pixels[i].r==original[i].r && pixels[i].g==original[i].g && pixels[i].b==original[i].b);
    // The mask also overrides Caps/Num/Scroll fallback and reactive white.
    now+=REDBLADE_RGB_TIMEOUT_MS;render();overlay(true);assert_fn_focus();
    assert(!pixels[RB_LED_CAPS].r && !pixels[RB_LED_CAPS].g && !pixels[RB_LED_NUM].g);
    // An arbitrary running Turbo target is not a Fn shortcut; its Fn status stays.
    base_keymap[3][5]=0x0A;
    keyrecord_t choose={.event={.key={.row=2,.col=5},.pressed=true}};
    assert(!redblade_controls_process(RB_TURBO_SELECT,&choose));choose.event.pressed=false;
    assert(!redblade_controls_process(RB_TURBO_SELECT,&choose));
    choose.event.key=(keypos_t){.row=3,.col=5};choose.event.pressed=true;
    assert(!redblade_controls_process(0x0A,&choose));choose.event.pressed=false;
    assert(!redblade_controls_process(0x0A,&choose));redblade_controls_task(false);
    assert(redblade_turbo_key()==0x0A);
    render();overlay(true);assert_fn_focus();assert(pixels[RB_LED_FN].r && !pixels[RB_LED_FN].g);
    render();overlay(false);assert(pixels[g_led_config.matrix_co[3][5]].r);
    value=0;render();overlay(true);
    for(unsigned i=0;i<109;++i) assert(!pixels[i].r && !pixels[i].g && !pixels[i].b);
    value=255;
    // Off still displays the Fn guide while held, but never during USB suspend.
    redblade_rgb_profile(RB_PROFILE_OFF); render(); overlay(true); assert_fn_focus();
    render(); overlay(false);
    for(unsigned i=0;i<109;++i) assert(!pixels[i].r && !pixels[i].g && !pixels[i].b);
    redblade_rgb_suspend(); render(); overlay(true);
    for(unsigned i=0;i<109;++i) assert(!pixels[i].r && !pixels[i].g && !pixels[i].b);
    puts("PASS: actual 8-LED effect rendering, zero-field Direct requests, moving red wave, 4 visible media LEDs, linear host brightness, USB off");
}
