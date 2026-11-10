// SPDX-License-Identifier: GPL-2.0-or-later
#include "redblade_store.h"
#include <hal.h>

_Static_assert(SN32_FLASH_LINE_SIZE == 4, "Storage requires four-byte program lines");
_Static_assert(SN32_FLASH_SECTOR_SIZE == 64, "Storage requires independent64-byte erase sectors");
_Static_assert(REDBLADE_STORE_BANK_A + REDBLADE_STORE_BANK_SIZE == REDBLADE_STORE_BANK_B, "Config banks must be contiguous");
_Static_assert(REDBLADE_STORE_BANK_B + REDBLADE_STORE_BANK_SIZE == 0xF780UL, "Config must end before existing QMK EEPROM backing");

const uint8_t *redblade_store_flash_bank(uint8_t bank) {
    return bank < 2 ? (const uint8_t *)(uintptr_t)(bank ? REDBLADE_STORE_BANK_B : REDBLADE_STORE_BANK_A) : NULL;
}
static bool redblade_store_flash_open(void) {
    // The pinned wear-leveling driver uses eflStart/eflStop around its writes.
    // These operations run on the same cooperative main thread: never interrupt
    // an existing EFL operation, and always restore its stopped/locked state.
    return EFLD1.state == FLASH_STOP && eflStart(&EFLD1, NULL) == HAL_RET_SUCCESS;
}
bool redblade_store_flash_erase(uint8_t bank, uint16_t offset) {
    if (bank > 1 || offset >= REDBLADE_STORE_BANK_SIZE || (offset & 63) || !redblade_store_flash_open()) return false;
    BaseFlash *flash = (BaseFlash *)&EFLD1;
    const flash_sector_t sector = ((bank ? REDBLADE_STORE_BANK_B : REDBLADE_STORE_BANK_A) + offset) / 64;
    flash_error_t result = flashStartEraseSector(flash, sector);
    if (result == FLASH_NO_ERROR || result == FLASH_BUSY_ERASING) result = flashWaitErase(flash);
    if (result == FLASH_NO_ERROR) result = flashVerifyErase(flash, sector);
    eflStop(&EFLD1);
    __asm__ volatile("" ::: "memory");
    return result == FLASH_NO_ERROR;
}
bool redblade_store_flash_program(uint8_t bank, uint16_t offset, uint32_t word) {
    if (bank > 1 || offset > REDBLADE_STORE_BANK_SIZE - 4 || (offset & 3) || !redblade_store_flash_open()) return false;
    BaseFlash *flash = (BaseFlash *)&EFLD1;
    const flash_offset_t address = (bank ? REDBLADE_STORE_BANK_B : REDBLADE_STORE_BANK_A) + offset;
    // The pinned SN32 low-level writer must receive exactly one aligned line.
    const flash_error_t result = flashProgram(flash, address, 4, (const uint8_t *)&word);
    // This pinned synchronous Sonix writer waits for BUSY before reporting an
    // error, but its error branch leaves FLASH_PGM set. Normalize only that
    // completed state so eflStop's READY/STOP precondition remains satisfied.
    if (EFLD1.state == FLASH_PGM) EFLD1.state = FLASH_READY;
    eflStop(&EFLD1);
    __asm__ volatile("" ::: "memory");
    return result == FLASH_NO_ERROR && *(const volatile uint32_t *)(uintptr_t)address == word;
}
