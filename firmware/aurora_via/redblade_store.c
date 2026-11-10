// SPDX-License-Identifier: GPL-2.0-or-later
#include "redblade_store.h"
#include "raw_hid.h"
#include <stddef.h>
#include <string.h>

enum {
    RB_STORE_IDLE, RB_STORE_ERASING, RB_STORE_READY, RB_STORE_VERIFYING, RB_STORE_ERROR,
    RB_STORE_AUTO_PATCH, RB_STORE_COPYING, RB_STORE_HEADER, RB_STORE_COMMITTING
};
enum { RB_STORE_VALID = 1, RB_STORE_ACTIVE_BANK = 2, RB_STORE_AUTOMATIC = 4 };
enum {
    RB_STORE_OK, RB_STORE_BUSY, RB_STORE_BOUNDS, RB_STORE_WRONG_STATE, RB_STORE_CRC,
    RB_STORE_FLASH, RB_STORE_UNSUPPORTED, RB_STORE_INVALID, RB_STORE_GENERATION, RB_STORE_KEYS
};
#define RB_STORE_LAYOUT 0x52424653UL
#define RB_STORE_COMMIT 0x434F4D54UL
#define RB_STORE_PATCH_OFFSET (32U + REDBLADE_STORE_PAYLOAD_SIZE)

// Exactly eight persistent SRAM bytes. The CRC field also holds a pending CRC
// or Fn+F9 scalar metadata before those values are written to the inactive bank.
static struct {
    uint32_t crc;
    uint16_t cursor;
    uint8_t control;
    uint8_t phase_error;
} redblade_store_state;
_Static_assert(sizeof(redblade_store_state) == 8, "Storage must not grow persistent SRAM beyond eight bytes");

