# RedBlade+ 0.8.1 — code review and validation

**0.13 source verification:** Turbo Fn/target cues share a frame phase driven by actual output events, including standard lighting effects. Middle-button Turbo consumes the media key's audio action. Anti-AFK is manual, mutually exclusive with other movement/replay helpers, and stops on basic or advanced VIA key activity; it retains no input log. The existing profile format is unchanged. Full links with 80 usable SRAM bytes and 47,396 program bytes; Core 0.12 and its BIN remain unchanged. USB polling, RGB frame interval and the shared PWM/scan clock are unchanged. Factory repeat cadence is 60/s; this is not a claim of lower first-input or physical latency. Build/host checks and physical acceptance are recorded separately.

**0.12 verification:** Extended reactions reuse the existing packed TTL and boot-only fields; no additional SRAM is allocated. Full retains 88 usable SRAM bytes and uses 46,824 program bytes; Core retains 544. Tests cover continuous typing expiration, retrigger, extended styles, modifier overlay priorities, Fn visibility during Full Off, and incompatible downgrades across all profiles. Studio adds 8-second bounded PC reactions, three presets, a virtual Fn reference and a bilingual setup assistant with a verified OpenRGB download. Native and WebView tests cover identity replies, VIA dimension refusal, archive validation, language messages and UI interactions. No scan/PWM clock or GPIO current setting changes. See [brightness audit](RGB_BRIGHTNESS.md). Hardware acceptance for 0.12 is recorded in release notes separately from these build and host checks.



**0.11 source verification:** Color Lab, transient Raw Input effects and autonomous reaction styles compile and pass host and real WebView2 interaction tests. The consolidated Sol 6.1 review identified inspector focus loss and preview/runtime divergence; both were corrected and regression-tested. Full uses 46,100 program bytes with 88 bytes usable SRAM; Core has 544 bytes. Full 0.11 programming/checksum, fresh-handle recovery identification, VIA/macros and four-profile readback passed on the preparation keyboard. The owner confirmed saved ripple/color, typing, numpad and software reactive typing. The malformed immediate-reply fix was exercised during recovery and is regression-tested; no second physical flash was performed. See release notes and hardware-validation.json; power-cycle, sustained-load and physical latency measurements remain untested.

**0.10 development:** The 0.9 dropdown disposal crash was confirmed in Windows logs; Studio now uses WebView2. Full and Core 0.10 have been rebuilt. Hardware-free tests cover protocol/options, update transport, software effects and WebView bridge interactions. Full 0.10 programming and checksum passed; an immediate HID identify timeout stopped the first guided job before restoration. A separate later identify/read succeeded; the original VIA/macros were restored with full readback, and a profile save/readback passed. The owner confirmed typing, numpad and Aurora. The reconnect fix retries only read-only identification on fresh handles for 30 seconds and passes simulated transition tests; another physical flash has not been performed. Power-cycle persistence remains untested. Full uses 45,848 program bytes and has 88 bytes unassigned SRAM; Core has 544 bytes unassigned.

The 0.9.0 Studio integration received a consolidated source review with Sol 6.1. Its two flash blockers (user-writable privileged staging and accepting an unrelated bootloader) were corrected with protected atomic staging/IPC and a required same-port normal-to-bootloader transition. OpenRGB GUI-only restoration was corrected too. Follow-up checks cover inherited owner rights, cross-account request paths, strict SIDs and atomic result replacement while a UI snapshot remains open. Studio builds without warnings/errors and passes 119 hardware-free assertions. The UAC/ACL and USB-location paths still require real-world validation; no 0.9.0 flash, power-cycle persistence or physical latency result is claimed.

**0.9.0 status:** The observations below concern earlier versions unless explicitly identified otherwise. Studio 0.9.0 storage, persistence, power-cycle and flashing have not been validated on physical hardware.

## 0.9.0 development verification

