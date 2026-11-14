# Modifier background dimming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for root work; isolated Studio work may be delegated under the user's AGENTS instructions.

**Goal:** Configurable background dimming while physical Ctrl/Shift/Alt highlights are active.
**Architecture:** Preserve raw RGB; scale channels before publication to the existing active SN32 buffer on flush, with cached mode/modifier eligibility and preserved priority cues. Keep profile wire dimensions.
**Tech Stack:** SonixQMK C, .NET 10 WinForms/WebView2, JavaScript.
**Spec:** `docs/superpowers/specs/2026-11-13-modifier-dimming-design.md`.

## Global Constraints

- HighlightDimPercent is byte 14 of each 416-byte profile, 0–100; 0 disables.
- Full/Studio 0.14.0; Core 0.12.1 unchanged. Old JSON/wire zero preserves behavior.
- No extra RGB buffer, input hooks, USB/scan timer or interrupt changes; >=64 usable SRAM bytes.
- Mode eligibility follows HighlightMode; Fn/Off/suspend and priority cues win.

## Review Focus

- Repeated static/native-effect flushes must not compound dimming.
- Release, GUI-only and weak modifiers must restore/retain ordinary lighting.
- Multiple held modifiers protect the union of useful keys for the selected layout.
- Turbo Fn/target phases and Recorder/game status must remain readable.
- Hidden profiles and downgrade/save to older firmware must not silently lose settings.

## Task 1: Firmware and output path

- [x] Write failing production C tests: byte 14 accepts 0/70/100, rejects 101; a background RGB(100,200,50) dims to (30,60,15) at 70%, remains stable across repeated raw-buffer copies, and restores on release.
- [x] Implement bounded validation, modifier classification/cache and active-buffer dim helper; protect priority cues and all mode gates.
- [x] Add the guarded output-buffer callback/dirty request to the canonical driver patch and accept the prior canonical patch when preparing QMK.
- [x] Run native suites and ARM resource guard; verify Core BIN unchanged.

## Task 2: Studio controls and compatibility

- [x] Add failing codec/JSON/range/all-profile downgrade and UI-control tests.
- [x] Implement HighlightDimPercent in config, bridge, capability and apply/save guards; bilingual toggle+slider/help in the existing modifier panel.
- [x] Run JS/C# and actual WebView2 interaction checks.

## Task 3: Delivery

- [x] Update version/docs/manifests; perform one consolidated review and correct significant findings.
- [x] Commit/push, run final CI, build/package exact sources and Studio; verify public checksums/downloads and publish stable Latest 0.14.0.
- [x] Report physical acceptance pending and that the keyboard was not reflashed automatically.

## 0.14.1 correction

- [x] Reproduce the bright intermediate frame with a simulated PWM interrupt in the real patched flush, in both local and host modes.
- [x] Scale from the raw source before writing each live output channel; preserve priorities and accept only known canonical driver versions when upgrading.
- [x] Run regression suites, build Full/Core and verify resources; retain the published Core BIN byte-for-byte after checking the rebuild's only differences are VIA build-day constants. Consolidated review passed.
- [ ] Publish stable Full/Studio 0.14.1 with BINs and matching sources; report physical confirmation separately.
