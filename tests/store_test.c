// SPDX-License-Identifier: GPL-2.0-or-later
#include "redblade_store.h"
#include "store_flash_fixture.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static uint8_t payload[1696], replacement[1696], old_bank[2048];
static unsigned tests;
static void put16(uint8_t *p, uint16_t v) { p[0] = v; p[1] = v >> 8; }
static void put32(uint8_t *p, uint32_t v) { put16(p, v); put16(p + 2, v >> 16); }
static uint16_t get16(const uint8_t *p) { return p[0] | (uint16_t)p[1] << 8; }
static uint32_t get32(const uint8_t *p) { return get16(p) | (uint32_t)get16(p + 2) << 16; }
static uint8_t *request(uint8_t op) {
    static uint8_t packet[32]; memset(packet, 0, sizeof(packet)); packet[0] = 0xfe; packet[1] = 0x90; packet[2] = op; return packet;
}
static unsigned send(uint8_t *p) {
    assert(redblade_store_command(p, 32));
    assert(store_reply[0] == 0xfe && store_reply[1] == 0x90 && store_reply[2] == p[2]);
    return store_reply[3];
}
static unsigned begin(const uint8_t *p, uint32_t generation) {
    uint8_t *q = request(2); put32(q + 4, store_fixture_crc(p, 1696)); put32(q + 8, generation); put16(q + 12, 1696); q[14] = 1; return send(q);
}
static unsigned phase(void) { assert(send(request(3)) == 0); return store_reply[4]; }
static void ready(void) {
    for (unsigned i = 0; i < 100 && phase() == 1; ++i) {
        const unsigned erases = store_erases; redblade_store_task(); assert(store_erases - erases <= 1);
    }
    assert(phase() == 2);
}
static void write_payload(const uint8_t *p) {
    for (unsigned offset = 0; offset < 1696; offset += 24) {
        const unsigned count = 1696 - offset < 24 ? 1696 - offset : 24;
        uint8_t *q = request(4); put16(q + 4, offset); q[6] = count; memcpy(q + 8, p + offset, count);
        assert(send(q) == 0); assert(get16(store_reply + 8) == offset + count);
    }
}
static void finish(void) {
    for (unsigned i = 0; i < 2000 && redblade_store_busy(); ++i) {
        unsigned programs = store_programs, erases = store_erases;
        redblade_store_task(); assert(store_programs - programs <= 1 && store_erases - erases <= 1);
    }
    assert(!redblade_store_busy());
}
static void install(const uint8_t *p, uint32_t generation) { assert(begin(p, generation) == 1); ready(); write_payload(p); assert(send(request(5)) == 1); finish(); }
static void reset(void) { store_fixture_reset(); store_callbacks_reset(); store_fixture_payload(payload); memcpy(replacement, payload, 1696); replacement[48] = 201; redblade_store_init(); }
static void old(void) { reset(); store_fixture_seed(0, 41, payload); redblade_store_init(); memcpy(old_bank, store_flash[0], 2048); assert(redblade_store_valid()); }