The Full and Core ARM builds succeeded. The Full image uses 44,992 program bytes and has 104 bytes of unassigned SRAM after aligned heap and reserved stacks; Core has 544 bytes. Native host verification passed nine Full and two Core suites, fourteen Python checks and nine Windows recovery scenarios. The four-profile store tests simulate interrupted writes, recovery and validation; they are not hardware tests. The physical keyboard remains on public 0.8.1. Persistence through power loss, 0.9.0 flashing, reconnect and real-device behavior remain unvalidated.


Review date: **2026-11-04**. Scope: actual protocol, controls, Recorder, keymap/rendering, Windows tools, migration, source packaging and documentation. Build/host results and on-device checks are recorded separately.

## Changes and evidence

| Area | Implementation / checks |
| --- | --- |
| RGB bounds | Validate the entire 9-color packet range before changing the 327-byte host buffer. Exhaustive 76,800 start/count boundary cases remain covered. |
| Direct brightness | Ignore unused global HSV/speed fields in Direct mode. Linear QMK conversion avoids double CIE dimming. Actual custom-effect tests cover zero scalar fields, red wave geometry, visible four-media fallback and host handover. |
| USB/watchdog | Unsigned elapsed time, five-second fallback, stale-frame clearing and explicit USB-state hooks. Suspension/unconfigured has priority over waves, fallback and overlays. Diagnostics do not renew the watchdog. |
| Scan / input | Requested polling remains 1 ms; factory eager debounce, NKRO and the shared 720,896 Hz matrix/PWM timer are preserved. Eight base LEDs are rendered per cooperative task; no added timer stops key scanning. |
| Windows / releases | Both Windows RGB indices and Fn are checked against actual base-layer assignments (Fn is LED 102; Right Ctrl is 103). All non-trigger Fn actions own their physical release. Held GUI modifiers are cleared without clearing Ctrl/Alt/Shift. |
| Turbo | Cached interval/remainder; one edge per housekeeping pass, no division in that scheduler, no burst catch-up. Physical target presses take over. Forward hold and Turbo cannot own the same usage. |
| Recorder storage | Three offset/length slots share a fixed 192-byte byte arena. A typical key edge uses two bytes; delays >=255 ms use four. Replacing one slot compacts the arena once on a user action and preserves the other slots. No allocator or EEPROM writes are used. |
| Recorder capacity | Each capture reserves space to finish all currently held keys. Full memory or the 30-second limit saves a balanced sequence; it does not release a still-physical key from the live keyboard report. Multi-key-full and long-held-key cases are tested. |
| Recorder ownership | Recording observes accepted basic key edges and physical modifiers. Playback refuses a nonempty keyboard/held modifiers, owns a private bitset of generated usages and releases only those on stop. A real key takes over before QMK processes it. Media/Fn/advanced macro actions are not recorded. |
| Recorder timing | Millisecond edge deltas; 1x is exact before USB scheduling. Cached Q8 multipliers implement target 0.5x–8x plus Hyper speeds, with rounded intermediate ratios. Playback emits at most one edge per pass and resets timing to the actual edge, never draining missed events in a burst. |
| Loop chord | Explicit Shift+Fn+slot playback waits for Fn/Shift release rather than clearing real modifiers. A physical Shift stops an existing loop and retains that stop gesture until release, so the same loop shortcut toggles off in either Fn/Shift order. Pending playback retains its GUI-lock check. |
| Loop boundary | Release generated usages between passes; minimum 5 ms inter-loop gap is retained across live speed changes. Looping is an explicit runtime flag, never persisted or automatically resumed. |
| Lock / lifecycle | Game Mode refuses a recording containing GUI events, including a deferred loop. Enabling Game Mode stops Recorder. Simple USB suspension stops and can preserve completed RAM slots; USB reset/deconfiguration clears the arena. |
| Stops / defaults | Esc finishes capture or stops playback; short Fn+Esc also unlocks. Holding Fn+Esc two seconds enters the bootloader; releasing either key cancels. F8 resets lighting/rates/speed and retains current RAM recordings; F9 saves lighting only. |
| Privacy / tools | FE82 exposes state, sizes, speed and errors, not the recorded key/timing payload. HID benchmarks are read-only and reject active/armed/waiting Recorder/Turbo/forward states. Personal backups remain outside the repository. |
| Migration | Check 33 current Fn bindings plus 3 explicit clears for removed Up/Down/Enter commands. Full private VIA backup before writes, ACK/readback validation and rollback including a write whose ACK was lost. |
| Packaging / diagrams | Exact clean project commit, pinned QMK and only the approved timer driver patch; include tracked dependency sources and the selected keymap. Other untracked keymaps/notes, backups, credentials, Git metadata and build caches are excluded. EN/FR diagrams use the actual 109-position layout. |

