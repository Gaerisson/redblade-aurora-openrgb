# Installation & Technical Reference

**Studio and firmware 0.10 preview:** Full/Core flashing is guided; flash and persistence remain unvalidated on hardware. The physical keyboard remains on Full 0.8.1. Studio requires Microsoft Edge WebView2 Runtime. See [Studio](../app/README.md).

**English** · [Français](INSTALL.fr.md) · [Project](../README.md) · [Shortcuts](SHORTCUTS.md)


SonixQMK firmware for the **Designed by GG Red Blade Full Size ISO** keyboard with numeric keypad, USB **320F:5136**, **SN32F248B** controller and **109 LEDs**.

**Status: experimental.** Version 0.1.0 was flashed and checked on a Red Blade Full Size: programming and checksum verification, VIA 13, VialRGB 1, 109 LEDs, typing and Aurora colors. Version 0.7.0 adds Fn controls, game helpers and saved lighting preferences; its validation status is recorded in `dist/manifest.json`. Hardware latency and extended testing remain unmeasured. This repository contains no personal backups.

```text
Aurora (effects and game integrations) → OpenRGB SDK server → VialRGB → Red Blade FS
```

OpenRGB can also be used directly or with other software that supports its SDK. Games use the host software's integrations; this firmware does not promise direct compatibility with every game. VIA retains key remapping and macros. The extension implements the VialRGB subset needed by OpenRGB, rather than the full Vial configuration interface.

## Fallback lighting and red wave — 0.7.0

- **USB suspended or unconfigured:** lights off takes priority, even if the port still supplies power. USB state changes are tracked explicitly alongside QMK's `RGB_MATRIX_SLEEP`. Previous colors are cleared; neither fallback lighting nor the animation turns the keyboard back on in this state.
- **No RGB data in Direct mode:** switch to **white at approximately 25% brightness after 5 seconds**, on the next render (33 ms minimum frame interval). VIA reads, diagnostics and invalid packets do not extend this timeout. A new color packet takes over; cleared colors are not replayed.
- **Calculator, Mute, Volume −, Volume +:** dim white when the stream assigns black or provides no color for these keys. A non-black host color takes priority. These are physical LED indices 16..19, checked against the pinned board definition. This policy also replaces intentionally black colors on those four keys: the protocol cannot distinguish them from keys left black by Aurora. The global **Off** command and USB suspension remain authoritative.
- **Startup / USB resume:** a curved **2-second** wave, fading from deep red to scarlet, with a soft trail, gradual entrance and a fade into the latest received colors during the final 400 milliseconds. Packets continue to be received during the animation; typing is not blocked. Disabled lighting and saved standalone effects are respected. Ordinary USB notifications (Num Lock, HID protocol changes, etc.) do not restart the animation.
- **Standalone effects:** white fallback and the four-key policy apply only to Direct mode. VIA standalone effects remain available and also turn off during USB suspension.

Durations and brightness can be changed in `firmware/aurora_via/config.h` before building: `REDBLADE_RGB_TIMEOUT_MS`, `REDBLADE_BOOT_ANIMATION_MS`, `REDBLADE_BOOT_BRIGHTNESS`, `REDBLADE_FALLBACK_BRIGHTNESS`, `REDBLADE_MEDIA_BRIGHTNESS`. The timeout is checked once per RGB frame, and the wave uses the same eight-LED rendering chunks with integer arithmetic. No key-scanning timer is stopped; no blocking delay or dynamic allocation is added. Only the explicit Fn + F9 save action writes RedBlade+ lighting preferences; normal controls and RGB streaming do not.

The firmware knows the USB bus state, not the Windows power state. Lights turn off at PC shutdown when the USB controller suspends or unconfigures the connection. If a host genuinely keeps the bus configured and active after shutdown, missing RGB data alone cannot distinguish that state from Aurora being closed: white fallback then applies. That case would require a USB power/ErP setting or an explicit shutdown signal from the PC. Actual shutdown and wake behavior must therefore be checked on the target machine.

In Direct mode, the host application must keep sending colors, even for a visually static profile. An OpenRGB color sent only once changes to white after 5 seconds; use the standalone **Static** mode for a fixed color without active host software.

Read-only diagnostics (stop OpenRGB, Aurora and VIA first):

```powershell
python tools/redblade.py status
```

This command does not extend the timeout. It reports firmware version, mode, stream freshness, last-packet age and the accepted-packet count. Its private `FE 80` extension, format 1, is separate from VialRGB: version at bytes 3..5; flags at byte 6 (fresh stream, animation, suspension, RGB enabled); mode at byte 7; age/timeout/count/startup duration as little-endian 32-bit integers at offsets 8/12/16/20; `RDBLFS01` signature at bytes 24..31. An age of `FFFFFFFF` means no retained colors.

## Performance choices

