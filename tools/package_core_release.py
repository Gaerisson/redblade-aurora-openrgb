"""Package Core's exact pinned QMK and selected sources, excluding Full modules."""
import argparse,json,subprocess,zipfile
from pathlib import Path
from check_core_release import ROOT,validate
from package_release import tracked_sources
def package(qmk,destination):
 m=validate();assert not destination.is_relative_to(qmk) and not destination.is_relative_to(ROOT)
 assert not subprocess.check_output(['git','-C',str(ROOT),'status','--porcelain'],text=True).strip(),'Commit public sources first'
 assert subprocess.check_output(['git','-C',str(qmk),'rev-parse','HEAD'],text=True).strip()==m['qmk_commit']
 changes=set(subprocess.check_output(['git','-C',str(qmk),'diff','--name-only','HEAD'],text=True).splitlines())
 assert changes<={'drivers/led/sn32f2xx.c'},'Unexpected QMK changes'
 keymap=qmk/'keyboards/designedbygg/redblade/rbfs/keymaps/openrgb_core'
 selected=list((ROOT/'firmware/openrgb_core').iterdir())
 for source in selected:
  if source.is_file():assert source.read_bytes().replace(b'\r\n',b'\n')==(keymap/source.name).read_bytes().replace(b'\r\n',b'\n')
 common=ROOT/'firmware/common/redblade_usb_watchdog.h'
 assert common.read_bytes().replace(b'\r\n',b'\n')==(keymap/common.name).read_bytes().replace(b'\r\n',b'\n'),'QMK common watchdog differs'
 destination.mkdir(parents=True,exist_ok=True);target=destination/f'redblade-openrgb-core-{m["release"]}-complete-source.zip'
 commit=subprocess.check_output(['git','-C',str(ROOT),'rev-parse','HEAD'],text=True).strip()
 sources=list(tracked_sources(qmk,qmk))+[(keymap/s.name,(keymap/s.name).relative_to(qmk)) for s in selected if s.is_file()]+[(keymap/common.name,(keymap/common.name).relative_to(qmk))]
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for path,relative in sources:
   if path.name=='.git' or path.name.endswith(('.pyc','.o','.elf','.bin','.hex','.log')):continue
   z.write(path,'qmk_firmware/'+relative.as_posix())
  z.write(ROOT/'LICENSE','LICENSE');z.write(ROOT/'dist/manifest-core.json','manifest-core.json')
  z.writestr('SOURCE-BUNDLE.txt',f'RedBlade+ OpenRGB Core {m["release"]}\nProject commit: {commit}\nSonixQMK: {m["qmk_commit"]}\n'
   'Only Core custom sources are included. Full game/Recorder modules are excluded.\n'
   'The pinned QMK sources, fetched dependencies and shared-clock patch are included.\n'
   'In a configured QMK build environment: cd qmk_firmware; qmk compile -kb designedbygg/redblade/rbfs -km openrgb_core -e SKIP_GIT=yes\n'
   'USB 320F:5136, SN32F248B, Full Size ISO only. Fn+Esc held 2 seconds enters the bootloader.\n'
   'Core: per-key Direct RGB/VIA, off after 5 seconds without RGB and on suspend; no added game helpers.\n')
 with zipfile.ZipFile(target) as z:assert not any('/keymaps/aurora_via/' in n or 'redblade_recorder' in n or '/.git/' in n for n in z.namelist())
 return dict(file=target.name,qmk_source_files=len(sources),commit=commit,bytes=target.stat().st_size)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--qmk-tree',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();print(json.dumps(package(a.qmk_tree.resolve(),a.output.resolve())))
