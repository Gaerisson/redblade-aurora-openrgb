/* Full-only SN32F248B layout. Keep config and existing QMK EEPROM out of all
 * executable/load sections, while retaining the mandatory terminal flag. */
MEMORY
{
    flash0 (rx) : org = 0x00000000, len = 0xE780
    flash1 (rx) : org = 0x00000000, len = 0
    flash2 (rx) : org = 0x00000000, len = 0
    flash3 (rx) : org = 0x00000000, len = 0
    flash4 (rx) : org = 0x00000000, len = 0
    flash5 (rx) : org = 0x00000000, len = 0
    flash6 (rx) : org = 0x00000000, len = 0
    flash7 (rx) : org = 0x00000000, len = 0
    flashflag (rx) : org = 0xFFC0, len = 64
    ram0 (wx) : org = 0x20000000, len = 8k
    ram1 (wx) : org = 0x00000000, len = 0
    ram2 (wx) : org = 0x00000000, len = 0
    ram3 (wx) : org = 0x00000000, len = 0
    ram4 (wx) : org = 0x00000000, len = 0
    ram5 (wx) : org = 0x00000000, len = 0
    ram6 (wx) : org = 0x00000000, len = 0
    ram7 (wx) : org = 0x00000000, len = 0
}
REGION_ALIAS("VECTORS_FLASH", flash0);
REGION_ALIAS("VECTORS_FLASH_LMA", flash0);
REGION_ALIAS("XTORS_FLASH", flash0);
REGION_ALIAS("XTORS_FLASH_LMA", flash0);
REGION_ALIAS("TEXT_FLASH", flash0);
REGION_ALIAS("TEXT_FLASH_LMA", flash0);
REGION_ALIAS("RODATA_FLASH", flash0);
REGION_ALIAS("RODATA_FLASH_LMA", flash0);
REGION_ALIAS("VARIOUS_FLASH", flash0);
REGION_ALIAS("VARIOUS_FLASH_LMA", flash0);
REGION_ALIAS("RAM_INIT_FLASH_LMA", flash0);
REGION_ALIAS("MAIN_STACK_RAM", ram0);
REGION_ALIAS("PROCESS_STACK_RAM", ram0);
REGION_ALIAS("DATA_RAM", ram0);
REGION_ALIAS("DATA_RAM_LMA", flash0);
REGION_ALIAS("BSS_RAM", ram0);
REGION_ALIAS("HEAP_RAM", ram0);
INCLUDE rules.ld
_flag_start = 0xFFFC;
SECTIONS
{
    .flag _flag_start : { KEEP(*(.flag)); } > flashflag
}
ASSERT(__flash0_init__ <= 0xE780, "Full executable/load data overlaps persistent config banks");
ASSERT(ADDR(.flag) == 0xFFFC && SIZEOF(.flag) == 4, "SN32 terminal flag must occupy exactly0xFFFC..0xFFFF");
