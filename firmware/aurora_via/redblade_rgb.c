// SPDX-License-Identifier: GPL-2.0-or-later
// VialRGB wire compatibility based on vial-kb/vial-qmk/quantum/vialrgb.c
// and CalcProgrammer1/OpenRGB/Controllers/QMKController/QMKVialRGBController.
#include "redblade_rgb.h"
#include "redblade_controls.h"
#include "redblade_recorder.h"
#include "redblade_config.h"
#include "redblade_store.h"
#include "raw_hid.h"
#include <string.h>

_Static_assert(RGB_MATRIX_LED_COUNT == 109, "Red Blade Full Size only");
enum { REDBLADE_REPORT_SIZE = 32 }; // Fixed QMK Raw HID API report size.

// Fixed allocation. Incoming packets only copy HSV; conversion is deferred
// to the cooperative RGB task (at most eight LEDs per task iteration).
static hsv_t incoming[RGB_MATRIX_LED_COUNT];
static uint32_t boot_started_at, last_packet_at, received_packets;
static bool boot_active, have_stream, suspended, idle_fallback;
enum render_kind { RENDER_OFF, RENDER_WAITING, RENDER_DIRECT, RENDER_WHITE, RENDER_BOOT, RENDER_LOCAL_WHITE, RENDER_RED };
static uint8_t render_kind, render_target;
static uint16_t boot_position;
static uint8_t boot_brightness, fallback_brightness, media_brightness, boot_blend;
static uint8_t local_profile, previous_profile, master_brightness = 255, settings = 7, manual_brightness;
// Two 4-bit frame lifetimes per byte: 55 bytes for every physical LED.
static uint8_t typing_ttl[(RGB_MATRIX_LED_COUNT + 1) / 2], typing_count;
// Single latest-key wave: source LED and remaining frame lifetime.
static uint8_t wave_source, wave_ttl;
#include "typing_positions.inc"
static void typing_clear(void) {
    wave_ttl = 0; boot_blend = 0;
    if (!typing_count) return;
    memset(typing_ttl, 0, sizeof(typing_ttl)); typing_count = 0;
}
bool redblade_rgb_key_lit(uint8_t index) {
    return index < RGB_MATRIX_LED_COUNT && ((typing_ttl[index / 2] >> ((index & 1) * 4)) & 15);
}
void redblade_rgb_key_flash(uint8_t index) {
    if (index >= RGB_MATRIX_LED_COUNT || !redblade_config_feature(RB_MODULE_REACTIVE) || !redblade_rgb_fallback_active() || !rgb_matrix_get_val()) return;
    if (redblade_config_byte(10, 0)) {
        boot_blend = 0; // Only the latest wave is retriggered.
        wave_source = index; wave_ttl = redblade_config_byte(7, 15) + 1; return;
    }
    if (!typing_count) boot_blend = 0; // Keep other keys aging during continuous typing.
    const uint8_t shift = (index & 1) * 4;
    if (!((typing_ttl[index / 2] >> shift) & 15)) ++typing_count;
    typing_ttl[index / 2] = (typing_ttl[index / 2] & ~(15 << shift)) | (redblade_config_byte(7,15) << shift);
}
void redblade_rgb_replay_key(uint8_t keycode) {
    if (keycode < 128) redblade_rgb_key_flash(pgm_read_byte(&replay_leds[keycode]));
}
static hsv_t typing_color(uint8_t index, uint8_t white) {
    hsv_t base=redblade_config_key_color(index,(hsv_t){0,0,white});
    if (base.v>rgb_matrix_get_val()) base.v=rgb_matrix_get_val();
    const uint8_t duration=redblade_config_byte(7,15), style=redblade_config_byte(10,0);
    uint16_t strength, divisor;
    if (style) {
        if (!wave_ttl) return base;
        const int16_t dx=(int16_t)g_led_config.point[index].x-g_led_config.point[wave_source].x;
        const int16_t dy=(int16_t)g_led_config.point[index].y-g_led_config.point[wave_source].y;
        const uint16_t ax=dx<0?-dx:dx, ay=dy<0?-dy:dy;
        const uint16_t distance=style==1 ? ax+ay : style==3 ? (ax>ay ? ax : ay) : style==4 ? 0 : ax;
        const uint8_t width=redblade_config_byte(11,0) ? redblade_config_byte(11,0) : 16;
        if (style==3 && ax>=width && ay>=width) return base;
        const uint16_t gap=distance>boot_position ? distance-boot_position : boot_position-distance;
        if (style==4) { strength=boot_brightness; divisor=255; }
        else {
            if (gap>=width) return base;
            strength=(uint16_t)boot_brightness*(width-gap); divisor=255U*width;
        }
    } else {
        const uint8_t shift=(index & 1)*4, ttl=(typing_ttl[index/2]>>shift)&15;
        if (!ttl) return base;
        if (!boot_blend) {
            typing_ttl[index/2]-=1<<shift;
            if (ttl==1) --typing_count;
        }
        const uint8_t rate=redblade_config_byte(12,0) ? redblade_config_byte(12,0) : 1;
        const uint8_t subframe=boot_blend ? boot_blend-1 : rate-1;
        strength=(uint16_t)ttl*rate-subframe; divisor=(uint16_t)duration*rate;
    }
    hsv_t peak=redblade_config_effect(RB_COLOR_REACTIVE,(hsv_t){0,255,rgb_matrix_get_val()});
    if (peak.v>rgb_matrix_get_val()) peak.v=rgb_matrix_get_val();
    int16_t delta=(int16_t)peak.h-base.h;
    if (delta>127) delta-=256;
    if (delta<-128) delta+=256;
    base.h+=(int32_t)delta*strength/divisor;
    base.s+=(int32_t)((int16_t)peak.s-base.s)*strength/divisor;
    base.v+=(int32_t)((int16_t)peak.v-base.v)*strength/divisor;
    return base;
}

