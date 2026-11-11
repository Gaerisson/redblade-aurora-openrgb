// SPDX-License-Identifier: GPL-2.0-or-later
#include "redblade_controls.h"
#include "redblade_rgb.h"
#include "redblade_recorder.h"
#include "redblade_config.h"
#include "redblade_store.h"
#include "dynamic_keymap.h"
#include "report.h"
#include "host.h"

_Static_assert(RB_GAME == 0x7E40 && RB_BOOT == 0x7E53, "VIA custom keycode ABI changed");
_Static_assert(RB_TURBO_RATE == 0x7E58 && RB_TURBO_FASTER == 0x7E5B, "Game helper keycode ABI changed");
static bool game_mode, boot_armed, feedback_active;
static bool recorder_rate_context;
static bool caps_word, caps_weak;
static uint32_t turbo_started_at, forward_started_at, playback_started_at;
static uint32_t boot_started_at, feedback_started_at;
static keypos_t boot_key;
static uint8_t media_held;
static struct { uint8_t code, row, col; } media[3];
static uint8_t turbo_key, turbo_led = 255, turbo_rate = 50, turbo_interval = 10;
static uint8_t turbo_base_ms = 10, turbo_remainder, trigger_held;
static uint16_t turbo_fraction;
static int8_t rate_direction;
static bool rate_repeating;
static keypos_t rate_key;
static uint32_t rate_at;
static bool turbo_down, auto_w, selecting;
static uint8_t pending_key, consumed_releases[(MATRIX_ROWS * MATRIX_COLS + 7) / 8];
static uint32_t turbo_edge_at, selection_started_at;
static keypos_t trigger[3];
// Shipped Fn shortcuts plus Fn itself. Flash-only: no EEPROM reads or new RAM
// in the eight-LED rendering path. check_fn_map.py verifies this against VIA's
// published default bindings and the pinned physical LED positions.
static const uint8_t PROGMEM fn_shortcut_led_mask[] = {
    0xFF,0x83,0xEF,0x00,0x12,0x79,0x00,0x31,0x02,0x00,0x00,0x00,0x6A,0x05
};
_Static_assert(sizeof(fn_shortcut_led_mask) == (RGB_MATRIX_LED_COUNT + 7) / 8, "Fn LED mask size differs from the board");
static bool any_turbo_enabled(void) {
    return redblade_config_feature(RB_MODULE_KEY_TURBO) || redblade_config_feature(RB_MODULE_MOUSE_TURBO);
}
static bool shortcut_enabled(uint8_t index) {
    if (index>=109 || !(pgm_read_byte(&fn_shortcut_led_mask[index/8]) & (1U<<(index%8)))) return false;
    if (index==RB_LED_E || index==RB_LED_F || index==RB_LED_SPACE) return redblade_config_feature(RB_MODULE_KEY_TURBO);
    if (index==RB_LED_CAPS) return redblade_config_feature(RB_MODULE_CAPS_WORD);
    if (index==RB_LED_W) return redblade_config_feature(RB_MODULE_FORWARD);
    if (index==RB_LED_T || index==RB_LED_R) return any_turbo_enabled();
    if (index==RB_LED_MINUS || index==RB_LED_PLUS) return any_turbo_enabled() || redblade_config_feature(RB_MODULE_RECORDER);
    if (index==16 || (index>=21 && index<=23)) return redblade_config_feature(RB_MODULE_RECORDER);
    if (index==RB_LED_LGUI || index==RB_LED_RGUI) return redblade_config_feature(RB_MODULE_WIN_LOCK);
    return true;
}
static bool action_enabled(uint16_t code) {
    if (code==RB_TURBO_E || code==RB_TURBO_F || code==RB_TURBO_SPACE) return redblade_config_feature(RB_MODULE_KEY_TURBO);
    if (code==RB_TURBO_SELECT || code==RB_TURBO_RATE) return any_turbo_enabled();
    if (code==RB_TURBO_SLOWER || code==RB_TURBO_FASTER) return any_turbo_enabled() || redblade_config_feature(RB_MODULE_RECORDER);
    if (code==RB_AUTO_W) return redblade_config_feature(RB_MODULE_FORWARD);
    if (code>=RB_REC_ARM && code<=RB_REC_3) return redblade_config_feature(RB_MODULE_RECORDER);
    if (code==RB_CAPS_WORD) return redblade_config_feature(RB_MODULE_CAPS_WORD);
    if (code==RB_GAME) return redblade_config_feature(RB_MODULE_WIN_LOCK);
    return true;
}

