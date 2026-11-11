"""Package exact Git sources, firmware, native tool and corresponding QMK tree."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile
from check_release import ROOT, validate
from prepare_firmware import BASE_TIMER


def tracked_sources(tree, root):
    # Include tracked sources only; arbitrary local keymaps/notes never enter a bundle.
    entries=subprocess.check_output(['git','-C',str(tree),'ls-files','-z']).decode('utf-8').split('\0')
    for entry in entries:
        if not entry: continue
        path=tree/entry
        if path.is_dir() and (path/'.git').exists():
            if subprocess.check_output(['git','-C',str(path),'diff','--name-only','HEAD'],text=True).strip():
                raise RuntimeError('A QMK dependency has modified tracked sources')
            yield from tracked_sources(path,root)
        elif path.is_file() and not path.is_symlink(): yield path,path.relative_to(root)


def package(qmk, destination, native):
    if destination.is_relative_to(qmk) or destination.is_relative_to(ROOT):
        raise RuntimeError('Release output must be outside the QMK tree and project checkout')
    manifest=validate()
    tag=manifest['release']
    if subprocess.check_output(['git','-C',str(ROOT),'status','--porcelain'],text=True).strip():
        raise RuntimeError('Commit all public project changes before packaging')
    commit=subprocess.check_output(['git','-C',str(ROOT),'rev-parse','HEAD'],text=True).strip()
    upstream=subprocess.check_output(['git','-C',str(qmk),'rev-parse','HEAD'],text=True).strip()
    if upstream!=manifest['qmk_commit']: raise RuntimeError('QMK base differs from the pinned commit')
    changes=set(subprocess.check_output(['git','-C',str(qmk),'diff','--name-only','HEAD'],text=True).splitlines())
    if changes-{'drivers/led/sn32f2xx.c'}: raise RuntimeError('Unexpected tracked changes in QMK')
    original=subprocess.check_output(['git','-C',str(qmk),'show','HEAD:drivers/led/sn32f2xx.c']).decode('utf-8')
    line=next(line for line in original.splitlines() if 'static const uint32_t freq ' in line and BASE_TIMER in line)
    expected=original.replace(line,'#ifndef SN32F2XX_PWM_CLOCK_FREQUENCY\n#    define SN32F2XX_PWM_CLOCK_FREQUENCY '+BASE_TIMER+'\n#endif\nstatic const uint32_t freq = SN32F2XX_PWM_CLOCK_FREQUENCY;',1)
    if (qmk/'drivers/led/sn32f2xx.c').read_text(encoding='utf-8')!=expected:
        raise RuntimeError('Sonix clock patch differs from the approved change')
    keymap=qmk/'keyboards/designedbygg/redblade/rbfs/keymaps/aurora_via'
    for source in (ROOT/'firmware/aurora_via').iterdir():
        if source.is_file() and source.read_bytes().replace(b'\r\n',b'\n')!=(keymap/source.name).read_bytes().replace(b'\r\n',b'\n'):
            raise RuntimeError(f'QMK keymap source differs: {source.name}')
    if not native.is_file(): raise RuntimeError('Build the Windows native benchmark first')
    if hashlib.sha256(native.read_bytes()).hexdigest()!=manifest['native_benchmark']['sha256']:
        raise RuntimeError('Native benchmark differs from its manifest digest')
    destination.mkdir(parents=True,exist_ok=True)
    project=destination/f'redblade-plus-{tag}.zip'
    subprocess.run(['git','-C',str(ROOT),'archive','--format=zip',f'--prefix=redblade-plus-{tag}/',f'--output={project}','HEAD'],check=True)
    with zipfile.ZipFile(project,'a',zipfile.ZIP_DEFLATED) as archive:
        archive.write(native,f'redblade-plus-{tag}/dist/redblade-plus-benchmark-windows.exe')
    bundle=destination/f'redblade-plus-{tag}-complete-source.zip'
    count=0
    with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        with zipfile.ZipFile(project) as source:
            for entry in source.infolist():
                if not entry.is_dir() and (not entry.filename.endswith('.exe') or entry.filename==f'redblade-plus-{tag}/dist/redblade-plus-benchmark-windows.exe'):
                    archive.writestr(entry.filename,source.read(entry.filename))
        sources=list(tracked_sources(qmk,qmk))
        sources.extend((keymap/source.name,(keymap/source.name).relative_to(qmk)) for source in (ROOT/'firmware/aurora_via').iterdir() if source.is_file())
        for path,relative in sources:
            if path.name=='.git' or path.name.endswith(('.pyc','.o','.elf','.bin','.hex','.log')): continue
            archive.write(path,'qmk_firmware/'+relative.as_posix());count+=1
        archive.writestr('SOURCE-BUNDLE.txt',f'RedBlade+ {tag}\nProject commit: {commit}\nSonixQMK base: {upstream}\n'
            'Includes the selected keymap, timer patch and fetched QMK dependencies.\n'
            'Use the project build guide for the pinned-Git workflow. Upstream licenses are retained.\n')
    assets=[project,bundle]
    for source in (ROOT/'dist/redblade-fs-aurora-via.bin',ROOT/'dist/manifest.json',ROOT/'assets/redblade-plus.png',native):
        target=destination/source.name
        if source.resolve()!=target.resolve(): shutil.copy2(source,target)
        assets.append(target)
    sums=destination/'SHA256SUMS'
    sums.write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in assets),encoding='utf-8')
    with zipfile.ZipFile(bundle) as archive:
        assert not any('/.git/' in n or '/.build/' in n or '__pycache__' in n or 'via-backup' in n for n in archive.namelist())
    return dict(commit=commit,tag=tag,qmk_source_files=count,assets=[dict(name=p.name,bytes=p.stat().st_size) for p in [*assets,sums]])


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--qmk-tree',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--native-benchmark',type=Path,default=ROOT/'dist/redblade-plus-benchmark-windows.exe')
    args=parser.parse_args()
    print(json.dumps(package(args.qmk_tree.resolve(),args.output.resolve(),args.native_benchmark.resolve())),flush=True)