uint8_t redblade_rgb_profile_get(void) { return local_profile; }
uint8_t redblade_rgb_brightness_get(void) { return master_brightness; }
uint8_t redblade_rgb_settings_get(void) { return settings; }

void redblade_rgb_profile(uint8_t profile) {
    if (profile > RB_PROFILE_OFF) return;
    typing_clear();
    if (profile != RB_PROFILE_OFF) previous_profile = profile;
    local_profile = profile;
    boot_active = false;
    rgb_matrix_enable_noeeprom();
    rgb_matrix_mode_noeeprom(RGB_MATRIX_CUSTOM_REDBLADE_DIRECT);
}
void redblade_rgb_runtime(uint8_t profile,uint8_t master,uint8_t flags) {
    if (profile>RB_PROFILE_OFF || flags>7) return;
    const uint8_t restore_profile=previous_profile;
    master_brightness=master;settings=flags;redblade_rgb_profile(profile);
    previous_profile=restore_profile;
}
void redblade_rgb_apply_config(void) {
    rgb_matrix_sethsv_noeeprom(rgb_matrix_get_hue(),rgb_matrix_get_sat(),RGB_MATRIX_MAXIMUM_BRIGHTNESS);
    redblade_rgb_runtime(redblade_config_byte(3,0),redblade_config_byte(2,255),redblade_config_byte(0,7)&7);
    previous_profile=local_profile==RB_PROFILE_OFF ? RB_PROFILE_HOST : local_profile;
}

void redblade_rgb_cycle(int8_t direction) {
    if (!direction) return;
    const uint8_t profile = local_profile == RB_PROFILE_OFF ? previous_profile : local_profile;
    redblade_rgb_profile((profile + (direction > 0 ? 1 : 2)) % 3);
}

void redblade_rgb_toggle(void) {
    redblade_rgb_profile(local_profile == RB_PROFILE_OFF ? previous_profile : RB_PROFILE_OFF);
}

void redblade_rgb_brightness_step(int8_t direction) {
    const int16_t next = master_brightness + (direction > 0 ? 16 : direction < 0 ? -16 : 0);
    master_brightness = next < 0 ? 0 : next > 255 ? 255 : next;
}

void redblade_rgb_toggle_setting(uint8_t setting) {
    settings ^= setting & 7;
    if (!(settings & RB_SETTING_ANIMATION)) boot_active = false;
}

void redblade_rgb_defaults(void) {
    master_brightness = 255;
    settings = 7;
    rgb_matrix_sethsv_noeeprom(rgb_matrix_get_hue(), rgb_matrix_get_sat(), RGB_MATRIX_MAXIMUM_BRIGHTNESS);
    redblade_rgb_profile(RB_PROFILE_HOST);
}

