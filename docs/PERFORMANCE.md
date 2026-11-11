# Performance checks

**0.13.0 status:** The native reference measures 0.13 code on the preparation PC. USB polling stays at 1 ms and RGB frames at 33 ms. Physical keyboard latency and sustained hardware effect load have not been measured. HID comparisons below are historical.

[Project](../README.md) · [Shortcuts](SHORTCUTS.md) · [Audit](AUDIT.md)

RedBlade+ requests 1 ms USB polling and preserves the shared Sonix matrix/PWM timer. Those are configuration facts, **not a measured 1 ms key latency claim**.

## Run tonight on Windows

Extract the **Full complete-source ZIP**, then open its `redblade-plus-v0.13.0` folder. It includes the tools and `dist/redblade-plus-benchmark-windows.exe`; the small Studio portable ZIP contains the app instead. Python 3.10+ and PowerShell 7 are required.

### 1. Native code check — RGB software keeps running

```powershell
pwsh -File tools/measure-performance.ps1
```

This runs the actual protocol handler, controls and pinned QMK color conversion on the PC (the custom renderer uses no second CIE curve). It measures average host CPU cost for 109-LED frames, dimming, local white and wave handover, plus the ordinary key-processing hook with Game Mode off/on. QMK hardware and USB transport are mocked. Use it to compare code changes on the same machine/build, not to predict microcontroller timing.

The [reference report](performance-reference.json) is a real native run on the preparation PC. It contains no HID or physical key-latency result. A separate [read-only HID comparison](hid-reference.json) contains 2,000 reads each on installed 0.8.0 and 0.8.1, with RGB clients stopped and helpers off: medians 1.9957 / 1.9954 ms, p99 2.0272 / 2.0394 ms. These two sequential runs do not cover keyboard input, active RGB load or sustained Turbo. CPU-clock measurements are averaged over many iterations and include the fixture/call overhead.

### 2. Read-only USB request/reply timing

Press **Esc** to stop game helpers and Recorder and close VIA, including browser tabs using it. Then:

```powershell
pwsh -File tools/measure-performance.ps1 -Hid -Samples 2000
```

Windows may request administrator permission to stop the existing OpenRGB service. The script records startup commands in memory, temporarily stops the service and current-session RGB clients, measures HID timing, and restores the applications in `finally`. Let the window finish. Reports are saved under `%LOCALAPPDATA%\RedBladePlus\Measurements`; the console prints the exact filename.

The HID test only sends RGB information **read** requests. It does not flash, change lighting, remap keys or start a game helper. It reports median, p95, p99 and maximum Windows → HIDAPI → firmware → Windows round-trip time, plus host-process CPU time. Competing RGB clients or active Turbo/forward hold/Recorder cause an abort rather than silently contaminating the result.

For manual control of applications and output:

```powershell
python tools/benchmark.py --native-exe dist/redblade-plus-benchmark-windows.exe --output native-report.json
# With OpenRGB, Aurora and VIA already stopped:
python tools/benchmark.py --hid --samples 2000 --output hid-report.json
```

Output files use exclusive creation; existing measurements are not overwritten. Firmware 0.1.x can be measured for a baseline; richer diagnostics are available in later versions. Use the same USB port and system conditions for comparisons.

## What these checks cannot measure

Neither test measures mechanical switch actuation, matrix/debounce latency, the MCU's exact task execution time, game input processing or end-to-end display response. Native nanoseconds are PC timings. HID round trips are a different path from keyboard input.

For a physical comparison, use a controlled actuator/electrical trigger and USB analyzer or a high-speed acquisition setup, with the original and custom firmware under equivalent RGB load. No such measurement has been performed for this release.

The implementation bounds work: eight base LEDs per RGB task iteration, bounded status overlays, including the persistent Fn status, at most nine incoming colors per packet, and at most one Turbo edge per housekeeping pass. Standard QMK report I/O still depends on USB scheduling. Turbo's 5–250 presses/clicks per second targets may run slower under load; missed pulses are not queued.

## 0.10.0 host regression run

A native run on the preparation PC measured 2,880 ns per 109-LED Direct frame and 3,200 ns for the onboard palette; Fn focus measured 2,140 ns without and 2,720 ns with an onboard palette. The immediately preceding 0.8.1 host baseline measured 2,740 ns for Direct. These are averaged host CPU times, include fixture overhead and have 1 ms clock resolution; they are not microcontroller execution times or physical key latency. The scenarios and checksum are recorded in [performance-reference-0.10.json](performance-reference-0.10.json). No 0.10.0 hardware measurements were performed.

## Build the native benchmark yourself

With GCC/Clang and the repository root as working directory:

```sh
cc -std=c11 -O2 -DUSE_CIE1931_CURVE -I tests/stubs -I tests -I firmware/aurora_via \
  tests/benchmark.c tests/qmk_fixture.c tests/store_flash_fixture.c tests/upstream/color.c tests/upstream/led_tables.c \
  firmware/aurora_via/redblade_rgb.c firmware/aurora_via/redblade_controls.c firmware/aurora_via/redblade_recorder.c firmware/aurora_via/redblade_config.c firmware/aurora_via/redblade_store.c -o native-benchmark
./native-benchmark
```

The Windows release executable uses MinGW GCC with static runtime linkage. The sources, QMK color implementation and retained upstream licenses are included.

## Recorder cost and timing

The native report includes a reactive-white frame with five refreshed keys. Minimal shared mouse HID button reports reuse the Turbo scheduler; no cursor engine is enabled. Selected rates are latched until changed/reset, not a release-to-restore boost. These rates are targets, not physical latency promises.

The native report also benchmarks the idle Recorder task and a synthetic loop with a 20 ms hold and 5 ms iteration gap. It uses fixture reports, not real keyboard output. The fixed 192-byte arena is shared by three slots; capture stores 1 ms key-edge delays, and playback performs at most one edge per housekeeping pass. A cached Q8 delay multiplier implements the target 0.5×–8× speeds; Hyper instead requests one edge per millisecond; 1×, 0.5× and 2× are exact before USB scheduling, intermediate multipliers are rounded. Capacity and timeout completion reserve space for balanced releases. There are no EEPROM writes, heap allocations, busy waits or catch-up bursts in capture/playback. Standard QMK report I/O and USB scheduling still apply; no physical latency measurement is implied.

## 0.11 native reference

The bundled report now measures the 0.11 host code, including latest-key ripple and horizontal-wave frames. Direct: 2840.0 ns/frame; autonomous ripple: 4200.0 ns/frame; horizontal wave: 4280.0 ns/frame. These are averaged PC CPU times with fixture overhead, not microcontroller execution or physical keyboard latency.
