# RedBlade+ Studio configuration protocol, schema 1

Target: Full firmware 0.9.0+, board identity `RDBLFS01`, USB 320F:5136, Raw HID FF60:61. Core deliberately excludes these modules/storage. All offsets below refer to the 32-byte payload, without the Windows report-ID prefix. Integers are little-endian unless explicitly stated. Existing VIA/VialRGB commands are unchanged.

## Configuration payload

Exactly **1696 bytes**: 32 global bytes followed by four 416-byte profiles.

- Global byte 0: selected onboard profile, 0..3.
- Global bytes 1..31: zero, reserved.
- Profile base: `32 + profileIndex * 416`.

| Profile offset | Size | Meaning |
| --- | --- | --- |
| 0 | 2 | Enabled module bits (LE): bit0 startup wave, 1 four-media fallback, 2 idle fallback, 3 reactive typing, 4 Fn focus, 5 keyboard Turbo, 6 mouse Turbo, 7 Recorder, 8 forward hold, 9 Windows lock. Bits10..15 zero. Default 0x03FF. |
| 2 | 1 | Master brightness 0..255, default255 |
| 3 | 1 | Default mode: 0 Host, 1 local per-key colors, 2 accent/Red, 3 Off |
| 4 | 1 | Host RGB timeout in seconds, 1..60, default5 |
| 5 | 1 | Default Turbo presses/s: 5..100 in steps5, then125/150/175/200/225/250; default50 |
| 6 | 1 | Recorder speed index0..9: 0.5,0.75,1,1.25,1.5,1.75,2,4,8,Hyper; default2 (1x) |
| 7 | 1 | Reactive trail lifetime1..15 RGB frames, default15 |
| 8..15 | 8 | Zero, reserved |
| 16 | 327 | 109 per-key HSV8 triples, physical LED order from led-map.json; default(0,0,64) |
| 343 | 72 | 24 effect HSV8 triples in the order below |
| 415 | 1 | Zero, reserved |

Effects, index and default HSV8:

0 startup wave (0,255,255); 1 reactive peak (0,255,255); 2 accent/Red profile (0,255,255); 3 media fallback (0,0,64); 4 Fn idle (170,255,24); 5 Fn held (128,255,96); 6 keyboard Turbo (0,255,128); 7 forward hold (85,255,96); 8 locked Windows keys (0,255,128); 9 Fn Game Mode (200,255,80); 10 left-click Turbo (32,255,128); 11 right-click Turbo (200,255,128); 12 Caps/Num active (85,255,128); 13 Scroll active (32,255,128); 14 Recorder recording (0,255,128); 15 Recorder one-shot (85,255,128); 16 Recorder loop (128,255,128); 17 selection/error (32,255,128); 18 Fn lighting hints (0,0,80); 19 Fn game hints (85,255,80); 20 Fn Turbo hints (0,255,96); 21 Fn Host hint (170,255,80); 22 Fn accent hint (0,255,80); 23 enabled option hints (85,255,64).

RGB pickers convert RGB8 to HSV8 before encoding. Some RGB values quantize to the nearest HSV8 color. Dynamic brightness/breathing/blink still applies to these base colors. Master brightness and RGB Off/USB suspension remain authoritative. Fn focus follows the supplied shortcut positions and hides disabled helper shortcuts; it does not reclassify arbitrary VIA layer edits.

## Packets

Requests start `FE 90 op`. Responses start `FE 90 op status`. Status: 0 OK; 1 busy/accepted; 2 bounds/alignment; 3 wrong state; 4 CRC mismatch; 5 flash error; 6 unsupported; 7 invalid configuration; 8 generation mismatch; 9 release keys/stop helpers first. Unspecified bytes are zero. Mutations are serialized; ordinary reads do not change RGB freshness or EEPROM.