bool redblade_rgb_save(void) {
    if (redblade_store_valid()) return redblade_store_save_lighting(local_profile,master_brightness,settings);
    // Dedicated QMK user word. Only the explicit Fn+F9 action writes it.
    // Game mode, VIA layers/macros and streaming colors are never stored here.
    const uint32_t word = 0x010000A7UL | ((uint32_t)local_profile << 19) | ((uint32_t)settings << 16) | ((uint32_t)master_brightness << 8);
    eeconfig_update_user(word);
    return eeconfig_read_user() == word;
}

void redblade_rgb_replay(void) {
    if (!redblade_rgb_output_active()) return;
    boot_started_at = timer_read32();
    boot_active = true;
}

bool redblade_rgb_output_active(void) {
    if (suspended || !rgb_matrix_is_enabled() || local_profile == RB_PROFILE_OFF || !master_brightness) return false;
    return !(rgb_matrix_get_mode() == RGB_MATRIX_CUSTOM_REDBLADE_DIRECT && local_profile == RB_PROFILE_HOST && idle_fallback && !(settings & RB_SETTING_IDLE_WHITE));
}

bool redblade_rgb_off_fn_available(void) {
    return !suspended && rgb_matrix_is_enabled() && local_profile == RB_PROFILE_OFF && master_brightness;
}

hsv_t redblade_rgb_apply_brightness(hsv_t color) {
    if (master_brightness != 255) color.v = ((uint16_t)color.v * master_brightness + 127) / 255;
    return color;
}

bool redblade_rgb_fallback_active(void) {
    return rgb_matrix_get_mode() == RGB_MATRIX_CUSTOM_REDBLADE_DIRECT && !boot_active && redblade_rgb_output_active() &&
        (local_profile == RB_PROFILE_WHITE || (local_profile == RB_PROFILE_HOST && idle_fallback && (settings & RB_SETTING_IDLE_WHITE)));
}

rgb_t redblade_rgb_convert(hsv_t color) {
    // OpenRGB sends brightness in every pixel. Applying QMK's CIE curve
    // again crushes dark host colors and the deliberately dim status LEDs.
    // Native QMK effects retain their own standard conversion.
    return hsv_to_rgb_nocie(color);
}

_Static_assert(REDBLADE_RGB_TIMEOUT_MS > 0, "A positive RGB watchdog timeout is required");
_Static_assert(REDBLADE_BOOT_ANIMATION_MS > 0, "A positive boot animation duration is required");
_Static_assert(REDBLADE_BOOT_BRIGHTNESS <= RGB_MATRIX_MAXIMUM_BRIGHTNESS, "Boot brightness exceeds board limit");
_Static_assert(REDBLADE_BOOT_ANIMATION_MS >= 10, "Boot animation must allow an entrance and handover phase");
_Static_assert(REDBLADE_FALLBACK_BRIGHTNESS <= RGB_MATRIX_MAXIMUM_BRIGHTNESS, "Fallback brightness exceeds board limit");
_Static_assert(REDBLADE_MEDIA_BRIGHTNESS <= RGB_MATRIX_MAXIMUM_BRIGHTNESS, "Media brightness exceeds board limit");

// Generated inverse of the manufacturer's LED matrix, in logical LED order.
// No matrix search is performed in the USB request handler.
#include "led_positions.inc"

static void forget_stream(void) {
    typing_clear();
    have_stream = false;
    render_kind = RENDER_OFF;
    memset(incoming, 0, sizeof(incoming));
}