## 0.8 edition and speed changes

- Full: persistent tap/hold rates, 400 ms initial hold and 150 ms repeated steps.
  Turbo targets 5–250/s, 16-bit fractional accumulation; Recorder adds 4x, 8x and
  Hyper (one edge per millisecond target). Loop gap stays at least 5 ms.
- Mouse Turbo sends button-only shared HID reports through the existing Turbo
  scheduler. Left/right volume targets use amber/purple LEDs and matching Fn.
  Stops release the generated button. The mouse report adds SRAM overhead, so
  the Recorder arena is 192 bytes. Stacks remain 768 + 1536 bytes.
- Reactive white stores 109 four-bit frame lifetimes in 55 bytes plus a count.
  Physical input uses its actual matrix position; replay uses a factory lookup
  in flash. Trails are absent over live host RGB and cleared across Off/suspend.
- Core is a separately linked module with RGB/VIA and guarded boot entry only.
  It has no game/Recorder/mouse/reactive modules or local animation. Its five
  second RGB watchdog goes dark. [Edition manifests and source bundles](EDITIONS.md)
  distinguish build/host checks from actual hardware validation.
- Local native benchmark includes reactive-white frames. These PC timings do
  not measure switch latency, MCU time, USB load or game acceptance at high rates.

## Resources and performance

- Board: Full Size ISO `320F:5136`, SN32F248B, 109 LEDs. SonixQMK `62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60`, ARM GCC 15.2.0.
- Raw image: 65,536 bytes with Sonix flag/padding; vectors, size, SHA-256, version, keymap and actual linker heap boundary are checked by `tools/check_release.py`.
- Reserved stacks remain **768 bytes main + 1,536 bytes process**. The manifest reports total linked SRAM including these reservations and alignment. The artifact validator requires at least **64 bytes unassigned** after all static data and stack reservations; this unassigned amount is not the stack capacity.
- Recorder arena is fixed at 192 bytes plus bounded metadata. No dynamic allocation path was added. The [native reference](performance-reference.json) includes ordinary key hooks, 109-LED frames and idle/active-loop Recorder task costs on the PC, with transport mocked.
- [USB reference](hid-reference.json) measures information-request round trips with clients stopped and helpers off. It does not measure physical switch input, key scan/debounce delay, active RGB/Recorder load or the MCU's exact execution time.
- Runtime stack high-water marks, physical key latency and sustained RGB/Recorder stress remain unmeasured. Standard QMK report I/O still depends on USB scheduling.

## Validation performed