uint8_t redblade_turbo_key(void) { return turbo_key; }
uint8_t redblade_turbo_rate(void) { return turbo_rate; }
uint8_t redblade_turbo_led(void) { return turbo_led; }
bool redblade_turbo_selecting(void) { return selecting || pending_key; }
bool redblade_auto_w(void) { return auto_w; }
bool redblade_guide_active(void) { return false; } // Retired, keep diagnostic ABI.
bool redblade_recorder_rate_context(void) { return recorder_rate_context; }
static bool mouse_target(void) { return turbo_key == 0xD1 || turbo_key == 0xD2; }
static void turbo_output(bool down) {
    if (mouse_target()) {
        report_mouse_t report = {0};
        report.buttons = down ? (turbo_key == 0xD1 ? 1 : 2) : 0;
        host_mouse_send(&report);
    } else if (down) register_code(turbo_key); else unregister_code(turbo_key);
}

static void stop_turbo(void) {
    if (turbo_down) turbo_output(false);
    turbo_key = 0; turbo_led = 255; turbo_down = false;
}
static void stop_w(void) { if (auto_w) unregister_code(KC_W); auto_w = false; }
static void caps_clear_shift(void) {
    if (!caps_weak) return;
    caps_weak=false;
    if (get_weak_mods() & 2) { del_weak_mods(2); send_keyboard_report(); }
}
static void caps_stop(void) { caps_clear_shift(); caps_word=false; }
static void stop_automation(void) { caps_stop(); stop_turbo(); stop_w(); selecting = false; pending_key = 0; rate_direction = 0; redblade_recorder_stop(true); }