void redblade_rgb_init(bool usb_active) {
    local_profile = previous_profile = RB_PROFILE_HOST;
    master_brightness = 255;
    settings = 7;
    const uint32_t word = eeconfig_read_user();
    if (redblade_store_valid()) {
        local_profile=redblade_config_byte(3,0);
        previous_profile=local_profile==RB_PROFILE_OFF ? RB_PROFILE_HOST : local_profile;
        master_brightness=redblade_config_byte(2,255);settings=redblade_config_byte(0,7)&7;
        rgb_matrix_enable_noeeprom();rgb_matrix_mode_noeeprom(RGB_MATRIX_CUSTOM_REDBLADE_DIRECT);
        rgb_matrix_sethsv_noeeprom(rgb_matrix_get_hue(),rgb_matrix_get_sat(),RGB_MATRIX_MAXIMUM_BRIGHTNESS);
    } else if ((word & 0xFFE000FFUL) == 0x010000A7UL) {
        local_profile = (word >> 19) & 3;
        previous_profile = local_profile == RB_PROFILE_OFF ? RB_PROFILE_HOST : local_profile;
        settings = (word >> 16) & 7;
        master_brightness = word >> 8;
        if (local_profile != RB_PROFILE_HOST) {
            rgb_matrix_enable_noeeprom();
            rgb_matrix_mode_noeeprom(RGB_MATRIX_CUSTOM_REDBLADE_DIRECT);
        }
    }
    forget_stream();
    received_packets = 0;
    idle_fallback = false;
    suspended = !usb_active;
    boot_started_at = last_packet_at = timer_read32();
    // Respect an explicitly disabled light or a saved standalone effect.
    boot_active = usb_active && (settings & RB_SETTING_ANIMATION) && local_profile != RB_PROFILE_OFF && rgb_matrix_is_enabled() && rgb_matrix_get_mode() == RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
}

void redblade_rgb_suspend(void) {
    if (suspended) return;
    suspended = true;
    boot_active = false;
    forget_stream();
    // RGB_MATRIX_SLEEP in QMK flushes black without stopping the shared
    // matrix/PWM timer. Stopping that timer would also stop key scanning.
}

void redblade_rgb_wake(void) {
    if (!suspended) return; // USB LED/protocol notifications are not new boots.
    suspended = false;
    forget_stream(); // Never replay a pre-sleep frame.
    idle_fallback = false;
    boot_started_at = last_packet_at = timer_read32();
    boot_active = (settings & RB_SETTING_ANIMATION) && local_profile != RB_PROFILE_OFF && rgb_matrix_is_enabled() && rgb_matrix_get_mode() == RGB_MATRIX_CUSTOM_REDBLADE_DIRECT;
}

void redblade_rgb_begin_frame(void) {
    // Once per RGB frame, outside the keyboard scan and USB packet handler.
    const uint32_t now = timer_read32();
    render_kind = RENDER_OFF;
    if (!redblade_rgb_output_active() || rgb_matrix_get_mode() != RGB_MATRIX_CUSTOM_REDBLADE_DIRECT) { typing_clear(); return; }
    const uint8_t brightness = rgb_matrix_get_val();
    manual_brightness = brightness;
    boot_brightness = brightness < REDBLADE_BOOT_BRIGHTNESS ? brightness : REDBLADE_BOOT_BRIGHTNESS;
    fallback_brightness = brightness < REDBLADE_FALLBACK_BRIGHTNESS ? brightness : REDBLADE_FALLBACK_BRIGHTNESS;
    media_brightness = brightness < REDBLADE_MEDIA_BRIGHTNESS ? brightness : REDBLADE_MEDIA_BRIGHTNESS;
    if (!idle_fallback && now - last_packet_at >= redblade_config_timeout()) {
        forget_stream();
        idle_fallback = true;
    }
    render_target = local_profile == RB_PROFILE_WHITE ? RENDER_LOCAL_WHITE : local_profile == RB_PROFILE_RED ? RENDER_RED : have_stream ? RENDER_DIRECT : idle_fallback ? ((settings & RB_SETTING_IDLE_WHITE) ? RENDER_WHITE : RENDER_OFF) : RENDER_WAITING;
    if (boot_active) {
        const uint32_t elapsed = now - boot_started_at;
        if (elapsed < REDBLADE_BOOT_ANIMATION_MS) {
            render_kind = RENDER_BOOT;
            const uint32_t sweep_ms = REDBLADE_BOOT_ANIMATION_MS * 4 / 5;
            boot_position = elapsed * 384UL / sweep_ms;
            // Soft entrance, then HSV fade to the latest host colors in the final 20%.
            if (elapsed < REDBLADE_BOOT_ANIMATION_MS / 10) boot_brightness = boot_brightness * elapsed / (REDBLADE_BOOT_ANIMATION_MS / 10);
            boot_blend = elapsed < sweep_ms ? 0 : (elapsed - sweep_ms) * 255UL / (REDBLADE_BOOT_ANIMATION_MS - sweep_ms);
            return;
        }
        boot_active = false;
    }
    render_kind = render_target;
    if ((render_kind != RENDER_WHITE && render_kind != RENDER_LOCAL_WHITE) || !brightness || !redblade_config_feature(RB_MODULE_REACTIVE)) typing_clear();
    else {
        const uint8_t rate=redblade_config_byte(12,0);
        if (wave_ttl && (wave_ttl > redblade_config_byte(7,15) || !boot_blend)) --wave_ttl;
        const uint8_t step=rate ? rate : 1;
        if (++boot_blend >= step) boot_blend=0;
        if (wave_ttl) {
            // Reuse boot-only fields; compute smooth geometry once per frame, not per LED.
            const uint8_t total=redblade_config_byte(7,15)*step;
            const uint8_t remaining=wave_ttl*step-(boot_blend ? boot_blend-1 : step-1);
            const uint8_t elapsed=total-remaining;
            boot_position=(uint16_t)elapsed*16/step;
            if (redblade_config_byte(10,0)==4 && total>2) {
                const uint16_t phase=(uint16_t)elapsed*4;
                const uint8_t fraction=phase%total;
                const uint8_t triangle=((phase/total)&1) ? total-fraction : fraction;
                boot_brightness=(uint32_t)triangle*remaining*255/((uint16_t)total*total);
            } else boot_brightness=(uint16_t)remaining*255/total;
        }
    }
}

