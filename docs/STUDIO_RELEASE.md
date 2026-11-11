# Studio release packaging

Studio and firmware versions are independent: this preview uses Studio 0.10.0 and Full/Core 0.10.0 manifests and BINs. Verify both firmware artifacts before building Studio. Run the Studio self-test from the freshly published executable, then package on Windows from a clean committed checkout:

```powershell
python tools/package_studio_release.py --exe C:/release-build/RedBladePlus-Studio.exe --sonix-source C:/release-inputs/SonixFlasherC-3.1.0-source.zip --libusb-source C:/release-inputs/libusb-1.0.30-source.tar.bz2 --helper-dir C:/release-inputs/sonixflasher --output C:/release-output
```

Use the official SonixFlasherC 3.1.0 tag archive (commit e6ba5db11f703f89fd29a5bc0e56ed1ae42604c3) and official libusb 1.0.30 release source tarball. The tool checks exact source and helper hashes; the matching binaries are included for rebuilding Studio. It includes Studio source, notices and firmware resources; distribute the separate Full and Core complete QMK source packages and BINs alongside it. Include the WebView2 SDK notices and require the WebView2 Runtime on Windows. Keep the .NET runtime licenses/notices from the self-contained publish and consult the applicable distribution terms. The Studio version and archive tag come from the project Version; both firmware manifest releases must match each other. The output includes the standalone `RedBladePlus-Studio.exe`, both BINs, both manifests, binary/source ZIPs and `SHA256SUMS` covering each of those assets. Upload these exact names to the same GitHub release for the updater. This command neither downloads dependencies nor publishes a release. Do not distribute an incomplete source package.

Official inputs verified for this release:

- [SonixFlasherC 3.1.0 source tag](https://github.com/SonixQMK/SonixFlasherC/archive/refs/tags/3.1.0.zip), commit `e6ba5db11f703f89fd29a5bc0e56ed1ae42604c3`. The downloaded GitHub API tag ZIP used here has SHA-256 `455a649f67102314b26c2bbdf83e1d4afe182eeca8095d23805c624333448b75`. Archive bytes can vary by download endpoint: the packager accepts the verified API ZIP.
- [libusb 1.0.30 release source](https://github.com/libusb/libusb/releases/download/v1.0.30/libusb-1.0.30.tar.bz2), SHA-256 `fea36f34f9156400209595e300840767ab1a385ede1dc7ee893015aea9c6dbaf`.
- [SonixFlasherC Windows release](https://github.com/SonixQMK/SonixFlasherC/releases/download/3.1.0/sonixflasher-windows-x86_64.zip), SHA-256 `2958ca8de3021bf96d5c900b6bb5021a2d4c1a528d8da2516598fd067bbf0619`. Its executable and DLL match the hashes checked by Studio.
