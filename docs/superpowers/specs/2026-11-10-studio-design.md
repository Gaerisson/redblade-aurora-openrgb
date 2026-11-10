# RedBlade+ Studio design

The owner requested a Windows mini-app for checkable/configurable built-in functions, per-key default colors, effect colors, RGB profiles and guided keyboard flashing. They explicitly chose **built-in modules**, and **settings retained in the keyboard with the PC app closed and without Aurora/OpenRGB**. Preserve the pending Fn shortcut-focus change and the existing low-latency design.

## Product

Portable Windows x64 app, **RedBlade+ Studio**, English/French UI. Native C# WinForms using the installed.NET10 Windows SDK; self-contained release. Dark charcoal, red accents, existing RedBlade+ branding. Sidebar: Keyboard, Functions, Colors, Profiles, Firmware. Editing and file profiles work offline. A virtual109-key ISO keyboard includes the actual four upper-right media keys and physical Fn position. Color selection supports individual/multiple/all keys and ordinary RGB pickers.

Four complete onboard profiles hold full per-key HSV8 colors,24 effect colors, module enables, master/default mode/timeout, Turbo rate and Recorder speed. Profile names/libraries/import/export live in bounded, versioned PC JSON files. The four selected payloads and active index persist in the keyboard. App color preview is temporary and cannot silently overwrite saved settings. Explicit Save validates, commits and reads back the complete payload. Return RGB control to Aurora/OpenRGB after each hardware job.

Modules: startup wave, media fallback, idle fallback, reactive typing, Fn focus, keyboard Turbo, mouse Turbo, Recorder, forward hold and Windows lock. Disabled helper shortcuts are hidden by Fn focus. No third-party code/plugin loading. No automatic key/mouse action starts when applying a profile or reconnecting.

## Storage and compatibility

The existing1056-byte EEPROM is fully allocated, including all259 VIA macro bytes. Preserve it. Use independent two-bank flash storage in the unused code gap, with linker boundaries, CRC/commit-last publication and bounded cooperative erase/write/verify work. Direct flash reads avoid a second color buffer. New persistent SRAM must stay within8 bytes; retain existing stack reservations, USB polling, scan/PWM clock and eight-LED rendering. Detailed layout/wire contract: [CONFIG_PROTOCOL.md](../../CONFIG_PROTOCOL.md).

Full0.9.0 adds the configuration API. Core stays a separate RGB/VIA-only firmware and exposes no Full configuration API; Studio can identify/flash it but disables unsupported controls. The app can identify an older firmware and offer the bundled upgrade instead of pretending settings were saved. Before flashing, back up VIA and any custom settings privately; restore/verify after normal-mode reconnect. No flash occurs without a user-started guided flow and the physical Fn+Esc bootloader transition.

## Hardware jobs

Normal app editing has no HID or service side effects. Explicit Read/Save/Preview/Flash jobs may request UAC to temporarily pause the current-session RGB clients/OpenRGB service. Capture original executable paths/arguments in memory, preserve service state, restart OpenRGB before Aurora and wait for its actual SDK endpoint. Restore in finally even on failed backup/configuration/flash. Keep private VIA backups separate from shareable color profiles.

Use a single serialized native HID transport with strict VID/PID, usage page/usage, manufacturer/product and protocol identity checks. Normalize33-byte Windows reports to32-byte payloads and reject stale/short/mismatched replies. Reject ambiguous normal/bootloader device counts. Fixed embedded firmware/helper resources must match manifests and hashes. Stage writable firmware copies: SonixFlasherC3.1.0 may modify its input. Its automatic final-chunk/checksum verification is supplemented by reconnect+identity/version/settings checks. Do not kill an active programming operation on a timeout or normal window close.

## Verification

Host flash-emulation tests cover bounds, CRC, malformed payload, generation lease/wrap, interruption through every phase, old-bank survival, default fallback and asynchronous Fn+F9. Integration tests cover module gating, palette/effect rendering, host priority, disabled/off/suspend and retained input behavior. App tests cover codec vectors, profiles, wrong responses/disconnect/lost ACK, failure cleanup and flasher exit/reconnect combinations. Render and inspect the GUI. Build/check ARM resources and run existing C/Python/PowerShell suites and final CI before publishing. On-device persistence/power-cycle and flash behavior require explicit hardware validation; do not label simulated results as hardware results.