- Requested USB polling interval: **1 ms**. NKRO, the key matrix, pins and the factory 20 ms `sym_eager_pk` debounce are preserved. This is eager debounce: it does not add an initial 20 ms wait to each key press.
- The Sonix hardware clock retains the value calculated by the upstream driver for this board: **720,896 Hz**. This is the shared timer clock, not a measurement of the complete matrix scan rate or latency.
- The patch decouples this clock from the rendering chunk size. Rendering processes at most **8 LEDs** per task iteration, with at least **33 ms** between frames.
- One **327-byte** HSV array stores the latest colors. No dynamic allocation, frame queue or EEPROM write is used in the Direct RGB path. Both upstream Sonix driver buffers are retained.
- A valid Direct packet copies at most **9 colors**. HSV → RGB conversion runs in the rendering task, not the USB request handler.
- Retained standalone effects are simple: solid color, breathing, horizontal cycle, gradients and alphas/modifiers. Aurora/OpenRGB supplies complex and reactive effects; their previous firmware buffers and key-event tracking are removed.
- Proposed Aurora settings: **34 ms** update interval, approximately **29.4 FPS**. No additional Windows service.

These choices bound the added work. They are not hardware latency measurements: comparison with the original firmware still requires a USB analyzer or other measurement setup. Standard VialRGB transport uses 13 exchanges for a complete 109-LED frame; packets may arrive while a frame is being rendered.

## Building

