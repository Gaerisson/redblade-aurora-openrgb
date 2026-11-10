// SPDX-License-Identifier: GPL-2.0-or-later
#include "redblade_recorder.h"
#include "redblade_controls.h"
#include "redblade_rgb.h"
#include "redblade_config.h"
#include "report.h"
#include <string.h>

// Three volatile slots share one fixed arena. Typical edge: key + 8-bit ms
// delay. Delays >=255 ms use two extra bytes. No EEPROM, heap or event queue.
static uint8_t arena[REDBLADE_RECORDER_BYTES], held[16];
static uint16_t offsets[3], lengths[3], used, cursor, pending_delay;
static uint16_t speed_factor = 256;
static uint32_t last_at, started_at, wait_ms;
static uint8_t state, selected = 255, speed_index = 2, held_count, pending_code, last_error;
static bool have_recorded_edge, loop_mode, loop_boundary, pending_record, pending_gui_locked, modifier_cancel;

_Static_assert(REDBLADE_RECORDER_BYTES >= 16 && REDBLADE_RECORDER_BYTES <= 65535, "Recorder arena size");
_Static_assert(REDBLADE_RECORDER_MAX_MS <= 65535, "Recorded edge delay must fit 16 bits");

uint8_t redblade_recorder_state(void) { return state; }
uint8_t redblade_recorder_selected(void) { return selected; }
uint8_t redblade_recorder_speed_quarters(void) { return speed_index < 7 ? speed_index + 2 : speed_index == 7 ? 16 : speed_index == 8 ? 32 : 0; }
uint8_t redblade_recorder_error(void) { return last_error; }
uint16_t redblade_recorder_slot_bytes(uint8_t slot) { return slot < 3 ? lengths[slot] : 0; }
uint16_t redblade_recorder_used(void) { return used; }
bool redblade_recorder_active(void) { return (state >= RB_REC_ARMED && state <= RB_REC_PLAYING) || state == RB_REC_WAITING; }
bool redblade_recorder_loop(void) { return state == RB_REC_PLAYING && loop_mode; }

static uint8_t encode(uint16_t key) {
    if (key >= 0xE0 && key <= 0xE7) return key - 0xE0 + 120;
    return key >= 4 && key <= 119 && key != KC_ESC && key != 0x66 ? key : 0;
}
static uint8_t decode(uint8_t key) { return key >= 120 ? key - 120 + 0xE0 : key; }
static bool held_has(uint8_t key) { return (held[key / 8] & (1 << (key % 8))) != 0; }
static void held_set(uint8_t key, bool down) {
    if (held_has(key) == down) return;
    if (down) { held[key / 8] |= 1 << (key % 8); ++held_count; }
    else { held[key / 8] &= ~(1 << (key % 8)); --held_count; }
}
static void release_owned(void) {
    if (held_count) for (uint8_t key = 4; key < 128; ++key) if (held_has(key)) unregister_code(decode(key));
    memset(held, 0, sizeof(held)); held_count = 0;
}
static void feedback(uint8_t error) { last_error = error; state = RB_REC_FEEDBACK; last_at = timer_read32(); }
static uint32_t scaled(uint16_t delay) { return speed_factor ? ((uint32_t)delay * speed_factor + 128) >> 8 : 1; }
static void update_wait(void) {
    wait_ms = scaled(pending_delay);
    if (loop_boundary && wait_ms < REDBLADE_RECORDER_LOOP_GAP_MS) wait_ms = REDBLADE_RECORDER_LOOP_GAP_MS;
}

static void append(uint8_t key, uint16_t delay) {
    arena[used++] = key;
    arena[used++] = delay < 255 ? delay : 255;
    if (delay >= 255) { arena[used++] = delay; arena[used++] = delay >> 8; }
    lengths[selected] = used - offsets[selected];
}
static void finish_recording(uint32_t now) {
    // Space for these releases is reserved before every captured edge.
    const uint32_t elapsed = now - last_at;
    uint16_t delay = elapsed > 65535 ? 65535 : elapsed;
    for (uint8_t key = 4; held_count && key < 128; ++key) if (held_has(key)) {
        append(key, delay); delay = 0; held_set(key, false);
    }
    state = RB_REC_IDLE;
}
void redblade_recorder_stop(bool save_recording) {
    if (state == RB_REC_RECORDING) {
        if (save_recording) finish_recording(timer_read32());
        else { used = offsets[selected]; lengths[selected] = 0; memset(held, 0, sizeof(held)); held_count = 0; }
    } else if (state == RB_REC_PLAYING) release_owned();
    state = RB_REC_IDLE; loop_mode = loop_boundary = pending_record = pending_gui_locked = modifier_cancel = false;
}
void redblade_recorder_speed_configure(uint8_t index) {
    speed_index=index<=9 ? index : 2;
    const uint8_t quarter=redblade_recorder_speed_quarters();
    speed_factor=quarter ? (1024+quarter/2)/quarter : 0;
    update_wait();
}
void redblade_recorder_speed_reset(void) { redblade_recorder_speed_configure(2); }
void redblade_recorder_speed_step(int8_t direction) {
    if (direction > 0 && speed_index < 9) ++speed_index;
    if (direction < 0 && speed_index > 0) --speed_index;
    redblade_recorder_speed_configure(speed_index); // Only on a control action.
}
void redblade_recorder_init(void) {
    redblade_recorder_stop(false);
    memset(arena, 0, sizeof(arena)); memset(held, 0, sizeof(held));
    memset(offsets, 0, sizeof(offsets)); memset(lengths, 0, sizeof(lengths));
    used = cursor = pending_delay = held_count = 0; selected = 255; last_error = RB_REC_OK;
    have_recorded_edge = false; redblade_recorder_speed_reset();
}
void redblade_recorder_suspend(void) { redblade_recorder_stop(true); }
void redblade_recorder_arm(void) {
    if (redblade_recorder_active()) { redblade_recorder_stop(true); return; }
    state = RB_REC_ARMED; modifier_cancel = false; last_error = RB_REC_OK; last_at = timer_read32();
}

