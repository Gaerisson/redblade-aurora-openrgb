# RedBlade+ Studio

Portable native Windows x64 editor for RedBlade+ Full 0.9.0+. English/French, four complete onboard profiles, 109 physical ISO keys, 24 effect colors and ten built-in module enables. Core remains configurable through its existing OpenRGB/VIA interface; it does not implement the Full Studio configuration API.

Opening Studio performs no HID discovery or writes and does not inspect or stop RGB applications. JSON editing/import/export works offline. Read, Save, Preview and guided Flash are explicit hardware jobs. Read the keyboard before Save so the generation lease protects concurrent changes. Save writes all four profiles and the active index, then requires complete readback. Profile names stay in PC files. Preview sends the edited profile's per-key colors for five seconds and restores both private runtime lighting state and original VialRGB mode/speed/HSV before RGB clients return.

Keyboard legends default to AZERTY/ISO for this board and have an independent AZERTY/QWERTY display selector. Switching EN/FR never changes LED indices, matrix coordinates, keycodes or the selected legend layout. The physical forward-hold key is labelled Z under AZERTY and W under QWERTY. App-generated Profile/Profil names localize; names entered by the owner do not. Preview screenshots show the real factory white per-key palette, without rainbow demonstration data.

The module checkboxes gate compiled-in firmware functions; they do not unload code. Saving or reconnecting never starts generated keyboard/mouse actions. Profile JSON cannot load scripts, assemblies, executable paths or serializer types. Files are limited to 128 KiB with required schema/board/profile fields and unknown fields rejected.

RGB pickers quantize colors to the keyboard's HSV8 representation. Dynamic effect intensity and master brightness still apply. This wire representation is kept out of the ordinary editing flow.

## Build

Install the .NET 10 Windows SDK. There are no application NuGet dependencies. Self-contained publishing restores official Microsoft runtime/build packs when they are not cached.

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
./publish/RedBladePlus-Studio.exe --preview C:/artifacts/studio-keyboard.png
./publish/RedBladePlus-Studio.exe --preview C:/artifacts/studio-functions-fr.png Functions fr
```

Preview pages: `Keyboard`, `Functions`, `Colors`, `Profiles`, `Firmware`; optional language `en` or `fr`. These modes never construct a real HID transport or pause/start processes/services. Self-tests write `self-test-results.txt` beside the executable and return a failing exit code on error. With PowerShell and the GUI executable, use `Start-Process -Wait -PassThru -WindowStyle Hidden` to wait reliably and inspect `ExitCode`.

Checks cover exact offsets/reserved bytes, default palettes, CRC32, all four profiles, JSON rejection, rate validation, sequential writes/generation wrap, lost ACK without replay, wrong replies, preview restoration after failure, cleanup, image/helper hashes, physical media/Fn positions and flash exit/reconnect gates. They simulate protocol behavior; they do not establish hardware persistence or physical flash success.

## Hardware jobs and recovery

The editor stays nonadmin. An explicit job may request UAC for the same executable's fixed `--worker <GUID> <job-directory>` operation. The worker reads bounded typed request data from the initiating user's absolute LocalAppData job directory, including when elevation uses another account; the directory name must match the GUID and reparse points are refused. Profiles provide no command/path. A named mutex held by a dedicated thread serializes jobs across asynchronous continuations. The elevated worker creates each output directory atomically under ProgramData, with protected Administrators/SYSTEM access and read access for the requesting user; a reused job ID is refused. Inherited OWNER RIGHTS prevents file owners from changing child ACLs implicitly. Normal Raw HID uses Windows SetupAPI/HidD, exact manufacturer/product/VID/PID/usage checks and exactly one 33-byte interface, with a serialized one-second request timeout and no uncertain write replay.

Before pausing anything, capture current-session RGB processes, arguments and duplicated original user tokens in memory. Remember the OpenRGB service's original running state and whether its SDK port was reachable. Original user tokens prevent restarting normal RGB applications elevated. Restore in `finally`, OpenRGB first, wait for its SDK only if previously reachable, then restore Aurora. Startup arguments/tokens are not written to job JSON or logs. The job's process state cannot survive a machine crash; restoration failures are explicitly reported.

Flashing backs up VIA and supported custom profiles to `%PROGRAMDATA%/RedBladePlusStudio-job-<GUID>/private-backup/`. These files contain private keymaps/macros; keep them private. The app requires no pre-existing Sonix bootloader, then waits for physical Fn+Esc entry and normal keyboard disappearance. It requires one bootloader at the original USB physical location and SN32F24XB/64 KB/CS0; missing or ambiguous location data blocks flashing. Only fixed embedded images with matching board/size/SHA are accepted. The helper and writable firmware copy are extracted into a separate protected ProgramData staging directory because SonixFlasherC can align its input in place. Programming is not killed on a timeout or normal window close. A checksum/exit zero is insufficient: normal-mode edition/version reconnection and VIA/settings checks must succeed.

Known RedBlade+ VIA13 layouts with matching dimensions can be restored and read back after flash. If the previous keycode generation is unknown, changed VIA data is retained privately for manual migration rather than blindly written. Core cannot restore Full configuration payloads; the backup is retained and the result explains that limitation. The original manufacturer's recovery procedure remains the fallback for firmware failures. No physical device, UAC prompt, service stop or real flash is invoked by automated verification.

Jobs and private backups are intentionally retained until the owner removes them; there is no automatic deletion of recovery data. The app has no network connection, update downloader or extension loader.
