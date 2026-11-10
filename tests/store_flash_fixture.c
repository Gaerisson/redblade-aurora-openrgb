// SPDX-License-Identifier: GPL-2.0-or-later
#include "store_flash_fixture.h"
#include "redblade_store.h"
#include <assert.h>
#include <string.h>

uint8_t store_flash[2][2048];
unsigned store_programs, store_erases;
int store_fail_program, store_fail_erase;
static bool store_programmed_lines[2][512];

static void put16(uint8_t *p, uint16_t v) { p[0] = v; p[1] = v >> 8; }
static void put32(uint8_t *p, uint32_t v) { put16(p, v); put16(p + 2, v >> 16); }
uint32_t store_fixture_crc(const uint8_t *data, unsigned length) {
    uint32_t crc = 0xffffffffU;
    for (unsigned i = 0; i < length; ++i) {
        crc ^= data[i];
        for (unsigned j = 0; j < 8; ++j) crc = (crc >> 1) ^ ((crc & 1) ? 0xedb88320U : 0);
    }
    return crc ^ 0xffffffffU;
}
void store_fixture_payload(uint8_t *p) {
    memset(p, 0, 1696);
    for (unsigned profile = 0; profile < 4; ++profile) {
        uint8_t *q = p + 32 + 416 * profile;
        q[0] = 255; q[1] = 3; q[2] = 255; q[3] = 0; q[4] = 5; q[5] = 50; q[6] = 2; q[7] = 15;
        for (unsigned key = 0; key < 109; ++key) q[16 + key * 3 + 2] = 64;
        // Distinct values exercise whole-image copies without copying a production default table.
        for (unsigned i = 343; i < 415; ++i) q[i] = (uint8_t)(i + profile);
    }
}
void store_fixture_seed(unsigned bank, uint32_t generation, const uint8_t *payload) {
    assert(bank < 2);
    uint8_t *p = store_flash[bank];
    memset(p, 0, 2048);
    memset(store_programmed_lines[bank], 0, sizeof(store_programmed_lines[bank]));
    for (unsigned line = 0; line < 432; ++line) store_programmed_lines[bank][line] = true;
    memcpy(p, "RBPLUSCF", 8); put16(p + 8, 1); put16(p + 10, 1696);
    put32(p + 12, generation); put32(p + 16, store_fixture_crc(payload, 1696));
    put32(p + 24, 0x52424653U);
    uint8_t header[24]; memcpy(header, p, 20); memcpy(header + 20, p + 24, 4);
    put32(p + 20, store_fixture_crc(header, 24)); put32(p + 28, 0x434f4d54U);
    memcpy(p + 32, payload, 1696);
}
void store_fixture_reset(void) {
    memset(store_flash, 0, sizeof(store_flash));
    memset(store_programmed_lines, 0, sizeof(store_programmed_lines));
    store_programs = store_erases = 0; store_fail_program = store_fail_erase = -1;
}
const uint8_t *redblade_store_flash_bank(uint8_t bank) { assert(bank < 2); return store_flash[bank]; }
bool redblade_store_flash_erase(uint8_t bank, uint16_t offset) {
    assert(bank < 2 && offset < 2048 && offset % 64 == 0);
    if (store_fail_erase == 0) return false;
    if (store_fail_erase > 0) --store_fail_erase;
    ++store_erases; memset(store_flash[bank] + offset, 0, 64);
    memset(store_programmed_lines[bank] + offset / 4, 0, 16 * sizeof(bool)); return true;
}
bool redblade_store_flash_program(uint8_t bank, uint16_t offset, uint32_t word) {
    assert(bank < 2 && offset <= 2044 && offset % 4 == 0);
    assert(!store_programmed_lines[bank][offset / 4]);
    assert(store_flash[bank][offset] == 0 && store_flash[bank][offset + 1] == 0 && store_flash[bank][offset + 2] == 0 && store_flash[bank][offset + 3] == 0);
    store_programmed_lines[bank][offset / 4] = true;
    if (store_fail_program == 0) { store_flash[bank][offset] = (uint8_t)word; return false; }
    if (store_fail_program > 0) --store_fail_program;
    ++store_programs; put32(store_flash[bank] + offset, word); return true;
}
