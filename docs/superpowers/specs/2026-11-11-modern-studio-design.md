# RedBlade+ Studio 0.10 — modern interface and RGB effects

The owner requests Lumen's UI quality, the existing RedBlade logo/crimson colors, one visible profile, reliable controls and guided firmware updates, automatic application updates and useful optional keyboard features. This extends the existing 0.10 work; retain its tested native menu crash correction, update integrity/install logic and protected flashing backend.

## Approach

Keep the C#/.NET hardware/update backend and render a local HTML/CSS interface inside Microsoft's WebView2. Lumen's cards, hierarchy, spacing and progressive disclosure are the visual reference; use RedBlade crimson/charcoal/white, not Lumen branding. Continuing the native control layout does not meet the owner's visual request. Rewriting the backend in Electron would discard reviewed hardware work and add avoidable dependencies. The host implementation is our engineering choice under the owner's request to proceed autonomously.

Pages: Keyboard, RGB Effects, Functions, Guide & Updates. The keyboard occupies the primary canvas; a compact inspector edits selected keys. A single active profile is visible; other existing wire profiles are preserved. Show local edits, connection/firmware state and one Apply action clearly. Guide includes the existing GitHub shortcut PNG with zoom, searchable explanations and a staged install flow. Use a real application icon and a dark native caption. No decorative fake device readings or inert actions.

## Boundaries

Only embedded local frontend resources are allowed in WebView2. Block remote navigation, downloads, new windows and host-object injection. A bounded, typed JSON message bridge accepts an explicit action allowlist from the exact local origin. No executable paths/scripts come from profiles. Keep all privileged operations in the existing short-lived worker with protected staging, backup, unique same-port bootloader checks, checksum/reconnect and RGB restoration. Updates keep GitHub digest plus SHA256SUMS validation and rollback copies; no update restart while unsaved edits, effects, flashing or other hardware work are active.

## RGB and optional firmware

Software effects use a bounded 30fps scheduler and OpenRGB SDK, keeping keyboard USB/scan timing unchanged. Offer solid, breathing, wave, two-color gradient, sweep and rain with colors/speed/brightness. Preview locally without touching hardware. Actual streaming takes explicit RGB ownership, pauses/restores Aurora while keeping OpenRGB available, and stops cleanly on cancel/disconnect/app exit. Do not queue delayed frames. Firmware fallback continues when software streaming stops.

Add optional Caps Word and a Turbo safety timeout, default off. Preserve the Fn+Esc bootloader shortcut, all existing helpers, VIA EEPROM and Recorder capacity. Extend the feature-mask capability compatibly, rejecting unsupported enabled bits on older firmware. Keep the 64-byte usable SRAM guard, reserved stacks, 720896Hz timer, 1ms USB request, 8 LEDs/task and 33ms RGB frame settings. If a feature cannot satisfy the memory guard, simplify its implementation; do not reduce existing buffers/stacks.

## Validation

First pin new behavior with failing focused tests: bridge input rejection, effect packet/rate/stop behavior and new firmware option lifecycle. Then implement. Exercise the actual WebView UI (navigation, color selection, module controls, effect preview, guide and wizard) and inspect screenshots. Test update/download/install failure cases and protect edits across handoff. Run relevant native suites and both ARM builds if firmware changes. A targeted final review covers new bridge/effects boundaries and integration. Hardware tests require the owner's Windows UAC acceptance and physical Fn+Esc action; no claims before observed results.
