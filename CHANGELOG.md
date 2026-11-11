# Changelog

All new release notes are written in English. Version 0.x is experimental; hardware validation is recorded separately from build and host-test results.

## 0.11.0 preview — Color Lab and reactive lighting

- Color Lab adds drag and marquee selection, selection groups, solid and linear/radial fills, a palette, bounded 24-step undo/redo and the option to retain active-profile edits when connecting.
- Nine OpenRGB PC effects support palettes, zones, direction, scale and reactive overlays. Raw Input is filtered to the Red Blade device and recent events are bounded in memory; no scan-code or keystroke log is stored.
- Full 0.11 adds autonomous key fade, ripple and horizontal wave styles. Each new press restarts the single wave. Core remains OpenRGB/VIA only. Wire size and profile count are unchanged.
- Fixed French shortcut-map packaging: both PNGs are embedded in the main application. The map appears first in the guide, keeps its zoom across state refreshes, and has loading/error feedback. Real WebView2 regression checks cover both languages.
- Full 0.11 programming/checksum and identity passed on the preparation keyboard. VIA/macros and all four profiles were restored with complete readback after recovery from a malformed immediate identity reply. The owner confirmed typing, numpad, saved ripple/color and PC reactive typing. The read-only reconnect fix has regression coverage; no second physical flash, power-cycle, sustained-load or physical latency result is claimed. Core 0.11 is build/host-tested. Published 0.10 artifacts are unchanged.

## 0.10.0 preview — Studio and firmware

- Simplified active-profile editor, bilingual zoomable shortcut guide, application icon and explicit keyboard controls. The four-profile protocol remains compatible.
- Replaced the 0.9 native interface, including its confirmed dropdown disposal crash path, with a WebView2 interface; hardware-free UI checks exercise the bridge and navigation.
- Added official GitHub update checks and verified downloads, with optional automatic Studio restart when idle and no pending edits or hardware operation. Firmware flashing remains an explicit guided operation.
- Full 0.10 adds optional Caps Word and an automatic helper timeout, disabled by default. Core is rebuilt as 0.10. Studio provides six software RGB effects through OpenRGB. Full 0.10 programming/checksum, later identity, VIA/macro restoration and profile save/readback passed on the preparation keyboard; the owner confirmed input and Aurora. Corrected immediate post-flash identification retries after the first run exposed a timeout; power-cycle persistence remains untested.

## 0.9.0 preview — Studio and Full firmware

- Portable Windows x64 Studio: English/French, AZERTY/QWERTY legends, offline editing, guided Full/Core flash and explicit configuration jobs.
- Full: four persistent profiles, each with 109 HSV key colors, 24 effect colors and ten module enables; independent two-bank flash storage preserves the existing VIA EEPROM allocation.
- Fn focus hides unrelated keys while Fn is held; disabled module shortcuts are hidden. Existing Turbo and temporary Recorder slots remain available.
- 0.9.0 physical persistence, power-cycle and flash validation has **not** been performed. Last keyboard-validated Full release: 0.8.1.
- Studio uses the project's logo, crimson/charcoal/white styling and a physical 109-key editor. Guided flash verifies a normal-to-bootloader transition on the same USB port and uses protected staging; RGB recovery respects GUI-only OpenRGB setups.

## Unreleased — Windows tooling recovery