static hsv_t target_color(uint8_t index, uint8_t kind) {
    hsv_t color = {0, 0, 0};
    if (kind == RENDER_OFF) return color;
    if (kind == RENDER_WHITE || kind == RENDER_LOCAL_WHITE) return typing_color(index, fallback_brightness);
    if (kind == RENDER_RED) {
        color=redblade_config_effect(RB_COLOR_ACCENT,(hsv_t){0,255,manual_brightness});
        if (color.v>manual_brightness) color.v=manual_brightness;
        return color;
    }
    if (kind == RENDER_DIRECT) color = incoming[index];
    // Manufacturer LED indices 16..19: Calc, Mute, Volume-, Volume+.
    // A black host color is the fallback sentinel for these four keys only.
    if ((settings & RB_SETTING_MEDIA) && color.v == 0 && index >= 16 && index <= 19) {
        color=redblade_config_effect(RB_COLOR_MEDIA,(hsv_t){0,0,media_brightness});
        if (color.v>rgb_matrix_get_val()) color.v=rgb_matrix_get_val();
    }
    return color;
}

hsv_t redblade_rgb_color(uint8_t index) {
    if (render_kind != RENDER_BOOT) return redblade_rgb_apply_brightness(target_color(index, render_kind));
    hsv_t color = {0, 0, 0};
    if (render_kind == RENDER_BOOT) {
        const hsv_t wave=redblade_config_effect(RB_COLOR_WAVE,(hsv_t){0,255,255});
        const uint8_t peak=boot_brightness<wave.v ? boot_brightness : wave.v;
        // Curved wavefront: each row offsets the crest; trailing edge fades
        // over 112 units while the front edge is crisp (32 units).
        const int16_t crest = (int16_t)boot_position - 64 - ((int16_t)g_led_config.point[index].y - 32) * ((int16_t)g_led_config.point[index].y - 32) / 96;
        const int16_t behind = crest - g_led_config.point[index].x;
        color.h = wave.h; color.s = wave.s;
        color.v = peak / 12;
        if (behind >= -32 && behind < 112) {
            const uint8_t strength = behind < 0 ? (behind + 32) * 4 : (112 - behind) * 128 / 112;
            color.v = color.v + strength * (peak - color.v) / 128;
        }
        if (boot_blend) {
            const hsv_t target = target_color(index, render_target);
            int16_t hue_delta = (int16_t)target.h - color.h;
            if (hue_delta > 127) hue_delta -= 256;
            if (hue_delta < -128) hue_delta += 256;
            color.h += hue_delta * boot_blend / 255;
            color.s += ((int16_t)target.s - color.s) * boot_blend / 255;
            color.v += ((int16_t)target.v - color.v) * boot_blend / 255;
        }
    }
    return redblade_rgb_apply_brightness(color);
}

static uint16_t read16(const uint8_t *p) {
    return (uint16_t)p[0] | ((uint16_t)p[1] << 8);
}