| Op | Request | Response |
| --- | --- | --- |
| 0 capabilities | No arguments | byte4 schema1,5 profiles4,6 LEDs109,7 effects24; u16@8 payload1696,u16@10 profile416,u32@12 generation (0 if none); byte16 selected0..3,17 flags(valid bit0,busy bit1); bytes24..31 RDBLFS01 |
| 1 read active/default | u16@4 offset,byte6 count1..24 | u16@4 echoed offset,byte6 count; bytes8..31 data. Bounds checked against1696. Without saved config, return defaults. |
| 2 begin save | u32@4 expected payload CRC32,u32@8 expected active generation,u16@12 length1696,byte14 schema1 | Accepted/busy. Never erase the active bank. Host must poll op3 until ready. |
| 3 transaction status | No arguments | byte4 phase(0idle,1erasing,2ready/writing,3verifying,4error); byte5 last error; u16@6 cursor,u16@8 total1696,u32@10 active generation; byte14 selected,15 valid; signature@24 |
| 4 write | u16@4 offset,byte6 count4..24 divisible4,byte7 zero,data@8 | Strictly sequential/aligned; echoed offset/count and updated u16 cursor@8. Reject gaps, overlap, overflow and writes outside ready state. |
| 5 commit | No arguments | Accepted/busy only after exactly1696 bytes. Verify CRC/fields in bounded tasks, then publish the commit word last. Poll status/caps for generation+1. |
| 6 abort | No arguments | Stop an incomplete inactive-bank transaction; the active committed config is retained. Does not revert a completed commit. |
| 7 temporary lighting | byte4 mode0..3,byte5 master,byte6 low three lighting flags0..7 | Apply only to runtime RGB state; no save. Intended for temporary direct-color preview. Read prior FE81 state and restore it afterward. |
| 8 stop helpers | No arguments | Stop generated keyboard/mouse actions and unlock Windows, without changing stored profiles or VIA. |

CRC32 is standard reflected IEEE (polynomial0xEDB88320, initial/final XOR0xFFFFFFFF), covering the 1696 payload bytes in order. A lost ACK is not permission to retry an uncertain write: stop/resynchronize or abort and restart safely. Save confirmation requires the new generation and full payload readback, not just an ACK. Profiles cannot contain code, executable paths or serializer type names.

## Firmware storage contract

Two independent 2048-byte flash banks at [0xE780,0xEF80) and [0xEF80,0xF780). Existing QMK wear-leveling [0xF780,0xFFC0), its1056-byte logical EEPROM, all three VIA layers and259-byte macro capacity remain unchanged. Full's linker must cap executable/load data before0xE780 and preserve the final flag at0xFFFC.

Each bank has a32-byte firmware-owned header plus1696-byte payload. Header: magic8 `RBPLUSCF`; u16 schema@8=1; u16 length@10=1696; u32 generation@12; u32 payloadCRC@16; u32 headerCRC@20; u32 layoutTag@24=0x52424653; u32 commit@28=0x434F4D54. HeaderCRC covers bytes0..19 then24..27. The commit word is programmed last. A bank is valid only after all signatures, ranges, header/payload CRC and profile fields pass. Generation comparison handles wrap.

Writes use one64-byte erase sector per housekeeping step and four-byte program calls. No heap, payload-sized RAM cache or EEPROM allocation is added. Storage transaction globals must fit8 bytes total. Colors are read directly from committed flash within existing eight-LED RGB chunks. No ordinary key event or slider preview programs flash.

With no valid custom bank, use built-in colors and the existing legacy lighting user word. Once custom config exists, Fn+F9 schedules a bounded journal copy changing only the active profile's runtime mode/master/low-three flags. It retains all colors/other profiles and does not consume VIA EEPROM. Save feedback is shown after commit, not before completion. The previous bank remains valid through interrupted erase/write/verify. App-initiated saves stop generated helpers; retained completed Recorder slots need not be erased. A host-supplied raw flash address is never accepted.

Flashing a full64KiB image can erase both custom banks. Export VIA and saved custom settings before flash, then restore and verify after reconnect. Flasher success/checksum does not replace normal-mode identity/version verification.
