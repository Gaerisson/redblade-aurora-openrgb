# Modern Studio Implementation Plan

> **For agentic workers:** Use the existing user-authorized specialist delegation and verification workflow. Preserve prior completed fixes and keep new boundaries independently testable.

**Goal:** Deliver the Lumen-inspired RedBlade+ UI, software RGB effects and useful optional firmware features with working updates and guided flashing.

**Architecture:** Local WebView2 frontend; typed C# bridge to the existing configuration/hardware/update backend. Separate bounded OpenRGB effect worker; existing persistent configuration extended without moving EEPROM.

**Tech Stack:** .NET 10, Microsoft.Web.WebView2, local HTML/CSS/JavaScript, QMK C, OpenRGB SDK.

**Spec:** ../specs/2026-11-11-modern-studio-design.md

## Global Constraints

- Single visible profile, EN/FR, real 109-key ISO geometry and four media keys.
- Existing reviewed staging/updater safeguards remain authoritative.
- No loss of custom/VIA data; optional features start disabled.
- SRAM usable >=64 bytes; stacks, Recorder arena and scan/USB timing preserved.

## Review Focus

- Untrusted web messages, navigation and imported strings cannot invoke arbitrary native operations.
- RGB effect ownership and app shutdown always restore Aurora and service state.
- Background updates cannot close an app with unsaved edits or active hardware/effects.
- Older firmware produces an actionable upgrade state, not a blank/nonfunctional editor.
- New options release generated keys on stop, suspend, disable and timeout.

## Tasks

- [x] Frontend: add local UI/model tests, implement Lumen-inspired layout and keyboard/effects/guide controls, inspect desktop renders.
- [x] Native host: add bridge rejection tests, WebView2 resource isolation and all typed configuration/update/flash actions; exercise actual web UI.
- [x] Effects: test deterministic frames, SDK packet bounds and stop/cleanup; implement native 30fps worker with explicit RGB ownership.
- [x] Firmware: test Caps Word and safety timeout transitions, extend optional configuration, rebuild with memory guard.
- [ ] Integration: run focused and release checks, final review, publish source/BIN/software assets and clear guided documentation, then real keyboard validation when owner is available.

Verification: native/ARM and application suites pass; real Full 0.10 identify, VIA recovery and save/readback passed. Initial guided reconnect and early OpenRGB discovery defects now have targeted regression tests. Publication and final software-effect hardware check remain in progress.