static void write16(uint8_t *p, uint16_t n) {
    p[0] = n;
    p[1] = n >> 8;
}

static void write32(uint8_t *p, uint32_t n) {
    write16(p, n);
    write16(p + 2, n >> 16);
}

static uint16_t current_mode(void) {
    if (!rgb_matrix_is_enabled()) return 0;
    return rgb_matrix_get_mode() == RGB_MATRIX_CUSTOM_REDBLADE_DIRECT ? 1 : 2;
}

// This intercepts only the VialRGB lighting extension and its discovery
// request. All standard VIA commands continue through upstream QMK.
bool via_command_kb(uint8_t *data, uint8_t length) {
    if (length != REDBLADE_REPORT_SIZE) return false;
    if (data[0]==0xFE && data[1]==0x90) return redblade_store_command(data,length);

    if (data[0] == 0xFE && data[1] == 0x00) {
        // Vial discovery envelope required by OpenRGB, RGB capability only.
        // Full Vial remapping / unlock protocol is deliberately not advertised
        // as a supported UI: this firmware retains the standard VIA UI.
        memset(data, 0, length);
        data[0] = 4; // little-endian discovery protocol version
        memcpy(data + 4, "RDBLFS01", 8);
        data[12] = 1; // VialRGB capability
        raw_hid_send(data, length);
        return true;
    }

    if (data[0] == 0xFE && data[1] == 0x80) {
        // Private read-only diagnostics, separate from VialRGB commands.
        const uint32_t now = timer_read32();
        const uint32_t age = have_stream ? now - last_packet_at : UINT32_MAX;
        memset(data + 2, 0, length - 2);
        data[2] = 1; // diagnostics format
        data[3] = REDBLADE_VERSION_MAJOR; data[4] = REDBLADE_VERSION_MINOR; data[5] = REDBLADE_VERSION_PATCH;
        data[6] = (have_stream && age < redblade_config_timeout() && !suspended ? 1 : 0) |
                  (boot_active && now - boot_started_at < REDBLADE_BOOT_ANIMATION_MS ? 2 : 0) |
                  (suspended ? 4 : 0) | (rgb_matrix_is_enabled() ? 8 : 0);
        data[7] = current_mode();
        write32(data + 8, age);
        write32(data + 12, redblade_config_timeout());
        write32(data + 16, received_packets);
        write32(data + 20, REDBLADE_BOOT_ANIMATION_MS);
        memcpy(data + 24, "RDBLFS01", 8);
        raw_hid_send(data, length);
        return true;
    }

    if (data[0] == 0xFE && data[1] == 0x81) {
        memset(data + 2, 0, length - 2);
        data[2] = 1;
        data[3] = REDBLADE_VERSION_MAJOR; data[4] = REDBLADE_VERSION_MINOR; data[5] = REDBLADE_VERSION_PATCH;
        data[6] = redblade_game_mode();
        data[7] = local_profile; data[8] = master_brightness; data[9] = settings;
        write16(data + 10, REDBLADE_BOOT_HOLD_MS);
        write32(data + 12, redblade_config_timeout());
        data[16] = redblade_turbo_key(); data[17] = redblade_turbo_rate(); data[18] = redblade_auto_w();
        data[19] = (redblade_turbo_selecting() ? 1 : 0) | (redblade_guide_active() ? 2 : 0) | (redblade_recorder_active() ? 4 : 0) | (redblade_recorder_rate_context() ? 8 : 0);
        write32(data + 20, REDBLADE_BOOT_ANIMATION_MS);
        memcpy(data + 24, "RDBLFS01", 8);
        raw_hid_send(data, length);
        return true;
    }

    if (data[0] == 0xFE && data[1] == 0x82) {
        // Metadata only. Recorded keycodes/timing payload are never exposed.
        memset(data + 2, 0, length - 2); data[2] = 1;
        data[3] = REDBLADE_VERSION_MAJOR; data[4] = REDBLADE_VERSION_MINOR; data[5] = REDBLADE_VERSION_PATCH;
        data[6] = redblade_recorder_state(); data[7] = redblade_recorder_selected();
        data[8] = redblade_recorder_speed_quarters(); data[9] = redblade_recorder_error();
        for (uint8_t i = 0; i < 3; ++i) write16(data + 10 + i * 2, redblade_recorder_slot_bytes(i));
        write16(data + 16, redblade_recorder_used()); write16(data + 18, REDBLADE_RECORDER_BYTES);
        write16(data + 20, REDBLADE_RECORDER_MAX_MS); data[22] = redblade_recorder_loop(); memcpy(data + 24, "RDBLFS01", 8);
        raw_hid_send(data, length); return true;
    }

    const uint8_t command = data[0];
    const uint8_t subcommand = data[1];
    if ((command != 0x07 && command != 0x08) || subcommand < 0x40 || subcommand > 0x44) return false;
    uint8_t *args = data + 2;

    if (command == 0x08) {
        const uint16_t requested = read16(args);
        memset(args, 0, length - 2);
        switch (subcommand) {
            case 0x40: // RGB protocol and maximum brightness
                write16(args, 1);
                args[2] = RGB_MATRIX_MAXIMUM_BRIGHTNESS;
                break;
            case 0x41:
                write16(args, current_mode());
                args[2] = rgb_matrix_get_speed();
                args[3] = rgb_matrix_get_hue();
                args[4] = rgb_matrix_get_sat();
                args[5] = rgb_matrix_get_val();
                break;
            case 0x42: { // supported modes, strictly greater than request
                memset(args, 0xFF, length - 2);
                uint8_t offset = 0;
                for (uint16_t mode = 1; mode <= 2; ++mode) {
                    if (mode > requested) {
                        write16(args + offset, mode);
                        offset += 2;
                    }
                }
                break;
            }
            case 0x43:
                write16(args, RGB_MATRIX_LED_COUNT);
                break;
            case 0x44:
                if (requested < RGB_MATRIX_LED_COUNT) {
                    args[0] = g_led_config.point[requested].x;
                    args[1] = g_led_config.point[requested].y;
                    args[2] = g_led_config.flags[requested];
                    args[3] = led_positions[requested][0];
                    args[4] = led_positions[requested][1];
                } else {
                    data[0] = 0xFF;
                }
                break;
        }
    } else if (subcommand == 0x41) {
        const uint16_t mode = read16(args);
        if (mode > 2) {
            data[0] = 0xFF;
        } else {
            if (mode != 1) { boot_active = false; typing_clear(); } // Explicit Off/standalone takes priority.
            if (mode == 2) local_profile = previous_profile = RB_PROFILE_HOST;
            if (mode == 0) {
                rgb_matrix_disable_noeeprom();
            } else {
                rgb_matrix_enable_noeeprom();
                rgb_matrix_mode_noeeprom(mode == 1 ? RGB_MATRIX_CUSTOM_REDBLADE_DIRECT : RGB_MATRIX_SOLID_COLOR);
            }
            // Direct mode has per-pixel colors, no global HSV/brightness
            // control. OpenRGB legitimately fills the unused fields with 0.
            // Do not let those zeros black out local waves/media/status LEDs.
            if (mode == 2) {
                rgb_matrix_set_speed_noeeprom(args[2]);
                rgb_matrix_sethsv_noeeprom(args[3], args[4], args[5]);
            }
        }
    } else if (subcommand == 0x42) {
        const uint16_t first = read16(args);
        const uint8_t count = args[2];
        // Validate the complete request before touching either color buffer.
        if (count == 0 || count > 9 || first >= RGB_MATRIX_LED_COUNT || count > RGB_MATRIX_LED_COUNT - first) {
            data[0] = 0xFF;
        } else {
            for (uint8_t i = 0; i < count; ++i) {
                incoming[first + i].h = args[3 + i * 3];
                incoming[first + i].s = args[4 + i * 3];
                uint8_t value = args[5 + i * 3];
#if RGB_MATRIX_MAXIMUM_BRIGHTNESS < 255
                if (value > RGB_MATRIX_MAXIMUM_BRIGHTNESS) value = RGB_MATRIX_MAXIMUM_BRIGHTNESS;
#endif
                incoming[first + i].v = value;
            }
            last_packet_at = timer_read32();
            if (local_profile == RB_PROFILE_HOST) typing_clear();
            have_stream = true;
            idle_fallback = false;
            // Keep accepting frames during the nonblocking boot wave, so its
            // final fade hands over to fresh colors rather than an old image.
            ++received_packets;
        }
    } else {
        data[0] = 0xFF;
    }
    raw_hid_send(data, length);
    return true;
}
