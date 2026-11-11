# Source and bundled component notices

Studio source is licensed under GPL-2.0-or-later, as marked in its source files and the repository license. Native Windows APIs are supplied by the operating system. .NET Windows Desktop runtime redistribution follows the license/third-party notices supplied with the official .NET runtime packs; preserve those notices in the release/source package.

The optional fixed bundled flashing helper is SonixFlasherC 3.1.0, GPL-3.0:

- Source tag: https://github.com/SonixQMK/SonixFlasherC/tree/3.1.0
- Release commit: `e6ba5db11f703f89fd29a5bc0e56ed1ae42604c3`
- Official Windows archive SHA-256: `2958ca8de3021bf96d5c900b6bb5021a2d4c1a528d8da2516598fd067bbf0619`; both embedded helper files match this archive.
- Source ZIP SHA-256: `455a649f67102314b26c2bbdf83e1d4afe182eeca8095d23805c624333448b75`
- License: https://github.com/SonixQMK/SonixFlasherC/blob/3.1.0/LICENSE
- EXE SHA-256: `741a87eb5ada23e7b6380d9b07b74fdce0ecc8a9e807a29b8f72415d73deeed6`

The accompanying libusb DLL's Windows version resource reports `1.0.30.12037` (libusb-1.0). Its SHA-256 is `bc18f430b2639c2f63961a51724adb910ac8ed4a045ad2dcfd504e67ea52c460`. Preserve libusb's LGPL-2.1-or-later notices and provide corresponding libusb 1.0.30 source/build materials with the released helper package:

- Source release: https://github.com/libusb/libusb/releases/tag/v1.0.30
- Source tarball SHA-256: `fea36f34f9156400209595e300840767ab1a385ede1dc7ee893015aea9c6dbaf`
- License: https://github.com/libusb/libusb/blob/v1.0.30/COPYING

The root release process must distribute matching corresponding source and complete license texts for bundled helpers/runtime dependencies. This file identifies the components; a bare upstream link does not replace the release distributor's source obligations. Helpers are fixed dependencies, not third-party extension loading.

`app/Licenses/` preserves the official .NET runtime 10.0.11 license and third-party notices plus the Windows Desktop 10.0.11 runtime license, copied from the Microsoft runtime packs used for this build. These notices accompany both the portable package and the source bundle. Helper source archives, their license texts and the exact helper binaries accompany the Studio complete-source bundle.

Keyboard geometry derives from SonixQMK QMK's `designedbygg/redblade/rbfs/keyboard.json` at the repository's pinned firmware commit; labels/order derive from the repository's `aurora-key-map.json` and `led-map.json`. The map preserves the four top-right keys (Calculator, Mute, Volume Down, Volume Up) and physical Fn position. Firmware/QMK and submodule licenses remain their own.

The application uses Microsoft.Web.WebView2 SDK 1.0.4314.35 under its supplied Microsoft license. The SDK license and third-party notices are preserved in `app/Licenses/webview2-LICENSE.txt` and `webview2-NOTICE.txt`. Microsoft Edge WebView2 Runtime is an independently installed prerequisite; it is not bundled with Studio.