static void default_reads_and_capabilities(void) {
    reset(); assert(!redblade_store_valid() && redblade_store_data() == NULL && redblade_store_generation() == 0);
    assert(send(request(0)) == 0); assert(store_reply[4] == 1 && store_reply[5] == 4 && store_reply[6] == 109 && store_reply[7] == 24);
    assert(get16(store_reply + 8) == 1696 && get16(store_reply + 10) == 416 && !memcmp(store_reply + 24, "RDBLFS01", 8));
    uint8_t *p = request(1); put16(p + 4, 32); p[6] = 8; assert(send(p) == 0);
    const uint8_t wanted[] = {255, 3, 255, 0, 5, 50, 2, 15}; assert(!memcmp(store_reply + 8, wanted, 8));
    p = request(1); put16(p + 4, 1695); p[6] = 2; assert(send(p) == 2);
    p = request(1); p[6] = 0; assert(send(p) == 2);
    p = request(1); p[6] = 25; assert(send(p) == 2);
    assert(!redblade_store_command(p, 31)); p = request(0); p[1] = 0x81; assert(!redblade_store_command(p, 32));
    assert(send(request(99)) == 6); assert(store_programs == 0 && store_erases == 0); ++tests;
}
static void journal_publish_and_reboot(void) {
    old(); assert(begin(replacement, 41) == 1); assert(redblade_store_busy() && redblade_store_generation() == 41);
    ready(); write_payload(replacement); assert(redblade_store_generation() == 41 && !memcmp(redblade_store_data(), payload, 1696));
    assert(send(request(5)) == 1); assert(redblade_store_generation() == 41); finish();
    assert(redblade_store_generation() == 42 && !memcmp(redblade_store_data(), replacement, 1696));
    assert(!memcmp(store_flash[0], old_bank, 2048) && store_applies == 1 && store_host_applies == 1);
    assert(get32(store_flash[1] + 28) == 0x434f4d54U); redblade_store_init(); assert(redblade_store_generation() == 42); ++tests;
}
static void strict_mutation_guards(void) {
    old(); assert(begin(replacement, 40) == 8 && !redblade_store_busy());
    store_begin_allowed = false; assert(begin(replacement, 41) == 9 && store_erases == 0); store_begin_allowed = true;
    uint8_t *q = request(2); put16(q + 12, 1692); q[14] = 1; assert(send(q) == 2);
    q = request(2); put16(q + 12, 1696); q[14] = 2; assert(send(q) == 6);
    assert(send(request(5)) == 3); assert(begin(replacement, 41) == 1); assert(begin(replacement, 41) == 1);
    q = request(4); q[6] = 4; assert(send(q) == 3); ready();
    q = request(4); put16(q + 4, 4); q[6] = 4; assert(send(q) == 2);
    q = request(4); q[6] = 3; assert(send(q) == 2);
    q = request(4); q[6] = 28; assert(send(q) == 2);
    q = request(4); q[6] = 4; q[7] = 1; assert(send(q) == 2);
    q = request(4); put16(q + 4, 0xfffc); q[6] = 8; assert(send(q) == 2);
    q = request(4); q[6] = 4; memcpy(q + 8, replacement, 4); assert(send(q) == 0);
    q = request(4); q[6] = 4; assert(send(q) == 2); // A lost write ACK cannot authorize replay.
    assert(send(request(5)) == 3); assert(send(request(6)) == 0 && !redblade_store_busy());
    assert(!memcmp(store_flash[0], old_bank, 2048)); ++tests;
}
static void crc_and_configuration_fail_closed(void) {
    old(); assert(begin(replacement, 41) == 1); ready(); replacement[60] ^= 1; write_payload(replacement); assert(send(request(5)) == 1); finish();
    assert(phase() == 4 && store_reply[5] == 4 && redblade_store_generation() == 41 && store_applies == 0);
    redblade_store_init(); assert(redblade_store_generation() == 41);
    const unsigned offsets[] = {0, 1, 33, 35, 36, 37, 38, 39, 40, 447};
    const uint8_t bad[] = {4, 1, 4, 4, 0, 101, 10, 0, 1, 1};
    for (unsigned i = 0; i < sizeof(offsets)/sizeof(offsets[0]); ++i) {
        old(); replacement[offsets[i]] = bad[i]; install(replacement, 41);
        assert(phase() == 4 && store_reply[5] == 7 && redblade_store_generation() == 41 && store_applies == 0);
        redblade_store_init(); assert(redblade_store_generation() == 41);
    }
    ++tests;
}
static void interruption_preserves_previous_bank(void) {
    for (unsigned step = 0; step < 100; ++step) {
        old(); assert(begin(replacement, 41) == 1);
        for (unsigned i = 0; i < step && phase() == 1; ++i) redblade_store_task();
        redblade_store_init(); assert(redblade_store_generation() == 41 && !memcmp(redblade_store_data(), payload, 1696));
    }
    for (unsigned written = 0; written < 1696; written += 4) {
        old(); assert(begin(replacement, 41) == 1); ready();
        for (unsigned offset = 0; offset < written; offset += 4) { uint8_t *q = request(4); put16(q + 4, offset); q[6] = 4; memcpy(q + 8, replacement + offset, 4); assert(send(q) == 0); }
        redblade_store_init(); assert(redblade_store_generation() == 41);
    }
    for (unsigned step = 0; step < 100; ++step) {
        old(); assert(begin(replacement, 41) == 1); ready(); write_payload(replacement); assert(send(request(5)) == 1);
        for (unsigned i = 0; i < step && redblade_store_busy(); ++i) redblade_store_task();
        const uint32_t generation = redblade_store_generation(); redblade_store_init(); assert(redblade_store_generation() == generation);
        assert(!memcmp(store_flash[0], old_bank, 2048));
    }
    ++tests;
}
static void corrupt_committed_banks_and_generation_wrap(void) {
    reset(); store_fixture_seed(0, 0xffffffffU, payload); store_fixture_seed(1, 0xfffffffeU, replacement); redblade_store_init(); assert(redblade_store_generation() == 0xffffffffU);
    install(replacement, 0xffffffffU); assert(redblade_store_generation() == 0 && redblade_store_valid()); redblade_store_init(); assert(redblade_store_generation() == 0 && redblade_store_valid());
    for (unsigned offset = 0; offset < 1728; ++offset) {
        reset(); store_fixture_seed(0, 41, payload); store_fixture_seed(1, 42, replacement); store_flash[1][offset] ^= 1; redblade_store_init(); assert(redblade_store_generation() == 41);
    }
    reset(); store_fixture_seed(0, 1, payload); store_flash[0][100] ^= 1; redblade_store_init(); assert(!redblade_store_valid()); ++tests;
}
static void flash_failures_abort_and_retry(void) {
    for (unsigned at = 0; at < 32; ++at) {
        old(); store_fail_erase = (int)at; assert(begin(replacement, 41) == 1); finish();
        assert(phase() == 4 && store_reply[5] == 5 && redblade_store_generation() == 41); redblade_store_init(); assert(redblade_store_generation() == 41);
    }
    old(); assert(begin(replacement, 41) == 1); ready(); store_fail_program = 0;
    uint8_t *q = request(4); q[6] = 4; memcpy(q + 8, replacement, 4); assert(send(q) == 5 && !redblade_store_busy());
    assert(send(request(6)) == 0); store_fail_program = -1; install(replacement, 41); assert(redblade_store_generation() == 42);
    old(); assert(begin(replacement, 41) == 1); ready(); write_payload(replacement); assert(send(request(5)) == 1);
    store_fail_program = 0; finish(); assert(phase() == 4 && redblade_store_generation() == 41); redblade_store_init(); assert(redblade_store_generation() == 41); ++tests;
}
static void temporary_controls_do_not_save(void) {
    old(); uint8_t *q = request(7); q[4] = 2; q[5] = 73; q[6] = 5; assert(send(q) == 0);
    assert(store_runtime_calls == 1 && store_runtime_mode == 2 && store_runtime_master == 73 && store_runtime_flags == 5);
    q = request(7); q[4] = 4; assert(send(q) == 7); q = request(7); q[6] = 8; assert(send(q) == 7);
    assert(send(request(8)) == 0 && store_stops == 1 && store_programs == 0 && store_erases == 0);
    assert(!memcmp(redblade_store_data(), payload, 1696)); ++tests;
}
static void asynchronous_lighting_copy_retains_every_other_byte(void) {
    reset(); assert(!redblade_store_save_lighting(1, 77, 5));
    for (unsigned selected = 0; selected < 4; ++selected) {
        reset(); payload[0] = selected; store_fixture_seed(0, 41, payload); redblade_store_init(); memcpy(old_bank, store_flash[0], 2048);
        assert(redblade_store_save_lighting(1, 77, 5) && redblade_store_busy() && store_programs == 0 && store_erases == 0);
        assert(!redblade_store_save_lighting(1, 77, 5)); finish(); assert(redblade_store_generation() == 42 && store_applies == 1 && store_host_applies == 0);
        uint8_t expected[1696]; memcpy(expected, payload, 1696); const unsigned base = 32 + selected * 416;
        expected[base] = (expected[base] & 0xf8) | 5; expected[base + 2] = 77; expected[base + 3] = 1;
        assert(!memcmp(redblade_store_data(), expected, 1696) && !memcmp(store_flash[0], old_bank, 2048));
        redblade_store_init(); assert(redblade_store_generation() == 42);
    }
    ++tests;
}
static void corrupt_inactive_header_is_never_published(void) {
    const unsigned fields[] = {0, 8, 10, 12, 20, 24};
    for (unsigned i = 0; i < sizeof(fields)/sizeof(fields[0]); ++i) {
        old(); assert(begin(replacement, 41) == 1); ready(); write_payload(replacement);
        store_flash[1][fields[i]] ^= 1; assert(send(request(5)) == 1); finish();
        assert(redblade_store_generation() == 41 && store_applies == 0 && get32(store_flash[1] + 28) == 0);
        redblade_store_init(); assert(redblade_store_generation() == 41);
    }
    ++tests;
}
static void asynchronous_lighting_copy_interruption(void) {
    for (unsigned step = 0; step < 550; ++step) {
        old(); assert(redblade_store_save_lighting(2, 77, 5));
        for (unsigned i = 0; i < step && redblade_store_busy(); ++i) redblade_store_task();
        const uint32_t generation = redblade_store_generation(); redblade_store_init(); assert(redblade_store_generation() == generation);
        assert(!memcmp(store_flash[0], old_bank, 2048));
        if (generation == 41) assert(!memcmp(redblade_store_data(), payload, 1696));
        else assert(generation == 42 && redblade_store_data()[34] == 77 && redblade_store_data()[35] == 2);
    }
    ++tests;
}
int main(void) {
    assert(store_fixture_crc((const uint8_t *)"123456789", 9) == 0xcbf43926U);
    default_reads_and_capabilities(); journal_publish_and_reboot(); strict_mutation_guards(); crc_and_configuration_fail_closed();
    interruption_preserves_previous_bank(); corrupt_committed_banks_and_generation_wrap(); flash_failures_abort_and_retry(); temporary_controls_do_not_save(); asynchronous_lighting_copy_retains_every_other_byte();
    corrupt_inactive_header_is_never_published(); asynchronous_lighting_copy_interruption();
    printf("store: %u regression groups passed (1696-byte payload, journal interruption/corruption, protocol, Fn+F9)\n", tests);
    return 0;
}
