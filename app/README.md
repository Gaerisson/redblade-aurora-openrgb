# RedBlade+ Studio

Studio 0.11.1 is a portable Windows x64 editor for RedBlade+ Full firmware 0.9.0+ (optional settings require Full 0.10.0 or 0.11.0 as indicated). It presents the active profile, with 109 physical ISO keys, 24 effect colors and built-in modules. The four-profile wire format is preserved for compatibility. Core uses its existing OpenRGB/VIA interface and does not implement the Full Studio configuration API.

Click the shortcut map in **Guide & Updates** to open the large reader. Use the mouse wheel to zoom (10–600%), drag the image to move, or use Fit, Fit width and 100%. Escape closes it. Keyboard arrows move the image; +/− zoom, 0 fits and 1 selects actual size.

To brighten onboard colors, select keys (or Select all), click **Maximize selected colors**, then Apply. The edit preserves exact hue/saturation and leaves black/off colors untouched; Undo restores the previous colors. A 100% master preserves each color's stored intensity, so dark colors stay dark. Default local colors use value 64, while custom stored colors already support 255. Aurora/OpenRGB and Studio software effects have their own pixel colors/brightness controls. This does not increase PWM current or exceed the existing 8-bit output range. Applying settings briefly pauses the RGB clients and writes onboard settings; transient lighting changes during this operation are not a supported boost mode. Full/Core firmware is unchanged at 0.11.0; this software update needs no flash.


The interface uses Microsoft Edge WebView2 Runtime (required on Windows), with English/French, AZERTY/QWERTY legends, a zoomable shortcut image and an application icon. Hover over modules or supported keys for explanations. Color Lab supports click, drag and marquee selection, add/subtract modes, groups, solid and linear/radial gradient fills, a palette and up to 24 undo steps. On Connect, choose to keep local active-profile edits, load the keyboard profile, or cancel; the other profiles come from the keyboard. Apply saves the active edits after reading the keyboard. The host, saved colors, lights off and stop Turbo/Recorder controls are explicit hardware operations. Import replaces only the active profile; export preserves the compatible format.

Startup does not discover or write to HID devices or stop RGB applications. By default Studio checks official GitHub releases and can download updates. With automatic restart enabled, a verified software update applies after 30 seconds of inactivity only when no unsaved changes, open update wizard or hardware operation blocks it. The normal-user updater retains the previous executable as a `.bak` file. Update preferences are configurable. Firmware can be downloaded and verified, but installation requires the guided flow and physical Fn + Esc; there is no automatic flash at startup.

Read, Apply, Preview, keyboard controls and guided Flash are explicit hardware jobs. Read before applying so the generation lease protects concurrent changes. Saving preserves all wire-format profiles and requires complete readback. Preview temporarily sends the edited profile's per-key colors and restores lighting state. The bundled Full/Core images are 0.11. Full 0.11 programming/checksum passed on the preparation keyboard. A malformed immediate identity reply stopped the first guided job before restoration; fresh-handle recovery identified Full 0.11 and restored VIA/macros and all four original profiles with full readback. The owner confirmed saved ripple/color, typing, numpad and PC reactive typing. Bounded read-only identity retries are regression-tested and were used during recovery; no second physical flash was performed. Power-cycle persistence, sustained load and physical latency remain untested.

Nine software RGB effects stream through the local OpenRGB SDK at up to 30 fps: solid, breathing, wave, gradient, sweep, rain, spectrum, twinkle and reactive. Direction, scale, reverse, palette, background and selected-key zone are configurable, as is a key fade, ripple or row-wave reactive overlay. The preview simulates presses locally and cannot trigger hardware. During an effect session only, Windows Raw Input filters to the Red Blade USB VID/PID; recent events are bounded and transient, without scan-code or keystroke logging or storage. Studio pauses Aurora for the effect session and restores it when the session ends; start/stop restoration and physical reactive typing were confirmed with Full 0.11. Full 0.10 adds optional Caps Word and an automatic safety timeout, disabled by default. Full 0.11 adds autonomous key fade, ripple and horizontal wave in fallback/local white; each new press restarts the single wave. Styles require Full 0.11; host lighting, Off and USB suspension retain priority.

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

The editor stays nonadmin. An explicit job may request UAC for the same executable's fixed `--worker <GUID> <job-directory>` operation. The worker reads bounded typed request data from the initiating user's absolute LocalAppData job directory, including when elevation uses another account; the directory name must match the GUID and reparse points are refused. Profiles provide no command/path. A named mutex held by a dedicated thread serializes jobs across asynchronous continuations. The elevated worker creates each output directory atomically under ProgramData, with protected Administrators/SYSTEM access and read access for the requesting user; a reused job ID is refused. Inherited OWNER RIGHTS prevents file owners from changing child ACLs implicitly. Normal Raw HID uses Windows SetupAPI/HidD, exact manufacturer/product/VID/PID/usage checks and exactly one 33-byte interface, with a serialized one-second request timeout and no uncertain write replay.

Before pausing anything, capture current-session RGB processes, arguments and duplicated original user tokens in memory. Remember the OpenRGB service's original running state and whether its SDK port was reachable. Original user tokens prevent restarting normal RGB applications elevated. Restore in `finally`, OpenRGB first, wait for its SDK only if previously reachable, then restore Aurora. Startup arguments/tokens are not written to job JSON or logs. The job's process state cannot survive a machine crash; restoration failures are explicitly reported.

Flashing backs up VIA and supported custom profiles to `%PROGRAMDATA%/RedBladePlusStudio-job-<GUID>/private-backup/`. These files contain private keymaps/macros; keep them private. The app requires no pre-existing Sonix bootloader, then waits for physical Fn+Esc entry and normal keyboard disappearance. It requires one bootloader at the original USB physical location and SN32F24XB/64 KB/CS0; missing or ambiguous location data blocks flashing. Embedded images or verified official GitHub release images with matching board, edition, size and SHA are accepted. The helper and writable firmware copy are extracted into a separate protected ProgramData staging directory because SonixFlasherC can align its input in place. Programming is not killed on a timeout or normal window close. A checksum/exit zero is insufficient: normal-mode edition/version reconnection and VIA/settings checks must succeed.

Known RedBlade+ VIA13 layouts with matching dimensions can be restored and read back after flash. If the previous keycode generation is unknown, changed VIA data is retained privately for manual migration rather than blindly written. Core cannot restore Full configuration payloads; the backup is retained and the result explains that limitation. The original manufacturer's recovery procedure remains the fallback for firmware failures. No physical device, UAC prompt, service stop or real flash is invoked by automated verification.

Jobs and private backups are intentionally retained until the owner removes them; there is no automatic deletion of recovery data. Updates use official GitHub release metadata, asset digests and SHA256SUMS. The app has no extension loader.
