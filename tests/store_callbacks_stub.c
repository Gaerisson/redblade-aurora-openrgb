// SPDX-License-Identifier: GPL-2.0-or-later
// Link this only in pure storage tests, never with the real runtime adapter.
#include "store_flash_fixture.h"
#include "redblade_store.h"
#include <assert.h>
#include <string.h>
uint8_t store_reply[32];
unsigned store_responses, store_applies, store_host_applies, store_stops, store_runtime_calls;
bool store_begin_allowed;
uint8_t store_runtime_mode, store_runtime_master, store_runtime_flags;
void store_callbacks_reset(void) {
    memset(store_reply, 0, sizeof(store_reply));
    store_responses = store_applies = store_host_applies = store_stops = store_runtime_calls = 0;
    store_begin_allowed = true; store_runtime_mode = store_runtime_master = store_runtime_flags = 0;
}
void raw_hid_send(uint8_t *packet, uint8_t length) { assert(length == 32); memcpy(store_reply, packet, 32); ++store_responses; }
uint8_t redblade_store_default_byte(uint16_t offset) {
    if (offset < 32) return 0;
    const unsigned n = (offset - 32) % 416;
    const uint8_t metadata[8] = {255, 3, 255, 0, 5, 50, 2, 15};
    if (n < 8) return metadata[n];
    if (n >= 16 && n < 343) return ((n - 16) % 3 == 2) ? 64 : 0;
    return 0;
}
bool redblade_store_allow_begin(void) { return store_begin_allowed; }
void redblade_store_applied(bool from_host) { ++store_applies; store_host_applies += from_host; }
void redblade_store_runtime(uint8_t mode, uint8_t master, uint8_t flags) { ++store_runtime_calls; store_runtime_mode = mode; store_runtime_master = master; store_runtime_flags = flags; }
void redblade_store_stop_helpers(void) { ++store_stops; }