Pinned upstream sources: [SonixQMK commit 62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60](https://github.com/SonixQMK/qmk_firmware/tree/62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60).

In a configured QMK environment (QMK MSYS on Windows, or QMK tools on Linux):

```sh
git clone https://github.com/SonixQMK/qmk_firmware.git qmk_firmware
git -C qmk_firmware checkout 62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60
bash tools/build.sh /path/to/qmk_firmware
```

`prepare_firmware.py` rejects other commits, checks the 109-LED map, installs the keymap and applies one driver change: a configurable clock parameter that leaves the default behavior unchanged for other boards. QMK and its submodules retain their respective licenses.

## Backup and flashing

Flashing replaces the firmware and may reset VIA keys/macros. Back up first, keep another input device available and retain the manufacturer's recovery firmware.

```powershell
python tools/redblade.py backup --output C:\private-path\via-backup.json
```

The backup contains layers and macros: **keep it private**. It is not a complete firmware dump. Do not blindly restore internal keycodes across different QMK generations.

1. Confirm the Full Size model, not the Red Blade 75.
2. On RedBlade+ 0.7.0+, hold **Fn + Esc for 2 seconds**. Earlier firmware may enter immediately with Fn + Esc.
3. With [SonixFlasherC](https://github.com/SonixQMK/SonixFlasherC), verify the bootloader identity **0C45:7040 / SN32F24xB** before writing anything.
4. For this controller, use the raw binary at offset **0**; do not use an SN32F26x jumploader.

```powershell
sonixflasher.exe -v 0c45/7040 --info
# Only after verifying the controller and explicitly deciding to flash:
sonixflasher.exe -v 0c45/7040 -f dist/redblade-fs-aurora-via.bin -o 0
```

[SonixFlasherC](https://github.com/SonixQMK/SonixFlasherC) is the current reference tool. The older `sonix-flasher` repository is archived and discouraged by its maintainers.

To restore the manufacturer firmware, follow the [Designed by GG procedure](https://designedby.gg/qmk-installateur-simplifie/) and use the official **Red Blade Full Size** archive. The manufacturer requires restoring the original firmware before using its QMK update installers.

## Windows setup

OpenRGB must include the **QMK VialRGB Devices** controller (version checked on the setup PC: OpenRGB 1.1). Add this entry in its VialRGB settings: name `Designed by GG Red Blade FS (VialRGB)`, VID `0x320F`, PID `0x5136`, then fully restart OpenRGB.

The PowerShell 7 script configures this, backs up existing files, sets up the local SDK and Aurora, and prepares application startup:

```powershell
pwsh -File tools/configure-windows.ps1        # read / describe only
pwsh -File tools/configure-windows.ps1 -Apply # apply and restart RGB applications
```

After flashing, install the [new Fn bindings](SHORTCUTS.md#upgrading-an-existing-via-layer) if an old VIA layer survived, while RGB clients are stopped. Then follow the setup below. Commands are run from the project root.

The connection uses **127.0.0.1:6742**. In Aurora, enable **OpenRGB (RGB.NET)**. Choose **Fn + F1** for host lighting control. The keyboard must appear in OpenRGB with a **Direct** mode before validating Aurora effects. The script does not flash anything or enable the dangerous option for devices without Direct mode.

After flashing, run `configure-windows.ps1 -Apply` again. It temporarily stops RGB clients, explicitly selects **Direct** on the keyboard and saves that setting once before restarting OpenRGB/Aurora. This fixes the case where SDK colors are sent while the keyboard remains in a standalone effect (for example, solid red). The configuration write does not occur during RGB streaming.

To change only this setting, with OpenRGB, Aurora and VIA stopped:

```powershell
python tools/set-direct-mode.py --save
```

Close VIA while using OpenRGB: both share the Raw HID interface and may receive a response intended for the other application.

## Verification

```powershell
python tools/redblade.py verify
```

This read-only check verifies the RGB signature, version, 109 LEDs and their unique positions. Then check red/green/blue colors in OpenRGB, the numeric keypad and ISO keys, followed by Aurora Direct mode, NKRO typing, wake from sleep and USB reconnection.

Stop OpenRGB, Aurora and VIA before the Raw HID commands `redblade.py backup`, `redblade.py verify` or `set-direct-mode.py`. A shared interface with an active client may receive another tool's response. The mapping script below uses the OpenRGB SDK and can run while its server is active.

Once the keyboard appears in OpenRGB, establish its explicit physical mapping in Aurora:

```powershell
pwsh -File tools/configure-aurora-mapping.ps1        # check all 109 actual indices
pwsh -File tools/configure-aurora-mapping.ps1 -Apply # back up and write the mapping
```

Then restart Aurora. The script uses the RGB.NET provider shipped with Aurora, so mapping does not depend on OpenRGB recognizing QMK key names. It includes the numeric keypad, ISO keys and four extra keys.

If OpenRGB is installed as a Windows service, `configure-windows.ps1` reuses that service and its configuration in `service_config`. Applying these settings then requires an elevated console; no second server is added.

The C tests compile the actual firmware handler against a small QMK fixture: VIA command passthrough, discovery, buffer boundaries, all LED positions, colors and modes. They also cover the wave and its fade into recent host colors, the four fallback keys, the exact 5-second boundary, requests that must not extend the timeout, recovery without an old frame, timer wraparound and suspend/resume. The GitHub workflow runs them with AddressSanitizer and UndefinedBehaviorSanitizer. It does not replace testing on a real keyboard.

## References

- [Manufacturer: Red Blade Full Size](https://designedby.gg/product/red-blade-01/)
- [SonixQMK board definition](https://github.com/SonixQMK/qmk_firmware/tree/62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60/keyboards/designedbygg/redblade/rbfs)
- [VialRGB protocol](https://github.com/vial-kb/vial-qmk/blob/vial/quantum/vialrgb.c)
- [OpenRGB VialRGB controller](https://github.com/CalcProgrammer1/OpenRGB/tree/master/Controllers/QMKController/QMKVialRGBController)
- [Aurora](https://github.com/Aurora-RGB/Aurora)

The keymap is derived from Dimitris Mantzouranis's SonixQMK keymap. Added sources are distributed under GPL-2.0-or-later; see `LICENSE` and the upstream component notices.

## RedBlade+ controls diagnostics

`python tools/redblade.py status` also reports Game Mode, local profile, master brightness, animation/fallback options, Turbo target/rate and forward hold on 0.7.0+. These are read-only diagnostics. Details and behavior are documented in [Shortcuts](SHORTCUTS.md).

## Direct-mode compatibility fix — 0.7.0

Direct mode carries brightness in each pixel. Its unused scalar HSV/speed fields may be zero in OpenRGB requests; these fields no longer overwrite QMK engine brightness in Direct mode. Standalone mode still uses them. The custom renderer and RGB status use QMK’s no-CIE conversion, avoiding a second dimming curve on host colors. The two-second red wave peaks at HSV value 255 before master/engine limits; media fallback remains linear value 64.

FE81 byte 19 uses bit 0 for armed/pending Turbo selection and bit 1 for the temporary shortcut guide. Bytes 16/17 still carry the selected HID usage and target cadence, now any supported basic key and 5–100/s. Diagnostics never renew the RGB timeout. See [shortcuts](SHORTCUTS.md).

## Recorder metadata — 0.7.0

The Recorder is volatile, fixed-size and independent of the VIA macro buffer. FE82 format 1 returns state at byte 6 (0 idle, 1 armed, 2 recording, 3 playing, 4 feedback, 5 waiting for loop-chord release), selected slot at 7 (0..2, 255 none), speed in quarter units at 8, error at 9, three little-endian slot byte lengths at 10/12/14, total bytes at 16, capacity at 18, maximum recording milliseconds at 20 (16-bit), loop flag at 22 and UID at 24..31. No recorded key/timing payload can be read through this diagnostic. FE81 byte 19 additionally uses bit 2 for Recorder active/waiting and bit 3 for Recorder rate-control context. Reads do not renew the RGB watchdog.

See [the Recorder guide](SHORTCUTS.md#1-recorder--three-temporary-slots) for controls, buffer/time limits and USB RAM lifetime. The Fn installer applies 33 bindings and 3 explicit removals.
