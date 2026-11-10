"""Validate the separately built OpenRGB Core image and its resource manifest."""
import argparse,hashlib,json,re,struct,subprocess
from pathlib import Path
from check_release import ram_usage
ROOT=Path(__file__).resolve().parents[1]
def validate(elf=None,size_tool='arm-none-eabi-size',update=False,nm_tool='arm-none-eabi-nm'):
 path=ROOT/'dist/manifest-core.json';binary=(ROOT/'dist/redblade-fs-openrgb-core.bin').read_bytes()
 assert len(binary)==65536 and binary[-4:]==bytes.fromhex('5555aaaa')
 stack,reset=struct.unpack_from('<II',binary);assert 0x20000000<stack<=0x20002000 and reset&1 and reset<65532
 config=(ROOT/'firmware/openrgb_core/config.h').read_text(encoding='utf-8')
 version='v'+'.'.join(re.search(r'#define REDBLADE_VERSION_'+x+r'\s+(\d+)',config).group(1) for x in ('MAJOR','MINOR','PATCH'))
 values=dict(release=version,sha256=hashlib.sha256(binary).hexdigest(),binary_bytes=len(binary))
 m=json.loads(path.read_text(encoding='utf-8')) if path.exists() else dict(edition='core',project='RedBlade+ OpenRGB Core',board='Designed by GG Redblade FS ISO',usb_vid='320f',usb_pid='5136',mcu='SN32F248B',led_count=109,qmk_commit='62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60',keyboard_usb_poll_interval_ms=1,shared_timer_clock_hz=720896,rgb_process_leds_per_task=8,rgb_minimum_frame_interval_ms=33,watchdog_ms=5000,watchdog_action='off',usb_suspend_action='off',via_layers=3,bootloader_shortcut='Fn+Esc held 2000 ms',game_helpers=False,recorder=False,mouse_hid=False,reactive_white=False,startup_animation=False,host_tests='passed',hardware_validation=None,physical_key_latency_measured=False)
 if elf:
  values.update(ram_usage(elf,nm_tool))
 if update:
  assert elf;m.update(values);path.write_text(json.dumps(m,indent=2)+'\n',encoding='utf-8')
  sums=ROOT/'dist/SHA256SUMS';lines=[x for x in sums.read_text(encoding='utf-8').splitlines() if not x.endswith('redblade-fs-openrgb-core.bin')]
  sums.write_text('\n'.join(lines+[values['sha256']+'  redblade-fs-openrgb-core.bin'])+'\n',encoding='utf-8')
 for key,value in values.items():assert m[key]==value,key
 assert m['ram_remaining_bytes']>=64 and not m['game_helpers'] and not m['recorder'] and not m['mouse_hid']
 assert any(x.split()==[values['sha256'],'redblade-fs-openrgb-core.bin'] for x in (ROOT/'dist/SHA256SUMS').read_text(encoding='utf-8').splitlines())
 return m
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--elf',type=Path);p.add_argument('--size-tool',default='arm-none-eabi-size');p.add_argument('--nm-tool',default='arm-none-eabi-nm');p.add_argument('--update',action='store_true');a=p.parse_args()
 print(json.dumps(validate(a.elf,a.size_tool,a.update,a.nm_tool)))