- Eight actual-C suites (including actual Full/Core keymap hooks): protocol, controls, rendering, Recorder, high-speed/reactive/mouse, and independent Core. Coverage includes exact timings, >255 ms gaps, wraparound, Ctrl/GUI ownership, live speed, full buffers with held keys, slot compaction, single/loop playback, Shift chord release/toggle, sleep stop/reset wipe, dynamic fallback and RGB host priority.
- Eleven Python checks cover mapping agreement, percentiles, read-only timing, active Recorder refusal, explicit removals, lost-ACK rollback, HID cleanup/response matching, edition migration/refusal and checksum update order.
- Full ARM build with warnings as errors, native Windows C tests and native benchmark. GitHub runs C suites with AddressSanitizer/UndefinedBehaviorSanitizer and the Python/artifact/native checks.
- On 0.6.0, programming/checksum, protocol, positions and Fn map were verified; the user confirmed Caps/Num fallback, corrected Fn placement, Windows LEDs and Calculator's previous guide. On 0.5.0, visible wave/media lighting and generic Turbo/live rate/stop were confirmed. Those are historical checks, not automatic passes for Recorder.
- Current on-device Recorder validation is recorded in [the manifest](../dist/manifest.json). The owner confirmed capture/replay/loop/speed/indicators, removed shortcuts, typing, numpad and Aurora on the installed 0.7.0 local build. Current 0.8.0 hardware checks are recorded separately in the manifest.

## Practical limits

An independent review found two ownership/mode defects before publication.
Regressions now verify that VIA modified/advanced physical actions stop Recorder
before QMK processes them, including a waiting loop; layer holds remain control
gestures. Core intercepts the standard VIA effect channel, forces Direct after
remapped mode keys and disables configurable native effects, so its RGB watchdog
cannot be bypassed by selecting a local animation. Manifest refreshes preserve
both editions' checksum entries in either update order. Each correction was
reproduced by a failing test before the passing implementation.

Approximately 48 simple presses are shared across all three Recorder slots; holds/pauses/modifiers consume variable space. One recording lasts at most 30 seconds. Basic physical keyboard/modifier edges are supported, not text capture, mouse input or arbitrary QMK macros. Host language/lock state and application focus still apply. Ordinary physical input cancels playback; it does not pause/resume it.

USB state does not reveal Windows shutdown when the host leaves the bus genuinely configured and active. Sleep/wake, actual PC shutdown and power-cycle behavior still need target-host checks. Game Mode is not a filter for every arbitrary VIA/advanced action. All RGB status respects Off/brightness zero/USB suspension, while keyboard stops continue to work.

## Previous 0.8.0 device result

Full 0.8.0 was flashed on the target Red Blade with programming/checksum
**0x34ed** verified. VIA 13, VialRGB 1, 109 unique LED positions and the 33 Fn
bindings/three explicit removals were verified; base/third layers and VIA macros
were preserved. The owner confirmed physical/replayed red trails on white,
latched Hyper speed/reverse adjustment, left/right mouse targets, their
amber/purple indicators and stops. OpenRGB/Aurora were restored and the owner
confirmed host priority. Core remains build/host-tested only. Physical latency,
power-cycle/shutdown, runtime stack high-water and sustained high-rate RGB load
are not claimed as tested. See both manifests and the scoped HID comparison.

## 0.8.1 dedicated Windows indicator

The owner confirmed that holding Fn lit the physical upper-right Windows logo
on 0.8.0. RBFS names this active-low B12 output LED_FN_PIN; the manufacturer
confirms the shared Ironclad PCB, whose QMK definition calls B12 Win Lock.
Full now gives the output to Game Mode, with GPIO writes only on lock/lifecycle
changes. Fn layer transitions no longer overwrite it; RGB Fn hints are separate.
Core leaves it off. Tests include the actual production keymap hooks and real
controls, failing on the previous Fn-only behavior before passing the correction.
The current patch hardware result is recorded in the manifest.

Full 0.8.1 was flashed with programming/checksum 0x4f6a verified. The owner
confirmed Fn-only leaves the dedicated indicator off, Fn+Windows lights it,
Fn release keeps it on, unlock clears it, and it remains usable with RGB Off.
Typing/Aurora were also confirmed after service restoration. VIA base/third
layers and macros were preserved; VIA 13/VialRGB 1/109 positions/Fn map verified.
Core remains build/host-tested only. Exact scope is recorded in the manifests.