static void set_rate(uint8_t rate) {
    turbo_rate = rate;
    // Divide only on a human control action; distribute fractional ms across
    // later edges so a requested 60/s does not become 62.5/s.
    turbo_base_ms = 500 / rate; turbo_remainder = 500 % rate;
    turbo_interval = turbo_base_ms; turbo_fraction = 0; turbo_edge_at = timer_read32();
}
static void rate_step(int8_t direction) {
    if (recorder_rate_context) redblade_recorder_speed_step(direction);
    else if (direction > 0) set_rate(turbo_rate < 100 ? turbo_rate + 5 : turbo_rate < 250 ? turbo_rate + 25 : 250);
    else set_rate(turbo_rate > 100 ? turbo_rate - 25 : turbo_rate > 5 ? turbo_rate - 5 : 5);
}
static void rate_begin(int8_t direction, keypos_t pos) {
    rate_key = pos; rate_direction = direction; rate_repeating = false;
    rate_at = timer_read32(); rate_step(direction);
}
static uint8_t led_at(keypos_t pos) {
    return pos.row < MATRIX_ROWS && pos.col < MATRIX_COLS ? g_led_config.matrix_co[pos.row][pos.col] : 255;
}
static void start_turbo(uint8_t key, uint8_t led) {
    if (key == KC_W) stop_w(); // Exactly one owner for a generated usage.
    if (is_key_pressed(key)) return; // Never steal a physically held target.
    stop_turbo(); caps_stop();
    turbo_started_at = timer_read32();
    turbo_key = key; turbo_led = led; turbo_down = true;
    turbo_fraction = 0; turbo_interval = turbo_base_ms; turbo_edge_at = timer_read32();
    turbo_output(true);
    redblade_rgb_key_flash(led);
}
static void toggle_turbo(uint8_t key, keypos_t pos) {
    rate_direction = 0;
    redblade_recorder_stop(true); recorder_rate_context = false;
    trigger[0] = pos; trigger_held |= 1; selecting = false; pending_key = 0;
    if (turbo_key == key) { stop_turbo(); return; }
    start_turbo(key, led_at(pos));
}
bool redblade_game_mode(void) { return game_mode; }
static void game_set(bool enabled) {
    game_mode = enabled;
    // The shared Red Blade / Ironclad PCB uses this active-low B12 status LED.
    // Upstream RBFS calls it LED_FN_PIN; its physical legend is Windows lock.
    gpio_write_pin(LED_FN_PIN, !enabled);
    if (enabled) { redblade_recorder_stop(true); del_mods(MOD_MASK_GUI); del_weak_mods(MOD_MASK_GUI); send_keyboard_report(); }
}
void redblade_controls_init(void) {
    stop_automation(); game_mode = boot_armed = feedback_active = false;
    gpio_write_pin(LED_FN_PIN, true);
    redblade_recorder_init(); recorder_rate_context = false;
    media_held = trigger_held = 0;
    for (uint8_t i = 0; i < sizeof(consumed_releases); ++i) consumed_releases[i] = 0;
    set_rate(redblade_config_byte(5,50));
    redblade_recorder_speed_configure(redblade_config_byte(6,2));
    recorder_rate_context=!any_turbo_enabled() && redblade_config_feature(RB_MODULE_RECORDER);
}
void redblade_controls_apply_config(void) {
    redblade_controls_suspend();
    set_rate(redblade_config_byte(5,50));
    redblade_recorder_speed_configure(redblade_config_byte(6,2));
    recorder_rate_context=!any_turbo_enabled() && redblade_config_feature(RB_MODULE_RECORDER);
}
void redblade_controls_suspend(void) {
    boot_armed = feedback_active = game_mode = false; stop_automation(); trigger_held = 0;
    gpio_write_pin(LED_FN_PIN, true);
    redblade_recorder_suspend(); recorder_rate_context = false;
    for (uint8_t i = 0; i < sizeof(consumed_releases); ++i) consumed_releases[i] = 0;
    for (uint8_t i = 0; media_held && i < 3; ++i) {
        if ((media_held & (1 << i)) && media[i].code) unregister_code(media[i].code);
    }
    media_held = 0;
}
void redblade_controls_task(bool fn_active) {
    if (feedback_active && feedback_started_at==UINT32_MAX && !redblade_store_busy()) {
        if (redblade_store_error()) feedback_active=false;
        else feedback_started_at=timer_read32();
    }
    if (rate_direction && timer_read32() - rate_at >= (rate_repeating ? 150UL : 400UL)) {
        rate_step(rate_direction); rate_at = timer_read32(); rate_repeating = true;
    }
    if (redblade_config_feature(RB_MODULE_REPEAT_TIMEOUT)) {
        const uint32_t now=timer_read32();
        uint8_t seconds=redblade_config_byte(8,30); if (!seconds) seconds=30;
        const uint32_t limit=(uint32_t)seconds*1000UL;
        if (turbo_key && now-turbo_started_at>=limit) stop_turbo();
        if (auto_w && now-forward_started_at>=limit) stop_w();
        if (redblade_recorder_state()==RB_REC_PLAYING && now-playback_started_at>=limit) redblade_recorder_stop(true);
    }
    redblade_recorder_task(fn_active);
    if (!boot_armed && !turbo_key && !selecting && !pending_key) return;
    const uint32_t now = timer_read32();
    if ((selecting || pending_key) && now - selection_started_at >= REDBLADE_TURBO_SELECT_TIMEOUT_MS) {
        selecting = false; pending_key = 0;
    }
    if (boot_armed && !fn_active) boot_armed = false;
    if (boot_armed && now - boot_started_at >= REDBLADE_BOOT_HOLD_MS) {
        boot_armed = false; reset_keyboard(); return;
    }
    if (turbo_key && now - turbo_edge_at >= turbo_interval) {
        // At most one edge per pass. Drop missed deadlines, never catch up.
        turbo_edge_at = now;
        turbo_output(!turbo_down);
        if (!turbo_down) redblade_rgb_key_flash(turbo_led);
        turbo_down = !turbo_down; turbo_interval = turbo_base_ms;
        turbo_fraction += turbo_remainder;
        if (turbo_fraction >= turbo_rate) { ++turbo_interval; turbo_fraction -= turbo_rate; }
    }
}
bool redblade_controls_process(uint16_t keycode, keyrecord_t *record) {
    const keypos_t pos = record->event.key;
    const bool valid_pos = pos.row < MATRIX_ROWS && pos.col < MATRIX_COLS;
    const uint8_t position = valid_pos ? pos.row * MATRIX_COLS + pos.col : 0;
    if (!record->event.pressed) {
        if (rate_direction && pos.row == rate_key.row && pos.col == rate_key.col) rate_direction = 0;
        if (boot_armed && pos.row == boot_key.row && pos.col == boot_key.col) boot_armed = false;
        if (valid_pos && (consumed_releases[position / 8] & (1 << (position % 8)))) {
            consumed_releases[position / 8] &= ~(1 << (position % 8));
            if (pending_key && pos.row == trigger[0].row && pos.col == trigger[0].col) {
                const uint8_t key = pending_key; pending_key = 0; start_turbo(key, led_at(pos));
            }
            return false;
        }
        for (uint8_t i = 0; trigger_held && i < 3; ++i) {
            if ((trigger_held & (1 << i)) && pos.row == trigger[i].row && pos.col == trigger[i].col) {
                trigger_held &= ~(1 << i); return false;
            }
        }
        if (boot_armed && pos.row == boot_key.row && pos.col == boot_key.col) boot_armed = false;
        for (uint8_t i = 0; media_held && i < 3; ++i) {
            if ((media_held & (1 << i)) && pos.row == media[i].row && pos.col == media[i].col) {
                if (media[i].code) unregister_code(media[i].code);
                media_held &= ~(1 << i); return false;
            }
        }
    }
    if (record->event.pressed && valid_pos && (keycode == 0x57 || keycode == 0x56) &&
        (turbo_key || redblade_recorder_state() == RB_REC_PLAYING)) {
        consumed_releases[position / 8] |= 1 << (position % 8);
        rate_begin(keycode == 0x57 ? 1 : -1, pos); return false;
    }
    if (keycode == KC_ESC && record->event.pressed) stop_automation();
    if (selecting && record->event.pressed && keycode != RB_BOOT && keycode != RB_TURBO_SELECT && keycode != RB_REC_ARM && keycode != KC_ESC) {
        if (!valid_pos) return false;
        // Only resolve VIA's base layer while choosing; with Fn still held,
        // F1 selects F1 rather than changing the lighting profile.
        const uint16_t base = dynamic_keymap_get_keycode(0, pos.row, pos.col);
        if (redblade_config_feature(RB_MODULE_MOUSE_TURBO) && (base == 0xAA || base == 0xA9) && pos.row == 0 && (pos.col == 19 || pos.col == 20)) {
            selecting = false; pending_key = base == 0xAA ? 0xD1 : 0xD2;
            selection_started_at = timer_read32(); trigger[0] = pos;
            consumed_releases[position / 8] |= 1 << (position % 8);
            return false; // Volume-key selection never changes audio or track.
        }
        if (redblade_config_feature(RB_MODULE_KEY_TURBO) && base >= 4 && base <= 0xA4 && base != KC_ESC && base != 0x66 && !is_key_pressed(base)) {
            selecting = false; pending_key = base; selection_started_at = timer_read32(); trigger[0] = pos;
            consumed_releases[position / 8] |= 1 << (position % 8);
            return false; // Start on release; selection itself never types.
        }
        if (!((base >= 0xE0 && base <= 0xE7) || (base >= QK_MOMENTARY && base <= QK_MOMENTARY_MAX))) {
            consumed_releases[position / 8] |= 1 << (position % 8);
            return false; // Unsupported system/consumer/macro actions do not execute.
        }
    }
    if (record->event.pressed && mouse_target() && led_at(pos) == turbo_led) {
        stop_turbo(); rate_direction = 0;
        if (valid_pos) consumed_releases[position / 8] |= 1 << (position % 8);
        return false; // The active volume target doubles as a local stop.
    }
    if (record->event.pressed && keycode == turbo_key) stop_turbo();
    if (record->event.pressed && keycode == KC_W) stop_w();
    if (game_mode && (keycode == KC_LGUI || keycode == KC_RGUI ||
        (keycode >= QK_MODS && keycode <= QK_MODS_MAX && ((QK_MODS_GET_MODS(keycode) & 8) ||
        QK_MODS_GET_BASIC_KEYCODE(keycode) == KC_LGUI || QK_MODS_GET_BASIC_KEYCODE(keycode) == KC_RGUI)))) return false;
    if (keycode < RB_GAME || keycode > RB_CAPS_WORD) {
        if (record->event.pressed && keycode >= 4 && keycode <= 255) rate_direction = 0;
        if (record->event.pressed && caps_word) {
            caps_clear_shift();
            if ((get_mods() & ~(MOD_MASK_SHIFT | 0x40)) != 0) caps_stop();
            // HID M is comma on the board's AZERTY layout; HID semicolon is M.
            else if ((keycode >= 4 && keycode <= 29 && (keycode != 0x10 || redblade_config_byte(9,0))) ||
                     (keycode == 0x33 && !redblade_config_byte(9,0))) { add_weak_mods(2); caps_weak=true; }
            else if (!((keycode >= 30 && keycode <= 39) || keycode==0x2A || keycode==0x4C || keycode==0xE1 || keycode==0xE5 || keycode==0xE6 || (keycode>=QK_MOMENTARY && keycode<=QK_MOMENTARY_MAX))) caps_stop();
        }
        if (redblade_config_feature(RB_MODULE_RECORDER)) redblade_recorder_key(keycode, record->event.pressed);
        if (record->event.pressed && keycode >= 4 && keycode < 0xA5) redblade_rgb_key_flash(led_at(pos));
        return true;
    }
    if (!record->event.pressed) return false;
    if (!action_enabled(keycode)) return false;
    if (keycode != RB_TURBO_SLOWER && keycode != RB_TURBO_FASTER) rate_direction = 0;
    const bool shift = (get_mods() & MOD_MASK_SHIFT) != 0;
    if (keycode >= RB_PLAY && keycode <= RB_NEXT) {
        const uint8_t i = keycode - RB_PLAY;
        if (media_held & (1 << i)) return false;
        media_held |= 1 << i; media[i].row = pos.row; media[i].col = pos.col; media[i].code = 0;
        if (shift && i != 0) redblade_rgb_brightness_step(i == 1 ? -1 : 1);
        else {
            media[i].code = i == 0 ? (shift ? KC_MSTP : KC_MPLY) : i == 1 ? KC_MPRV : KC_MNXT;
            register_code(media[i].code);
        }
        return false;
    }
    // Every non-trigger Fn action owns its release too. In particular a live
    // rate adjustment must not release a repeated numpad key if Fn goes up first.
    if (valid_pos && keycode != RB_TURBO_E && keycode != RB_TURBO_F && keycode != RB_TURBO_SPACE && keycode != RB_AUTO_W && keycode != RB_TURBO_SELECT) {
        consumed_releases[position / 8] |= 1 << (position % 8);
    }
    switch (keycode) {
        case RB_CAPS_WORD: if (caps_word) caps_stop(); else caps_word=true; break;
        case RB_GAME: game_set(shift ? false : !game_mode); break;
        case RB_HOST: redblade_rgb_profile(RB_PROFILE_HOST); break;
        case RB_WHITE: redblade_rgb_profile(RB_PROFILE_WHITE); break;
        case RB_RED: redblade_rgb_profile(RB_PROFILE_RED); break;
        case RB_DARK: redblade_rgb_profile(RB_PROFILE_OFF); break;
        case RB_LIGHT_TOGGLE: redblade_rgb_toggle(); break;
        case RB_BRIGHTER: redblade_rgb_brightness_step(1); break;
        case RB_DIMMER: redblade_rgb_brightness_step(-1); break;
        case RB_WAVE: redblade_rgb_replay(); break;
        case RB_ANIMATION: redblade_rgb_toggle_setting(RB_SETTING_ANIMATION); break;
        case RB_MEDIA_FALLBACK: redblade_rgb_toggle_setting(RB_SETTING_MEDIA); break;
        case RB_IDLE_FALLBACK: redblade_rgb_toggle_setting(RB_SETTING_IDLE_WHITE); break;
        case RB_DEFAULTS: redblade_rgb_defaults(); game_set(false); stop_automation(); set_rate(50); redblade_recorder_speed_reset(); recorder_rate_context = false; break;
        case RB_PROFILE_NEXT: redblade_rgb_cycle(1); break;
        case RB_PROFILE_PREV: redblade_rgb_cycle(-1); break;
        case RB_SAVE:
            if (redblade_rgb_save()) { feedback_started_at = redblade_store_busy() ? UINT32_MAX : timer_read32(); feedback_active = true; }
            break;
        case RB_BOOT:
            stop_automation(); game_set(false);
            if (!boot_armed) { boot_key = pos; boot_started_at = timer_read32(); boot_armed = true; }
            break;
        case RB_GUIDE: break; // Retired, unmapped keycode remains reserved.
        case RB_TURBO_E: toggle_turbo(KC_E,pos); break;
        case RB_TURBO_F: toggle_turbo(KC_F,pos); break;
        case RB_TURBO_SPACE: toggle_turbo(KC_SPC,pos); break;
        case RB_TURBO_SELECT:
            redblade_recorder_stop(true); recorder_rate_context = false;
            trigger[2] = pos; trigger_held |= 4;
            if (turbo_key || selecting || pending_key) { stop_turbo(); selecting = false; pending_key = 0; }
            else { selecting = true; selection_started_at = timer_read32(); }
            break;
        case RB_AUTO_W:
            redblade_recorder_stop(true); recorder_rate_context = false;
            trigger[1] = pos; trigger_held |= 2;
            if (auto_w) stop_w();
            else { if (turbo_key == KC_W) stop_turbo(); if (!is_key_pressed(KC_W)) { caps_stop(); auto_w = true; forward_started_at=timer_read32(); register_code(KC_W); } }
            break;
        case RB_TURBO_RATE:
            redblade_recorder_stop(true); recorder_rate_context = false;
            set_rate(shift ? (turbo_rate > 50 ? 50 : turbo_rate > 25 ? 25 : 100) : (turbo_rate < 25 ? 25 : turbo_rate < 50 ? 50 : turbo_rate < 100 ? 100 : 25));
            break;
        case RB_TURBO_SLOWER:
            rate_begin(-1,pos);
            break;
        case RB_TURBO_FASTER:
            rate_begin(1,pos);
            break;
        case RB_REC_ARM:
            stop_turbo(); stop_w(); selecting = false; pending_key = 0;
            recorder_rate_context = true; redblade_recorder_arm();
            break;
        case RB_REC_1: case RB_REC_2: case RB_REC_3:
            stop_turbo(); stop_w(); selecting = false; pending_key = 0;
            recorder_rate_context = true; caps_stop(); redblade_recorder_slot(keycode - RB_REC_1, game_mode, shift);
            playback_started_at=timer_read32();
            break;
    }
    return false;
}
bool redblade_controls_indicators_needed(bool fn_active) { (void)fn_active; return true; }
bool redblade_controls_indicator(uint8_t index, bool fn_active, hsv_t *color) {
    const bool off_fn = fn_active && redblade_rgb_off_fn_available();
    if (!off_fn && !redblade_rgb_output_active()) return false;
    const bool hints=fn_active && shortcut_enabled(index);
    if (fn_active && (off_fn || redblade_config_feature(RB_MODULE_FN_FOCUS)) && !hints) {
        *color = (hsv_t){0,0,0};
        return true;
    }
    if (index==RB_LED_CAPS && caps_word) { *color=(hsv_t){32,255,128}; return true; }
    if (hints && index==RB_LED_CAPS) { *color=(hsv_t){85,255,80}; return true; }
    if (redblade_config_feature(RB_MODULE_RECORDER) && redblade_recorder_indicator(index, fn_active, color)) return true;
    if (index == RB_LED_FN) {
        if (selecting || pending_key) *color = redblade_config_effect_scaled(RB_COLOR_SELECTION,(hsv_t){32,255,(timer_read32()/250)&1 ? 128 : 40},128);
        else if (turbo_key) *color = redblade_config_effect(mouse_target() ? (turbo_key==0xD1 ? RB_COLOR_MOUSE_LEFT : RB_COLOR_MOUSE_RIGHT) : RB_COLOR_TURBO,(hsv_t){mouse_target() ? (turbo_key==0xD1 ? 32 : 200) : 0,255,128});
        else if (auto_w) *color = redblade_config_effect(RB_COLOR_FORWARD,(hsv_t){85,255,96});
        else if (fn_active) *color = redblade_config_effect(RB_COLOR_FN_HELD,(hsv_t){128,255,96});
        else if (game_mode) *color = redblade_config_effect(RB_COLOR_FN_GAME,(hsv_t){200,255,80});
        else *color = redblade_config_effect(RB_COLOR_FN_IDLE,(hsv_t){170,255,24});
        return true;
    }
    if (game_mode && (index == RB_LED_LGUI || index == RB_LED_RGUI)) { *color = redblade_config_effect(RB_COLOR_WIN_LOCK,(hsv_t){0,255,128}); return true; }
    if (index == turbo_led && turbo_key) {
        *color = redblade_config_effect_scaled(mouse_target() ? (turbo_key==0xD1 ? RB_COLOR_MOUSE_LEFT : RB_COLOR_MOUSE_RIGHT) : RB_COLOR_TURBO,
            (hsv_t){mouse_target() ? (turbo_key==0xD1 ? 32 : 200) : 0,255,mouse_target() || turbo_rate>100 ? ((timer_read32()/60)&1 ? 255 : 40) : 128},128); return true;
    }
    if (auto_w && index == RB_LED_W) { *color = redblade_config_effect(RB_COLOR_FORWARD,(hsv_t){85,255,96}); return true; }
    if (selecting && (index==18 || index==19)) { *color=redblade_config_effect_scaled(index==18 ? RB_COLOR_MOUSE_LEFT : RB_COLOR_MOUSE_RIGHT,(hsv_t){index==18 ? 32 : 200,255,96},128); return true; }
    if (hints && index==RB_LED_T) { *color=redblade_config_effect_scaled(RB_COLOR_SELECTION,(hsv_t){32,255,96},128);return true; }
    if (hints && (index == RB_LED_R || index == RB_LED_MINUS || index == RB_LED_PLUS)) {
        *color = (hsv_t){recorder_rate_context && index != RB_LED_R ? 200 : turbo_rate <= 25 ? 85 : turbo_rate <= 50 ? 43 : 0,255,80}; return true;
    }
    // Factory positions and regular modifier state; no dynamic keymap scan in the LED loop.
    // Highlight colors reuse palette slots: host 21 and selection 17.
    const uint8_t highlight=redblade_config_byte(13,0);
    if (!fn_active && highlight && (highlight==2 || redblade_rgb_fallback_active())) {
        const uint8_t held=get_mods();
        const bool ctrl=(held & 0x11)!=0, shift=(held & 0x22)!=0, alt=(held & 0x44)!=0;
        const bool ctrl_key=((held & 0x01) && index==96) || ((held & 0x10) && index==103);
        const bool shift_key=((held & 0x02) && index==78) || ((held & 0x20) && index==90);
        const bool alt_key=((held & 0x04) && index==98) || ((held & 0x40) && index==100);
        const uint8_t a=redblade_config_byte(9,0) ? 62 : 42;
        const uint8_t z=redblade_config_byte(9,0) ? 80 : 43;
        if (ctrl_key || (ctrl && (index==82 || index==83 || index==81 || index==z || index==a || index==63 || index==65))) {
            *color=redblade_config_effect(RB_COLOR_HOST_HINT,(hsv_t){170,255,80}); return true;
        }
        if (shift_key || (shift && ((index>=21 && index<=30) || (index>=42 && index<=53) || (index>=62 && index<=73) || (index>=80 && index<=89)))) {
            *color=(hsv_t){200,255,80}; return true;
        }
        if (alt_key || (alt && (index==41 || index==4))) {
            *color=redblade_config_effect_scaled(RB_COLOR_SELECTION,(hsv_t){32,255,80},128); return true;
        }
    }
    if (redblade_rgb_fallback_active()) {
        const led_t locks = host_keyboard_led_state();
        if (index == RB_LED_CAPS || (REDBLADE_CAPS_SHOW_SHIFT && (index == RB_LED_LSHIFT || index == RB_LED_RSHIFT))) {
            if (!locks.caps_lock && redblade_rgb_key_lit(index)) return false;
            if (!locks.caps_lock && redblade_store_valid()) return false;
            *color=locks.caps_lock ? redblade_config_effect(RB_COLOR_CAPS_NUM,(hsv_t){85,255,128}) : (hsv_t){85,0,REDBLADE_FALLBACK_BRIGHTNESS};return true;
        }
        if (index == RB_LED_SCROLL) {
            if (!locks.scroll_lock && redblade_rgb_key_lit(index)) return false;
            if (!locks.scroll_lock && redblade_store_valid()) return false;
            *color=locks.scroll_lock ? redblade_config_effect(RB_COLOR_SCROLL,(hsv_t){32,255,128}) : (hsv_t){32,0,REDBLADE_FALLBACK_BRIGHTNESS};return true;
        }
        if ((index >= 37 && index <= 40) || (index >= 57 && index <= 60) || (index >= 75 && index <= 77) || (index >= 92 && index <= 95) || index >= 107) {
            if (!(index == RB_LED_NUM && locks.num_lock) && redblade_rgb_key_lit(index)) return false;
            if (index==RB_LED_NUM && locks.num_lock) *color=redblade_config_effect(RB_COLOR_CAPS_NUM,(hsv_t){85,255,128});
            else {
                *color=redblade_config_key_color(index,(hsv_t){0,0,REDBLADE_FALLBACK_BRIGHTNESS});
                if (!locks.num_lock) color->v=(uint16_t)color->v*REDBLADE_NUMPAD_DIM_BRIGHTNESS/REDBLADE_FALLBACK_BRIGHTNESS;
            }
            return true;
        }
    }
    if (hints) {
        if (index==15 || index==RB_LED_E || index==RB_LED_F || index==RB_LED_SPACE || index==0) { *color=redblade_config_effect(RB_COLOR_TURBO_HINT,(hsv_t){0,255,96});return true; }
        if (index==RB_LED_LGUI || index==RB_LED_RGUI || index==RB_LED_W) { *color=redblade_config_effect(RB_COLOR_GAME_HINT,(hsv_t){85,255,80});return true; }
        if (index==1) { *color=redblade_config_effect(RB_COLOR_HOST_HINT,(hsv_t){170,255,80});return true; }
        if (index==2 || index==33 || index==36 || index==56 || index==104 || index==106) { *color=redblade_config_effect(RB_COLOR_LIGHT_HINT,(hsv_t){0,0,80});return true; }
        if (index==3) { *color=redblade_config_effect(RB_COLOR_ACCENT_HINT,(hsv_t){0,255,80});return true; }
        if (index==4) { *color=redblade_config_effect_scaled(RB_COLOR_LIGHT_HINT,(hsv_t){0,0,8},80);return true; }
        if (index>=5 && index<=7) {
            const bool active=(redblade_rgb_settings_get() & (1 << (index-5)))!=0;
            *color=redblade_config_effect(RB_COLOR_OPTION_HINT,(hsv_t){85,active ? 255 : 0,64});
            if (!active) color->s=0;
            return true;
        }
        if (index == 8 || index == 9) { *color = (hsv_t){index == 9 ? 85 : 200,255,80}; return true; }
    }
    if (index < 16 || index > 19) {
        return false;
    }
    if (index == 16 && feedback_active) {
        if (feedback_started_at==UINT32_MAX) { *color=redblade_config_effect_scaled(RB_COLOR_SELECTION,(hsv_t){32,255,64},128);return true; }
        if (timer_read32() - feedback_started_at < 600) { *color=redblade_config_effect_scaled(RB_COLOR_ONE_SHOT,(hsv_t){85,255,96},128);return true; }
        feedback_active = false;
    }
    if (!hints) return false;
    const bool shift = (get_mods() & MOD_MASK_SHIFT) != 0;
    *color = (hsv_t){index == 16 ? 128 : index == 17 ? (shift ? 0 : 140) : (shift ? 43 : 170),230,80};
    return true;
}

void redblade_controls_render(uint8_t led_min, uint8_t led_max, bool fn_active) {
    if (!redblade_rgb_output_active() && !(fn_active && redblade_rgb_off_fn_available())) return;
    for (uint8_t i = led_min; i < led_max; ++i) {
        hsv_t color;
        if (!redblade_controls_indicator(i, fn_active, &color)) continue;
        if (color.v > rgb_matrix_get_val()) color.v = rgb_matrix_get_val();
        const rgb_t rgb = redblade_rgb_convert(redblade_rgb_apply_brightness(color));
        rgb_matrix_set_color(i, rgb.r, rgb.g, rgb.b);
    }
}
