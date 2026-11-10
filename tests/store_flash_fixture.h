// SPDX-License-Identifier: GPL-2.0-or-later
#pragma once
#include <stdbool.h>
#include <stdint.h>
extern uint8_t store_flash[2][2048], store_reply[32];
extern unsigned store_programs, store_erases, store_responses, store_applies, store_host_applies, store_stops, store_runtime_calls;
extern bool store_begin_allowed;
extern uint8_t store_runtime_mode, store_runtime_master, store_runtime_flags;
extern int store_fail_program, store_fail_erase;
void store_fixture_reset(void);
void store_callbacks_reset(void);
void store_fixture_payload(uint8_t *payload);
uint32_t store_fixture_crc(const uint8_t *data, unsigned length);
void store_fixture_seed(unsigned bank, uint32_t generation, const uint8_t *payload);
