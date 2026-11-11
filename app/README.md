# RedBlade+ Studio

## Guided setup and OpenRGB

Open **Setup** to connect the keyboard, install Full or Core with a VIA backup, prepare OpenRGB, check the 109-LED controller and run a six-second lighting test. A working OpenRGB SDK is reused. Otherwise Studio installs the pinned [official OpenRGB 1.1 Windows portable archive](https://openrgb.org/releases.html), checks its SHA-256 and archive bounds, and maintains an isolated configuration with the RedBlade VialRGB USB entry. OpenRGB is downloaded when needed and is not bundled; this setup installs no low-level driver or OpenRGB startup entry. OpenRGB uses its normal hardware detectors; Studio streams colors only to the verified RedBlade controller.

The local SDK uses 127.0.0.1:6742, or 6743 when an existing server occupies 6742 without exposing the RedBlade. Use the displayed port in Aurora's OpenRGB (RGB.NET) provider. Existing RGB applications and their configuration are preserved. If both supported ports are occupied, setup refuses to take either port. Effects pause the RGB clients while active and restore them afterwards; the managed SDK stays available until stopped or Windows exits.

The update notice opens in the foreground within Studio only when the window is visible and editing, effects, dialogs and hardware work are idle. **Later** or Escape defers automatic installation until the next launch. A firmware update always requires the guided installer and a physical Fn+Esc hold; opening Studio never flashes the keyboard.

First installation from stock firmware requires acknowledging that unknown VIA keycodes cannot be migrated automatically. A private backup is retained and the new factory keymap is used. Existing RedBlade+ installations retain the usual verified restoration; dimension mismatches refuse writes and report protocol, layer count and macro-buffer size.

Studio 0.13 bundles Full 0.13 and the unchanged Core 0.12.0 firmware. Studio is a portable Windows x64 editor for RedBlade+ Full firmware 0.9.0+ (optional settings require Full 0.10.0, 0.11.0 or 0.12.0 as indicated). It presents the active profile, with 109 physical ISO keys, 24 effect colors and built-in modules. The four-profile wire format is preserved for compatibility. Core uses its existing OpenRGB/VIA interface and does not implement the Full Studio configuration API.

Full 0.13 adds middle-click Turbo via Fn + T then Mute and Anti-AFK via Fn + I. After about 30 seconds idle, Anti-AFK alternates ±8-pixel horizontal mouse movement and briefly presses W then S (80 ms each, 40 ms apart), then waits again. W is physical Z on AZERTY; without game-focus detection these keys can type outside a game. Arming is refused while keys or modifiers are held. It stops on keyboard activity including advanced VIA actions, Esc, Fn + I, suspend, reset, timeout or another helper; it does not monitor other mice or log input. The existing Auto-W / Anti-AFK module checkbox controls both helpers. Fn and the Turbo target follow each generated press and release; at high rates they may appear steady. Middle click is cyan, left amber and right violet; the master brightness applies. The factory Turbo rate is 60/s, saved profiles retain their settings and Fn + R presets remain 25/50/100/s. USB polling remains 1 ms.

Full 0.12 enables a reaction duration slider up to approximately 4 seconds, cross waves and a global double pulse. The legacy defaults remain unchanged until edited. Modifier highlights can be disabled, limited to autonomous lighting, or explicitly overlaid on host RGB. Ctrl is blue, Shift violet and Alt amber. Factory-position shortcut hints follow the selected AZERTY/QWERTY layout; VIA remapping can differ.

The keyboard editor's **Fn shortcut explorer** explains a key on hover/click, with a Shift alternative toggle or the Shift key. This is an interactive reference for factory bindings, not a live physical-Fn monitor, and it sends no keyboard command.


Click the shortcut map in **Guide & Updates** to open the large reader. Use the mouse wheel to zoom (10–600%), drag the image to move, or use Fit, Fit width and 100%. Escape closes it. Keyboard arrows move the image; +/− zoom, 0 fits and 1 selects actual size.

To brighten onboard colors, select keys (or Select all), click **Maximize selected colors**, then Apply. The edit preserves exact hue/saturation and leaves black/off colors untouched; Undo restores the previous colors. A 100% master preserves each color's stored intensity, so dark colors stay dark. Default local colors use value 64, while custom stored colors already support 255. Aurora/OpenRGB and Studio software effects have their own pixel colors/brightness controls. This does not increase PWM current or exceed the existing 8-bit output range. Applying settings briefly pauses the RGB clients and writes onboard settings; transient lighting changes during this operation are not a supported boost mode. New autonomous reactions and modifier highlights require Full 0.12 and the guided firmware installation. PC effects and the Fn explorer work without that firmware update. See [brightness audit](../docs/RGB_BRIGHTNESS.md) for verified software limits and remaining hardware uncertainty.


The native prerequisites window works without WebView2 and checks the runtime, Visual C++ x64, bundled firmware, flasher and layout. The embedded WebView2 bootstrapper downloads the full runtime from Microsoft; Visual C++ x64 is also downloaded from Microsoft. First installation requires internet. OpenRGB 1.1 is downloaded and verified from its official source when needed, not bundled.

The interface uses Microsoft Edge WebView2 Runtime (required on Windows), with English/French, AZERTY/QWERTY legends, a zoomable shortcut image and an application icon. Hover over modules or supported keys for explanations. Color Lab supports click, drag and marquee selection, add/subtract modes, groups, solid and linear/radial gradient fills, a palette and up to 24 undo steps. On Connect, choose to keep local active-profile edits, load the keyboard profile, or cancel; the other profiles come from the keyboard. Apply saves the active edits after reading the keyboard. The host, saved colors, lights off and stop Turbo/Recorder controls are explicit hardware operations. Import replaces only the active profile; export preserves the compatible format.

Setup offers an optional, off-by-default **Start Studio with Windows** setting for the current user (HKCU Run). The normal, non-elevated GUI starts at sign-in; startup does not request UAC, discover or write to HID devices, take RGB control or stop RGB applications. By default Studio checks official GitHub releases and can download updates. With automatic restart enabled, a verified software update applies after 30 seconds of inactivity only when no unsaved changes, open update wizard or hardware operation blocks it. The normal-user updater retains the previous executable as a `.bak` file. Update preferences are configurable. Firmware can be downloaded and verified, but installation requires the guided flow and physical Fn + Esc; there is no automatic flash at startup.

Read, Apply, Preview, keyboard controls and guided Flash are explicit hardware jobs. Read before applying so the generation lease protects concurrent changes. Saving preserves all wire-format profiles and requires complete readback. Preview temporarily sends the edited profile's per-key colors and restores lighting state. The bundled images are Full 0.13 and Core 0.12.0. Full 0.12.0 programming, checksum, identity and complete VIA, macro and profile restoration passed on the preparation keyboard. The owner confirmed typing, numpad, Fn guide after Fn+F4, darkness on release and Aurora after Fn+F1. SDK detection and the six-second lighting test passed with the existing OpenRGB server. First-time managed OpenRGB installation on a clean PC and physical Core acceptance remain unverified. Full 0.13 programming/restoration and the owner-confirmed middle-click, 5-click/s Fn/target synchronization and revised Anti-AFK checks passed; power-cycle persistence, sustained load and physical latency remain untested.

Twelve software RGB effects stream through the local OpenRGB SDK at up to 30 fps: solid, breathing, wave, gradient, sweep, rain, spectrum, twinkle, reactive, Aurora, Comet and Embers. Direction, scale, reverse, palette, background and selected-key zone are configurable, as is a key fade, ripple, row-wave, cross or pulse reactive overlay. The preview simulates presses locally and cannot trigger hardware. During an effect session only, Windows Raw Input filters to the Red Blade USB VID/PID; recent events are bounded to 32 events and 8 seconds, transient, without scan-code or keystroke logging or storage. When an SDK device-list notification arrives during playback, Studio rediscovers the controller and resumes only after the strict device identity and 109-LED checks pass. Studio pauses Aurora for the effect session and restores it when the session ends; start/stop restoration and physical reactive typing were confirmed with Full 0.11. Full 0.10 adds optional Caps Word and an automatic safety timeout, disabled by default. Full 0.11 adds autonomous key fade, ripple and horizontal wave in fallback/local white; each new press restarts the single wave. Styles require Full 0.11; host lighting, Off and USB suspension retain priority.

The module checkboxes gate compiled-in firmware functions; they do not unload code. Saving or reconnecting never starts generated keyboard/mouse actions. Profile JSON cannot load scripts, assemblies, executable paths or serializer types. Files are limited to 128 KiB with required schema/board/profile fields and unknown fields rejected.

RGB pickers quantize colors to the keyboard's HSV8 representation. Dynamic effect intensity and master brightness still apply. This wire representation is kept out of the ordinary editing flow.

## Build

Install the .NET 10 Windows SDK. The project pins Microsoft.Web.WebView2 1.0.4314.35. Self-contained publishing restores official Microsoft runtime/build packs when they are not cached.

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.local/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet publish RedBladePlus.Studio.csproj -c Release -r win-x64 --self-contained true -o publish
```

Run from `app/`. The source project embeds the repository's Full/Core `dist` manifests/binaries and keyboard map. Build after the final firmware artifacts are regenerated. The default helper directory is a repository-relative development location; release/CI builds should supply:

```powershell
dotnet publish RedBladePlus.Studio.csproj -c Release -r win-x64 --self-contained true -o publish -p:HelperDirectory=C:/build/dependencies/sonixflasher-3.1.0
```

`./build.ps1 -HelperDirectory C:/build/dependencies/sonixflasher-3.1.0` performs the self-contained publish and waits for the hardware-free self-test with failure checks. The default development helper path also works with `./build.ps1` in the original workspace.

That directory contains `sonixflasher.exe` and `libusb-1.0.dll`, whose fixed SHA-256 values are in `FirmwareImage.ValidateHelpers`. Missing helpers fail the hardware-free resource self-test and prevent flashing; they are never downloaded at runtime. No private absolute workspace paths are embedded in the project.

## Hardware-free checks and screenshots

```powershell
./publish/RedBladePlus-Studio.exe --self-test
./publish/RedBladePlus-Studio.exe --ui-self-test
./publish/RedBladePlus-Studio.exe --preview C:/artifacts/studio-keyboard.png
./publish/RedBladePlus-Studio.exe --preview C:/artifacts/studio-functions-fr.png Functions fr
```

Preview pages: `Keyboard`, `Functions`, `Colors`, `Firmware`; optional language `en` or `fr`. These modes never construct a real HID transport or pause/start processes/services. Self-tests write `self-test-results.txt` beside the executable and return a failing exit code on error. With PowerShell and the GUI executable, use `Start-Process -Wait -PassThru -WindowStyle Hidden` to wait reliably and inspect `ExitCode`.

Checks cover exact offsets/reserved bytes, default palettes, CRC32, all four profiles, JSON rejection, rate validation, sequential writes/generation wrap, lost ACK without replay, wrong replies, preview restoration after failure, cleanup, image/helper hashes, physical media/Fn positions and flash exit/reconnect gates. They simulate protocol behavior; they do not establish hardware persistence or physical flash success.

## Hardware jobs and recovery

The editor stays nonadmin. The first explicit hardware job may request UAC to start an elevated broker owned by the Studio session; later jobs reuse it while its owner lives. The worker reads bounded typed request data from the initiating user's absolute LocalAppData job directory, including when elevation uses another account; the directory name must match the GUID and reparse points are refused. Profiles provide no command/path. A named mutex held by a dedicated thread serializes jobs across asynchronous continuations. The elevated worker creates each output directory atomically under ProgramData, with protected Administrators/SYSTEM access and read access for the requesting user; a reused job ID is refused. Inherited OWNER RIGHTS prevents file owners from changing child ACLs implicitly. Normal Raw HID uses Windows SetupAPI/HidD, exact manufacturer/product/VID/PID/usage checks and exactly one 33-byte interface, with a serialized one-second request timeout and no uncertain write replay.

Before pausing anything, capture current-session RGB processes, arguments and duplicated original user tokens in memory. Remember the OpenRGB service's original running state and whether its SDK port was reachable. Original user tokens prevent restarting normal RGB applications elevated. Restore in `finally`, OpenRGB first, wait for its SDK only if previously reachable, then restore Aurora. Startup arguments/tokens are not written to job JSON or logs. The job's process state cannot survive a machine crash; restoration failures are explicitly reported.

Flashing backs up VIA and supported custom profiles to `%PROGRAMDATA%/RedBladePlusStudio-job-<GUID>/private-backup/`. These files contain private keymaps/macros; keep them private. The app requires no pre-existing Sonix bootloader, then waits for physical Fn+Esc entry and normal keyboard disappearance. It requires one bootloader at the original USB physical location and SN32F24XB/64 KB/CS0; missing or ambiguous location data blocks flashing. Embedded images or verified official GitHub release images with matching board, edition, size and SHA are accepted. The helper and writable firmware copy are extracted into a separate protected ProgramData staging directory because SonixFlasherC can align its input in place. Programming is not killed on a timeout or normal window close. A checksum/exit zero is insufficient: normal-mode edition/version reconnection and VIA/settings checks must succeed.

Known RedBlade+ VIA13 layouts with matching dimensions can be restored and read back after flash. If the previous keycode generation is unknown, changed VIA data is retained privately for manual migration rather than blindly written. Core cannot restore Full configuration payloads; the backup is retained and the result explains that limitation. The original manufacturer's recovery procedure remains the fallback for firmware failures. No physical device, UAC prompt, service stop or real flash is invoked by automated verification.

Jobs and private backups are intentionally retained until the owner removes them; there is no automatic deletion of recovery data. Updates use official GitHub release metadata, asset digests and SHA256SUMS. The app has no extension loader.
