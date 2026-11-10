"""Package a verified Studio executable and corresponding third-party source archives.

Provide source archives acquired from the corresponding upstream releases; inspect their
provenance and completeness before packaging. This tool does not download or publish.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile
from check_release import ROOT, validate as validate_full
from check_core_release import validate as validate_core


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def package(exe, sonix_source, libusb_source, helper_dir, output):
    full, core = validate_full(), validate_core()
    if full['release'] != core['release']:
        raise ValueError('Firmware releases differ')
    if output.is_relative_to(ROOT):
        raise ValueError('Output must be outside the project checkout')
    if subprocess.check_output(['git', '-C', str(ROOT), 'status', '--porcelain'], text=True).strip():
        raise ValueError('Commit all project changes before packaging')
    for path in (exe, sonix_source, libusb_source):
        if not path.is_file():
            raise ValueError(f'Missing input: {path}')
    expected = {
        sonix_source: '455a649f67102314b26c2bbdf83e1d4afe182eeca8095d23805c624333448b75',
        libusb_source: 'fea36f34f9156400209595e300840767ab1a385ede1dc7ee893015aea9c6dbaf',
        helper_dir / 'sonixflasher.exe': '741a87eb5ada23e7b6380d9b07b74fdce0ecc8a9e807a29b8f72415d73deeed6',
        helper_dir / 'libusb-1.0.dll': 'bc18f430b2639c2f63961a51724adb910ac8ed4a045ad2dcfd504e67ea52c460',
    }
    for path, digest in expected.items():
        if not path.is_file() or sha(path) != digest:
            raise ValueError(f'Missing or unexpected upstream input: {path}')
    if len({p.resolve() for p in (exe, sonix_source, libusb_source)}) != 3:
        raise ValueError('Inputs must be distinct')
    if subprocess.run([str(exe), '--self-test'], check=False).returncode:
        raise ValueError('Studio self-test failed')
    output.mkdir(parents=True, exist_ok=True)
    tag = full['release']
    commit = subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD'], text=True).strip()
    target = output / f'redblade-plus-studio-{tag}-windows-x64.zip'
    with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        z.write(exe, 'RedBladePlus-Studio.exe')
        for rel in ('LICENSE', 'app/THIRD-PARTY-NOTICES.md', 'app/README.md'):
            z.write(ROOT / rel, rel)
        for license_file in sorted((ROOT / 'app/Licenses').iterdir()):
            if license_file.is_file():z.write(license_file,'app/Licenses/'+license_file.name)
    source = output / f'redblade-plus-studio-{tag}-complete-source.zip'
    tracked = subprocess.check_output(['git', '-C', str(ROOT), 'ls-files', '-z']).decode().split('\0')
    with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for name in tracked:
            if name:
                path = ROOT / name
                if path.is_file() and not path.is_symlink():
                    z.write(path, 'redblade-plus/' + name)
        for path, label in ((sonix_source, 'sonixflasher-3.1.0'), (libusb_source, 'libusb-1.0.30')):
            z.write(path, f'third-party/{label}/{path.name}')
        for name in ('sonixflasher.exe', 'libusb-1.0.dll'):
            z.write(helper_dir / name, 'third-party/helpers/' + name)
        z.writestr('SOURCE-BUNDLE.txt', f'Project commit: {commit}\nSonixFlasher source SHA256: {sha(sonix_source)}\nlibusb source SHA256: {sha(libusb_source)}\n'
                   'SonixFlasherC 3.1.0 tag commit e6ba5db11f703f89fd29a5bc0e56ed1ae42604c3: https://github.com/SonixQMK/SonixFlasherC/releases/tag/3.1.0\n'
                   'libusb 1.0.30: https://github.com/libusb/libusb/releases/tag/v1.0.30\n'
                   'Rebuild Studio from redblade-plus/app with -p:HelperDirectory=<source bundle>/third-party/helpers. See app/THIRD-PARTY-NOTICES.md.\n')
    sums = output / 'studio-SHA256SUMS'
    sums.write_text(''.join(f'{sha(p)}  {p.name}\n' for p in (target, source)), encoding='utf-8')
    return {'assets': [str(target), str(source), str(sums)], 'commit': commit}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--exe', type=Path, required=True)
    p.add_argument('--sonix-source', type=Path, required=True)
    p.add_argument('--libusb-source', type=Path, required=True)
    p.add_argument('--helper-dir', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    print(json.dumps(package(*(x.resolve() for x in (a.exe, a.sonix_source, a.libusb_source, a.helper_dir, a.output)))))