static uint16_t redblade_store_read16(const uint8_t *p) { return p[0] | (uint16_t)p[1] << 8; }
static uint32_t redblade_store_read32(const uint8_t *p) { return redblade_store_read16(p) | (uint32_t)redblade_store_read16(p + 2) << 16; }
static void redblade_store_put16(uint8_t *p, uint16_t value) { p[0] = value; p[1] = value >> 8; }
static void redblade_store_put32(uint8_t *p, uint32_t value) { redblade_store_put16(p, value); redblade_store_put16(p + 2, value >> 16); }
static uint8_t redblade_store_phase(void) { return redblade_store_state.phase_error & 15; }
static void redblade_store_set_phase(uint8_t phase) { redblade_store_state.phase_error = phase; }
static void redblade_store_fail(uint8_t error) { redblade_store_state.phase_error = RB_STORE_ERROR | (error << 4); }
static uint8_t redblade_store_active_bank(void) { return !!(redblade_store_state.control & RB_STORE_ACTIVE_BANK); }
static uint8_t redblade_store_target_bank(void) { return redblade_store_valid() ? redblade_store_active_bank() ^ 1 : 0; }
static const uint8_t *redblade_store_target(void) { return redblade_store_flash_bank(redblade_store_target_bank()); }
static uint32_t redblade_store_crc_byte(uint32_t crc, uint8_t value) {
    crc ^= value;
    for (uint8_t bit = 0; bit < 8; ++bit) crc = (crc >> 1) ^ ((crc & 1) ? 0xEDB88320UL : 0);
    return crc;
}
static uint32_t redblade_store_crc_block(const uint8_t *p, uint16_t length) {
    uint32_t crc = UINT32_MAX;
    for (uint16_t i = 0; i < length; ++i) crc = redblade_store_crc_byte(crc, p[i]);
    return ~crc;
}
static uint32_t redblade_store_header_crc(const uint8_t *p) {
    uint32_t crc = UINT32_MAX;
    for (uint8_t i = 0; i < 20; ++i) crc = redblade_store_crc_byte(crc, p[i]);
    // The fixed layout tag is included even while its flash word is still erased.
    for (uint8_t i = 0; i < 4; ++i) crc = redblade_store_crc_byte(crc, (uint8_t)(RB_STORE_LAYOUT >> (8 * i)));
    return ~crc;
}
static bool redblade_store_payload_byte_valid(uint16_t offset, uint8_t value) {
    if (!offset) return value < 4;
    if (offset < 32) return !value;
    const uint16_t field = (offset - 32) % REDBLADE_STORE_PROFILE_SIZE;
    switch (field) {
        case 1: return !(value & 0xFC);
        case 3: return value < 4;
        case 4: return value >= 1 && value <= 60;
        case 5: return (value >= 5 && value <= 100 && value % 5 == 0) || (value >= 125 && value <= 250 && value % 25 == 0);
        case 6: return value <= 9;
        case 7: return value >= 1 && value <= 15;
        default: return ((field >= 8 && field <= 15) || field == 415) ? !value : true;
    }
}
static bool redblade_store_header_valid(const uint8_t *p) {
    return !memcmp(p, "RBPLUSCF", 8) && redblade_store_read16(p + 8) == 1 && redblade_store_read16(p + 10) == REDBLADE_STORE_PAYLOAD_SIZE &&
        redblade_store_read32(p + 24) == RB_STORE_LAYOUT && redblade_store_read32(p + 20) == redblade_store_header_crc(p);
}
static bool redblade_store_bank_valid(uint8_t bank) {
    const uint8_t *p = redblade_store_flash_bank(bank);
    if (!redblade_store_header_valid(p) || redblade_store_read32(p + 28) != RB_STORE_COMMIT ||
        redblade_store_read32(p + 16) != redblade_store_crc_block(p + 32, REDBLADE_STORE_PAYLOAD_SIZE)) return false;
    for (uint16_t i = 0; i < REDBLADE_STORE_PAYLOAD_SIZE; ++i) if (!redblade_store_payload_byte_valid(i, p[32 + i])) return false;
    return true;
}
bool redblade_store_valid(void) { return !!(redblade_store_state.control & RB_STORE_VALID); }
bool redblade_store_busy(void) { return redblade_store_phase() != RB_STORE_IDLE && redblade_store_phase() != RB_STORE_ERROR; }
uint8_t redblade_store_error(void) { return redblade_store_state.phase_error >> 4; }
uint32_t redblade_store_generation(void) {
    return redblade_store_valid() ? redblade_store_read32(redblade_store_flash_bank(redblade_store_active_bank()) + 12) : 0;
}
const uint8_t *redblade_store_data(void) {
    return redblade_store_valid() ? redblade_store_flash_bank(redblade_store_active_bank()) + 32 : NULL;
}
void redblade_store_init(void) {
    memset(&redblade_store_state, 0, sizeof(redblade_store_state));
    const bool a = redblade_store_bank_valid(0), b = redblade_store_bank_valid(1);
    if (a || b) {
        uint8_t bank = !a;
        if (a && b) {
            const uint32_t delta = redblade_store_read32(redblade_store_flash_bank(1) + 12) - redblade_store_read32(redblade_store_flash_bank(0) + 12);
            bank = delta != 0 && delta < 0x80000000UL;
        }
        redblade_store_state.control = RB_STORE_VALID | (bank ? RB_STORE_ACTIVE_BANK : 0);
    }
}
static bool redblade_store_program(uint16_t offset, uint32_t value) {
    if (redblade_store_flash_program(redblade_store_target_bank(), offset, value)) return true;
    redblade_store_fail(RB_STORE_FLASH); return false;
}
static uint32_t redblade_store_header_word(uint16_t offset) {
    switch (offset) {
        case 0: return 0x4C504252UL; // RBPL
        case 4: return 0x46435355UL; // USCF
        case 8: return 1UL | ((uint32_t)REDBLADE_STORE_PAYLOAD_SIZE << 16);
        case 12: return redblade_store_generation() + 1;
        case 16: return redblade_store_state.crc;
        case 20: return redblade_store_header_crc(redblade_store_target());
        default: return RB_STORE_LAYOUT; // offset24; commit at28 is always separate.
    }
}
void redblade_store_task(void) {
    switch (redblade_store_phase()) {
        case RB_STORE_ERASING:
            if (!redblade_store_flash_erase(redblade_store_target_bank(), redblade_store_state.cursor)) { redblade_store_fail(RB_STORE_FLASH); return; }
            redblade_store_state.cursor += 64;
            if (redblade_store_state.cursor == REDBLADE_STORE_BANK_SIZE) {
                redblade_store_state.cursor = 0;
                redblade_store_set_phase((redblade_store_state.control & RB_STORE_AUTOMATIC) ? RB_STORE_AUTO_PATCH : RB_STORE_HEADER);
            }
            return;
        case RB_STORE_AUTO_PATCH:
            // A dedicated, otherwise unused flash word retains three scalars while
            // the eight-byte transaction state is reused for incremental CRC/copy.
            if (!redblade_store_program(RB_STORE_PATCH_OFFSET, redblade_store_state.crc)) return;
            redblade_store_state.crc = UINT32_MAX; redblade_store_set_phase(RB_STORE_COPYING); return;
        case RB_STORE_COPYING: {
            const uint8_t *source = redblade_store_data();
            const uint32_t patch = redblade_store_read32(redblade_store_target() + RB_STORE_PATCH_OFFSET);
            uint32_t word = redblade_store_read32(source + redblade_store_state.cursor);
            if (redblade_store_state.cursor == 32U + (uint16_t)source[0] * REDBLADE_STORE_PROFILE_SIZE) {
                word = (word & 0x0000FFF8UL) | ((patch >> 16) & 7) | ((patch & 0xFF00UL) << 8) | ((patch & 3) << 24);
            }
            if (!redblade_store_program(32 + redblade_store_state.cursor, word)) return;
            for (uint8_t i = 0; i < 4; ++i) redblade_store_state.crc = redblade_store_crc_byte(redblade_store_state.crc, (uint8_t)(word >> (8 * i)));
            redblade_store_state.cursor += 4;
            if (redblade_store_state.cursor == REDBLADE_STORE_PAYLOAD_SIZE) {
                redblade_store_state.crc = ~redblade_store_state.crc; redblade_store_state.cursor = 0; redblade_store_set_phase(RB_STORE_HEADER);
            }
            return;
        }
        case RB_STORE_HEADER:
            if (!redblade_store_program(redblade_store_state.cursor, redblade_store_header_word(redblade_store_state.cursor))) return;
            redblade_store_state.cursor += 4;
            if (redblade_store_state.cursor == 28) {
                redblade_store_state.cursor = 0;
                if (redblade_store_state.control & RB_STORE_AUTOMATIC) {
                    redblade_store_state.crc = UINT32_MAX; redblade_store_set_phase(RB_STORE_VERIFYING);
                } else redblade_store_set_phase(RB_STORE_READY);
            }
            return;
        case RB_STORE_VERIFYING: {
            const uint8_t *p = redblade_store_target() + 32;
            // Fixed24-byte verification chunks; profile validation never walks an
            // entire image in a normal housekeeping iteration.
            const uint16_t end = redblade_store_state.cursor + 24U < REDBLADE_STORE_PAYLOAD_SIZE ? redblade_store_state.cursor + 24U : REDBLADE_STORE_PAYLOAD_SIZE;
            while (redblade_store_state.cursor < end) {
                const uint16_t i = redblade_store_state.cursor++;
                if (!redblade_store_payload_byte_valid(i, p[i])) { redblade_store_fail(RB_STORE_INVALID); return; }
                redblade_store_state.crc = redblade_store_crc_byte(redblade_store_state.crc, p[i]);
            }
            if (redblade_store_state.cursor == REDBLADE_STORE_PAYLOAD_SIZE) {
                if (~redblade_store_state.crc != redblade_store_read32(redblade_store_target() + 16)) { redblade_store_fail(RB_STORE_CRC); return; }
                redblade_store_set_phase(RB_STORE_COMMITTING);
            }
            return;
        }
        case RB_STORE_COMMITTING: {
            if (!redblade_store_header_valid(redblade_store_target()) || redblade_store_read32(redblade_store_target() + 12) != redblade_store_generation() + 1) {
                redblade_store_fail(RB_STORE_INVALID); return;
            }
            if (!redblade_store_program(28, RB_STORE_COMMIT)) return;
            const bool from_host = !(redblade_store_state.control & RB_STORE_AUTOMATIC);
            const uint8_t bank = redblade_store_target_bank();
            redblade_store_state.control = RB_STORE_VALID | (bank ? RB_STORE_ACTIVE_BANK : 0);
            redblade_store_state.cursor = 0; redblade_store_set_phase(RB_STORE_IDLE);
            redblade_store_applied(from_host); return;
        }
        default: return;
    }
}
static uint8_t redblade_store_wire_phase(void) {
    switch (redblade_store_phase()) {
        case RB_STORE_ERASING: case RB_STORE_AUTO_PATCH: return 1;
        case RB_STORE_HEADER: return (redblade_store_state.control & RB_STORE_AUTOMATIC) ? 3 : 1;
        case RB_STORE_COPYING: return 2;
        case RB_STORE_COMMITTING: return 3;
        default: return redblade_store_phase();
    }
}
bool redblade_store_save_lighting(uint8_t mode, uint8_t master, uint8_t flags) {
    if (!redblade_store_valid() || redblade_store_busy() || mode > 3 || flags > 7) return false;
    redblade_store_state.crc = mode | ((uint32_t)master << 8) | ((uint32_t)flags << 16);
    redblade_store_state.control |= RB_STORE_AUTOMATIC; redblade_store_state.cursor = 0; redblade_store_set_phase(RB_STORE_ERASING); return true;
}
bool redblade_store_command(uint8_t *packet, uint8_t length) {
    if (length != 32 || packet[0] != 0xFE || packet[1] != 0x90) return false;
    const uint8_t op = packet[2], count = packet[6];
    const uint16_t offset = redblade_store_read16(packet + 4);
    uint8_t status = RB_STORE_OK;
    switch (op) {
        case 0: case 3: break;
        case 1:
            if (!count || count > 24 || offset >= REDBLADE_STORE_PAYLOAD_SIZE || count > REDBLADE_STORE_PAYLOAD_SIZE - offset) status = RB_STORE_BOUNDS;
            break;
        case 2:
            if (redblade_store_busy()) { status = RB_STORE_BUSY; break; }
            if (redblade_store_read16(packet + 12) != REDBLADE_STORE_PAYLOAD_SIZE) { status = RB_STORE_BOUNDS; break; }
            if (packet[14] != 1) { status = RB_STORE_UNSUPPORTED; break; }
            if (redblade_store_read32(packet + 8) != redblade_store_generation()) { status = RB_STORE_GENERATION; break; }
            if (!redblade_store_allow_begin()) { status = RB_STORE_KEYS; break; }
            redblade_store_state.crc = redblade_store_read32(packet + 4); redblade_store_state.cursor = 0;
            redblade_store_state.control &= (RB_STORE_VALID | RB_STORE_ACTIVE_BANK); redblade_store_set_phase(RB_STORE_ERASING); status = RB_STORE_BUSY; break;
        case 4:
            if (redblade_store_phase() != RB_STORE_READY) { status = RB_STORE_WRONG_STATE; break; }
            if (count < 4 || count > 24 || (count & 3) || packet[7] || (offset & 3) || offset != redblade_store_state.cursor ||
                offset >= REDBLADE_STORE_PAYLOAD_SIZE || count > REDBLADE_STORE_PAYLOAD_SIZE - offset) { status = RB_STORE_BOUNDS; break; }
            for (uint8_t i = 0; i < count; i += 4) {
                if (!redblade_store_program(32 + redblade_store_state.cursor, redblade_store_read32(packet + 8 + i))) { status = RB_STORE_FLASH; break; }
                redblade_store_state.cursor += 4;
            }
            break;
        case 5:
            if (redblade_store_phase() != RB_STORE_READY || redblade_store_state.cursor != REDBLADE_STORE_PAYLOAD_SIZE) { status = RB_STORE_WRONG_STATE; break; }
            redblade_store_state.crc = UINT32_MAX; redblade_store_state.cursor = 0; redblade_store_set_phase(RB_STORE_VERIFYING); status = RB_STORE_BUSY; break;
        case 6:
            redblade_store_state.control &= (RB_STORE_VALID | RB_STORE_ACTIVE_BANK); redblade_store_state.cursor = 0; redblade_store_state.crc = 0; redblade_store_set_phase(RB_STORE_IDLE); break;
        case 7:
            if (packet[4] > 3 || packet[6] > 7) { status = RB_STORE_INVALID; break; }
            if (redblade_store_busy()) { status = RB_STORE_BUSY; break; }
            redblade_store_runtime(packet[4], packet[5], packet[6]); break;
        case 8: redblade_store_stop_helpers(); break;
        default: status = RB_STORE_UNSUPPORTED; break;
    }
    memset(packet + 3, 0, 29); packet[3] = status;
    if (!status || (op == 2 && status == RB_STORE_BUSY) || (op == 5 && status == RB_STORE_BUSY)) {
        const uint8_t *p = redblade_store_data();
        switch (op) {
            case 0:
                packet[4] = 1; packet[5] = 4; packet[6] = 109; packet[7] = 24;
                redblade_store_put16(packet + 8, REDBLADE_STORE_PAYLOAD_SIZE); redblade_store_put16(packet + 10, REDBLADE_STORE_PROFILE_SIZE);
                redblade_store_put32(packet + 12, redblade_store_generation()); packet[16] = p ? p[0] : 0;
                packet[17] = redblade_store_valid() | (redblade_store_busy() << 1); memcpy(packet + 24, "RDBLFS01", 8); break;
            case 1:
                redblade_store_put16(packet + 4, offset); packet[6] = count;
                for (uint8_t i = 0; i < count; ++i) packet[8 + i] = p ? p[offset + i] : redblade_store_default_byte(offset + i);
                break;
            case 3:
                packet[4] = redblade_store_wire_phase(); packet[5] = redblade_store_state.phase_error >> 4;
                redblade_store_put16(packet + 6, redblade_store_state.cursor); redblade_store_put16(packet + 8, REDBLADE_STORE_PAYLOAD_SIZE);
                redblade_store_put32(packet + 10, redblade_store_generation()); packet[14] = p ? p[0] : 0; packet[15] = redblade_store_valid();
                memcpy(packet + 24, "RDBLFS01", 8); break;
            case 4: redblade_store_put16(packet + 4, offset); packet[6] = count; redblade_store_put16(packet + 8, redblade_store_state.cursor); break;
            default: break;
        }
    }
    raw_hid_send(packet, length); return true;
}