static bool next_event(uint16_t *position, uint8_t *code, uint16_t *delay) {
    const uint16_t end = offsets[selected] + lengths[selected];
    if (*position > end || end - *position < 2) return false;
    *code = arena[(*position)++]; *delay = arena[(*position)++];
    if (*delay == 255) {
        if (end - *position < 2) return false;
        *delay = arena[(*position)++]; *delay |= (uint16_t)arena[(*position)++] << 8;
    }
    const uint8_t key = *code & 127;
    return key >= 4 && key != KC_ESC && key != 0x66;
}
static bool load_pending(void) {
    if (!next_event(&cursor, &pending_code, &pending_delay)) return false;
    update_wait(); return true;
}
void redblade_recorder_slot(uint8_t slot, bool gui_locked, bool repeat) {
    if (slot >= 3 || state == RB_REC_RECORDING) return;
    const bool record = state == RB_REC_ARMED || (state == RB_REC_WAITING && pending_record);
    // A physical Shift press stops a loop before QMK applies Shift. Remember
    // that gesture until release so Fn+Shift+the same slot truly toggles off.
    const bool toggle_off = repeat && selected == slot && ((state == RB_REC_PLAYING && loop_mode) || (state == RB_REC_IDLE && modifier_cancel));
    redblade_recorder_stop(true); selected = slot;
    if (toggle_off) return;
    if (repeat && !has_anykey() && !get_weak_mods() && get_mods() && !(get_mods() & ~MOD_MASK_SHIFT)) {
        // Never clear a user's held Shift to play a macro. Start only after
        // the loop chord is released; an ordinary key cancels this request.
        state = RB_REC_WAITING; loop_mode = true; pending_record = record; pending_gui_locked = gui_locked;
        last_at = timer_read32(); last_error = RB_REC_OK; return;
    }
    if (has_anykey() || get_mods() || get_weak_mods()) { feedback(RB_REC_BUSY); return; }
    if (record) {
        const uint16_t old = lengths[slot], begin = offsets[slot];
        if (old) {
            memmove(arena + begin, arena + begin + old, used - begin - old);
            for (uint8_t i = 0; i < 3; ++i) if (i != slot && lengths[i] && offsets[i] > begin) offsets[i] -= old;
            used -= old;
        }
        lengths[slot] = 0; offsets[slot] = used;
        memset(held, 0, sizeof(held)); held_count = 0; have_recorded_edge = false;
        started_at = last_at = timer_read32(); state = RB_REC_RECORDING; last_error = RB_REC_OK;
        return;
    }
    if (!lengths[slot]) { feedback(RB_REC_EMPTY); return; }
    if (gui_locked) {
        uint16_t position = offsets[slot], delay; uint8_t code;
        while (position < offsets[slot] + lengths[slot]) {
            if (!next_event(&position, &code, &delay)) { feedback(RB_REC_INVALID); return; }
            if ((code & 127) == 123 || (code & 127) == 127) { feedback(RB_REC_GUI_LOCKED); return; }
        }
    }
    memset(held, 0, sizeof(held)); held_count = 0; cursor = offsets[slot];
    if (!load_pending()) { feedback(RB_REC_INVALID); return; }
    state = RB_REC_PLAYING; loop_mode = repeat; last_error = RB_REC_OK; last_at = timer_read32();
}
void redblade_recorder_key(uint16_t keycode, bool pressed) {
    // A real physical key takes over before QMK processes that press.
    // Layer holds (including Fn) are control gestures; modified/remapped actions
    // can emit basic usages too, and must take over before their QMK handler.
    const bool physical_action = keycode >= 4 && keycode < SAFE_RANGE && !(keycode >= QK_MOMENTARY && keycode <= QK_MOMENTARY_MAX);
    if (state == RB_REC_PLAYING && pressed && physical_action) {
        const bool shifted_loop = loop_mode && (keycode == 0xE1 || keycode == 0xE5);
        redblade_recorder_stop(false); modifier_cancel = shifted_loop;
    } else if (state == RB_REC_WAITING && pressed && physical_action) redblade_recorder_stop(false);
    else if (modifier_cancel && ((pressed && physical_action && keycode != 0xE1 && keycode != 0xE5) || (!pressed && (keycode == 0xE1 || keycode == 0xE5)))) modifier_cancel = false;
    if (state != RB_REC_RECORDING) return;
    const uint32_t now = timer_read32();
    if (now - started_at >= REDBLADE_RECORDER_MAX_MS) { finish_recording(now); feedback(RB_REC_TIMEOUT); return; }
    const uint8_t key = encode(keycode);
    if (!key || held_has(key) == pressed) return;
    const uint16_t delay = have_recorded_edge ? now - last_at : 0;
    const uint8_t bytes = delay < 255 ? 2 : 4;
    const uint8_t after = held_count + (pressed ? 1 : -1);
    const uint16_t reserve = after ? after * 2 + 2 : 0;
    if (REDBLADE_RECORDER_BYTES - used < bytes + reserve) { finish_recording(now); feedback(RB_REC_FULL); return; }
    append(key | (pressed ? 128 : 0), delay); held_set(key, pressed);
    have_recorded_edge = true; last_at = now;
    if (REDBLADE_RECORDER_BYTES - used < 4) { finish_recording(now); feedback(RB_REC_FULL); }
}
void redblade_recorder_task(bool fn_active) {
    if (state == RB_REC_IDLE) return;
    const uint32_t now = timer_read32();
    if (state == RB_REC_WAITING) {
        if (now - last_at >= REDBLADE_TURBO_SELECT_TIMEOUT_MS) { redblade_recorder_stop(false); feedback(RB_REC_BUSY); }
        else if (!fn_active && !get_mods() && !get_weak_mods() && !has_anykey()) {
            state = pending_record ? RB_REC_ARMED : RB_REC_IDLE;
            redblade_recorder_slot(selected, pending_gui_locked, true);
        }
    } else if (state == RB_REC_ARMED && now - last_at >= REDBLADE_TURBO_SELECT_TIMEOUT_MS) state = RB_REC_IDLE;
    else if (state == RB_REC_FEEDBACK && now - last_at >= 1200) state = RB_REC_IDLE;
    else if (state == RB_REC_RECORDING && now - started_at >= REDBLADE_RECORDER_MAX_MS) { finish_recording(now); feedback(RB_REC_TIMEOUT); }
    else if (state == RB_REC_PLAYING && now - last_at >= wait_ms) {
        loop_boundary = false;
        const uint8_t key = pending_code & 127; const bool down = (pending_code & 128) != 0;
        if (down && !held_has(key)) { register_code(decode(key)); redblade_rgb_replay_key(decode(key)); }
        if (!down && held_has(key)) unregister_code(decode(key));
        held_set(key, down); last_at = now; // One edge per pass, no burst catch-up.
        if (cursor >= offsets[selected] + lengths[selected]) {
            if (!loop_mode) redblade_recorder_stop(false);
            else {
                release_owned(); cursor = offsets[selected]; loop_boundary = true;
                if (!load_pending()) { redblade_recorder_stop(false); feedback(RB_REC_INVALID); }
            }
        }
        else if (!load_pending()) { redblade_recorder_stop(false); feedback(RB_REC_INVALID); }
    }
}
bool redblade_recorder_indicator(uint8_t index, bool fn_active, hsv_t *color) {
    const bool slot_key = index >= 21 && index <= 23;
    if (index != RB_LED_FN && index != 16 && !slot_key) return false;
    if (state != RB_REC_IDLE && (!slot_key || state == RB_REC_ARMED || index == 21 + selected)) {
        const uint16_t phase = (timer_read32() / 3) % 512;
        const uint8_t value = 24 + (phase < 256 ? phase : 511 - phase) * 104 / 255;
        const uint8_t effect=state==RB_REC_RECORDING ? RB_COLOR_RECORDING : state==RB_REC_PLAYING ? (loop_mode ? RB_COLOR_LOOP : RB_COLOR_ONE_SHOT) : RB_COLOR_SELECTION;
        *color=redblade_config_effect_scaled(effect,(hsv_t){state==RB_REC_RECORDING ? 0 : state==RB_REC_PLAYING ? (loop_mode ? 128 : 85) : 32,255,value},128);
        return true;
    }
    if (slot_key && fn_active) {
        *color=lengths[index-21] ? redblade_config_effect_scaled(RB_COLOR_ONE_SHOT,(hsv_t){85,255,72},128) : (hsv_t){85,0,8};return true;
    }
    return false;
}
