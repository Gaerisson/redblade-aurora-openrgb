// SPDX-License-Identifier: GPL-2.0-or-later
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define REDBLADE_STORE_PAYLOAD_SIZE 1696U
#define REDBLADE_STORE_PROFILE_SIZE 416U
#define REDBLADE_STORE_BANK_SIZE 2048U
#define REDBLADE_STORE_BANK_A 0xE780UL
#define REDBLADE_STORE_BANK_B 0xEF80UL

void redblade_store_init(void);
void redblade_store_task(void);
bool redblade_store_valid(void);
bool redblade_store_busy(void);
uint8_t redblade_store_error(void); // Last terminal transaction error, zero after begin/abort/success.
uint32_t redblade_store_generation(void);
const uint8_t *redblade_store_data(void);
bool redblade_store_command(uint8_t *packet, uint8_t length);
bool redblade_store_save_lighting(uint8_t mode, uint8_t master, uint8_t flags);

// Memory-mapped banks; erase offsets are 64-byte aligned, program writes exactly four bytes.
const uint8_t *redblade_store_flash_bank(uint8_t bank);
bool redblade_store_flash_erase(uint8_t bank, uint16_t offset);
bool redblade_store_flash_program(uint8_t bank, uint16_t offset, uint32_t word);

// Runtime integration owns these callbacks. They never allocate or reset VIA EEPROM.
uint8_t redblade_store_default_byte(uint16_t offset);
bool redblade_store_allow_begin(void);
void redblade_store_applied(bool from_host);
void redblade_store_runtime(uint8_t mode, uint8_t master, uint8_t flags);
void redblade_store_stop_helpers(void);