### Fixed
- Restart standalone OpenRGB before Aurora after HID measurements, waiting for the SDK on the original server port (PR #1).
- Roll back configuration and recover the previous RGB applications/service when Windows setup fails (PR #1). Recovery now also covers failed restarts, individual rollback-copy failures and callers that escalate warnings; the original error is preserved and standalone application commands are retained.
- Report the selected firmware edition and its actual minimum version when installing Fn bindings: Full 0.7.0, OpenRGB Core 0.8.0 (PR #2).
- Add nine simulated Windows recovery/restart scenarios to CI and two installer version regressions. Firmware and published BINs are unchanged.

## 0.8.1 — Dedicated Windows-lock status

### Fixed
- The upper-right Windows indicator now follows Game Mode: Fn + Windows turns it on when Windows keys are disabled; unlocking turns it off. Holding/releasing Fn alone no longer changes it. Both Windows-key RGB backlights retain their red locked status, and Fn retains its independent RGB hints.
- The dedicated status light remains authoritative with RGB Off. Defaults, short Fn + Esc/boot entry, initialization and USB suspension turn it off with Game Mode. Core has no added Windows lock and leaves this indicator off.
- Actual Full/Core keymap-hook regressions cover Fn press/release, lock/unlock, RGB Off, defaults, suspend/wake and boot entry. Eight actual-C suites and eleven Python checks now run in CI. Hardware scope is recorded separately.

## 0.8.0 — Latched Hyper speeds, reactive white and mouse Turbo

### Added
- Two separately built editions: OpenRGB Core (RGB/VIA only, no game features) and full RedBlade+. Each has its own BIN, manifest and exact source package.
- Higher, persistent speed selections: Recorder 0.5×–2× in 0.25× steps, then 4×, 8× and Hyper. Hyper requests one key edge per millisecond while keeping the minimum 5 ms loop release gap. Releasing + keeps the selected value; − steps back down.
- Hold Fn + numpad −/+ for repeated steps (400 ms, then 150 ms); during active playback, bare numpad −/+ also adjusts speed. Outside playback, including recording, bare −/+ remains normal input. F8 restores Turbo 50/s and Recorder 1×.
- Keyboard/mouse Turbo targets up to 250/s: classic 5–100/s steps of 5, then steps of 25. Fn + R retains the classic 25/50/100 presets. A 16-bit fractional accumulator keeps intermediate high cadences accurate.
- Fn + T, then Volume − or Volume + selects left-click or right-click Turbo on release. The active volume key blinks amber/purple and Fn matches. The same volume target, Esc, Fn + T, F8 or USB suspension stops and releases it. Normal media behavior is retained outside click selection/running.
- Reactive white fallback after five seconds without RGB: physical typing, Turbo and Recorder replay light red key trails. Explicit Fn + F2 uses the same white/red effect. Default Host still gives fresh Aurora/OpenRGB colors priority; lock states and Off/suspend remain authoritative.
- Refreshed categorized shortcut diagrams and feature showcase images in English/French.

### Resources and limits
- Fixed reactive lifetimes use 55 bytes plus a count; the replay LED lookup lives in flash. Three Recorder slots now share 192 bytes, about 48 simple presses total, to preserve SRAM/stack reservations. Long delays/modifiers reduce capacity. The old, unmapped lighting-guide implementation is retired; its custom keycode stays reserved.
- Physical reactive lights follow matrix positions. Replay lights use factory usage positions, including when VIA remaps the keyboard. No recorded payload is exported or persisted.
- Native tests, ARM resource checks, actual device checks and unmeasured physical latency are distinguished in the manifest/audit.

## 0.7.0 — Local on-device validation build · Timed Recorder, optional loops and simpler shortcut groups

### Added
- Three temporary Recorder slots sharing a fixed 320-byte RAM arena. Fn + Calculator arms selection; Fn + 1/2/3 records into a slot; Calculator finishes. Fn + a slot replays once; Shift + Fn + a slot loops/toggles it. The loop chord waits for Fn/Shift release without stealing held modifiers.
- Recorded keyboard press/release timing, holds and physical modifiers, with live 0.5×–2× target speed in 0.25× steps through Fn + numpad −/+. The same controls use the last selected Recorder/Turbo context; Fn + R selects Turbo.
- Breathing red recording, green one-shot, cyan loop and amber selection/feedback on Calculator, Fn and the active slot. A loop releases generated keys between iterations with a minimum 5 ms gap; missed deadlines never cause bursts.
- Balanced capacity/30-second completion, physical-key takeover, GUI-lock checks, explicit stop, metadata-only read diagnostics and USB lifecycle cleanup. Slots are RAM-only; simple powered suspension can retain them, while USB reset/deconfiguration, disconnect or power loss clears them. F8 keeps recordings; F9 saves lighting only.
- Categorized English/French diagrams and guides explicitly covering Recorder, Turbo, profiles, each F5–F9 option and media controls.

### Removed / changed
- Remove default Fn + Up/Down profile cycling and Fn + Enter wave replay. Automatic startup/resume animation remains available through F5.
- Fn + Calculator now controls Recorder instead of the temporary shortcut guide. No additional menu or desktop service is required.
- Update the backed-up VIA installer with 33 current bindings plus explicit clearing of the three removed commands; base keys, VIA macros and unrelated Fn cells remain intact.

### Validation
- Actual C recorder/control/rendering/protocol tests cover timing, long delays, modifiers, ownership, speed, loop release/toggle, no catch-up, full buffers with held keys, slot compaction, sleep/USB reset behavior and read-only metadata. Eight Python tooling checks cover migration removals and refusal to benchmark an active Recorder as well as prior recovery cases.
- Native benchmark includes idle/loop Recorder scheduling. The manifest/audit distinguishes build, on-device and physical latency validation. Stack reservations and the shared scan/PWM timer are unchanged; the artifact validator requires at least 64 unassigned SRAM bytes after reserved stacks and all static data.

## 0.6.0 — Dynamic fallback and a clearer Fn layer

### Added
- Fn + either Windows key toggles Game Mode; both Windows logos turn red. Shift + Fn + Windows explicitly unlocks.
- Fn + Calculator now shows a temporary shortcut-lighting guide for 20 seconds. Press again, use Esc, or Shift + Fn + Calculator to close it. The normal Calculator key is retained.
- Without RGB data for five seconds: green Caps Lock, white active numpad with green Num Lock, very dim inactive numpad, and amber Scroll Lock. Fresh host colors and explicit Off/local/native choices keep priority.
- English/French visual shortcut maps and detailed guides covering Turbo selection, live cadence, Fn colors, dynamic lock states and recovery.

### Changed
- Restore the memorable Fn + Esc bootloader combination, with a two-second hold guard. A short press stops helpers/unlocks Windows; Fn + Shift + Esc remains accepted.
- Fix the Fn status backlight: LED 102 is Fn, while LED 103 is Right Ctrl. Mapping checks now verify the base-layer meaning as well as coordinates.

### Validation
- Retain the visible pure-red full-brightness wave, generic Turbo and live 5–100/s cadence from the 0.5.0 on-device validation build. The user confirmed wave/media lighting, target choice, live changes and stop on that build; the Fn placement report prompted this correction.
- Host rendering checks cover all 17 numpad keys, host takeover, lock-state colors, Windows LEDs and Fn/Right-Ctrl placement. Physical validation and USB timings are recorded in the manifest/audit, separately from physical key-input latency.

## 0.5.0 — Local on-device validation build · visible lighting and configurable Turbo

### Fixed
- Ignore unused global HSV/speed fields in Direct-mode requests. OpenRGB can send zeros there; those zeros previously hid the wave and four media fallback lights while streamed pixels still worked.
- Use linear QMK HSV conversion for host/custom/status colors instead of applying a second CIE dimming curve. Standalone QMK effects keep their usual conversion.
- Make the red wave a visible two-second sweep with a brighter crest and a 400 ms host handover.
- Capture releases for all Fn controls, including rate adjustment, when Fn is released before the key.

### Added
- Fn + T, then a basic keyboard key: repeat the chosen VIA base-layer assignment. Selection does not type, starts on release and expires after ten seconds. Esc and system/consumer/modifier/layer/macro actions remain outside the target set.
- Fn + numpad −/+ adjusts the target by 5 presses/s, from 5 to 100, before or during repetition. Fn + R retains the 25/50/100 presets. Fractional milliseconds are distributed without divisions or catch-up bursts in the scheduler.
- Fn RGB status: dim blue idle, cyan held, blinking amber selection, red Turbo, green forward hold, purple Game Mode. The repeated physical key is red; T and cadence controls are highlighted with Fn held.
- French shortcut guide and checked 33-binding upgrade map. Existing E/F/Space presets, emergency stop, VIA base keys and macros are retained.
- Actual custom-effect rendering regression tests, generic selection/release/timeout/ownership checks, intermediate-rate tests and read-only armed-selection diagnostics.

### Validation
- Full ARM build and native regression/tooling checks. Hardware validation scope is recorded in the manifest/audit; host timing is not physical key latency.
- Version 0.4.0 was flashed with programming/checksum verified and Aurora connectivity confirmed; its wave/media visual failures motivated these fixes.

## 0.4.0 — 2026-11-04 — RedBlade+

### Added

- Project identity: **RedBlade+ — OpenRGB Firmware** and an updated keyboard/RGB/red-blade logo.
- Fn media deck: Calculator toggles Game Mode; Mute plays/pauses; Volume −/+ select previous/next track. Shift adds Windows unlock, media Stop and lighting brightness controls.
- Windows-key lock, including simple GUI-modified QMK shortcuts, with a red Calculator indicator.
- Turbo E/F/Space with red target LEDs, one target at a time, selectable 25/50/100 target presses/second and no catch-up bursts.
- Forward-key hold (W on QWERTY / Z on AZERTY) with a green indicator. Physical target keys take over; Esc stops helpers, and Fn+Esc also unlocks Windows.
- Local white/red profiles, host profile, lighting toggle, global brightness and profile cycling.
- Optional startup wave, optional four-key white fallback and selectable white/dark idle fallback.
- Manual red-wave replay, RAM defaults and explicit Fn+F9 lighting-preference saving. Game helpers are never saved.
- A checked 30-binding Fn map and a backed-up, verified layer-1 installer with rollback attempts.
- Read-only controls diagnostics, a Windows native benchmark, read-only HID timing tools and a code audit.
- English landing page/changelog and a complete French README switch.

### Changed

- Bootloader entry now requires **Fn+Shift+Esc held for 2 seconds**. Fn+Esc alone is the immediate stop/unlock shortcut.
- Fn arrows now control brightness/profile navigation; the old standalone hue/saturation/speed bindings are replaced.
- Master brightness scales host colors and status indicators as well as local effects.
- Game Mode and game helpers clear on USB suspension/disconnection. Helpers never resume automatically.

### Verified / limitations

- Actual C protocol, controls, malformed-packet boundaries, helper ownership/release order, timer wrap, preferences and tooling recovery are tested.
- Firmware builds for the pinned Red Blade FS board. Native timing results are host-only; physical key latency remains unmeasured.
- This release has not yet been flashed and physically verified on the target keyboard. Version 0.1.0 hardware checks do not count as validation of the new features.

## 0.3.0 — 2026-11-04

- Added the red startup/resume wave, white idle fallback, four-media-key fallback and explicit USB-state handling.
- Added project artwork and native host tests. Build-tested; not flashed on the target keyboard at publication.

## 0.2.0 — 2026-11-04

- Added the Direct-mode RGB watchdog, startup sweep and read-only firmware diagnostics.
- Added timer-wrap, timeout and suspend/resume host tests. Build-tested; not flashed on the target keyboard at publication.

## 0.1.0 — 2026-11-04

- Initial SonixQMK + VIA + VialRGB integration for the 109-LED Red Blade FS.
- Added Windows setup, native OpenRGB/Aurora integration and physical Aurora mapping.
- Programming/checksum, VIA/VialRGB discovery, LED positions, typing and Aurora colors were checked on one keyboard. Hardware latency was not measured.
