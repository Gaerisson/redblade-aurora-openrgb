"""Install RedBlade+ Fn shortcuts after flashing, preserving base keys/macros.

Stop OpenRGB, Aurora and VIA first. Dry-run by default. --apply requires a
private backup path; a complete VIA backup is created before the first write.
"""
import argparse
import ctypes as C
import json
from pathlib import Path

from check_fn_map import check
from redblade import Keyboard, backup


def read_key(kb, row, col):
    result = kb.via(4,1,row,col)
    if result[:4] != bytes([4,1,row,col]):
        raise RuntimeError('Mismatched VIA key read; close competing HID clients')
    return int.from_bytes(result[4:6],'big')


def write_key(kb, row, col, code):
    payload=bytes([5,1,row,col,code>>8,code&255])
    packet=C.create_string_buffer(bytes([0])+payload+bytes(32-len(payload)),33)
    response=C.create_string_buffer(32)
    if kb.hid.hid_write(kb.handle,packet,33)!=33 or kb.hid.hid_read_timeout(kb.handle,response,32,1000)!=32:
        raise RuntimeError('VIA write failed')
    if response.raw[:6]!=payload or read_key(kb,row,col)!=code:
        raise RuntimeError('VIA write verification failed')


def apply(kb, bindings, destination):
    changes=[]
    for entry in bindings:
        old=read_key(kb,entry['row'],entry['col'])
        if old!=entry['code']: changes.append((entry,old))
    if not changes:
        print('Fn shortcuts already match RedBlade+. No writes needed.')
        return
    backup(kb,destination)
    written=[]
    try:
        for entry,old in changes:
            # Include the attempted write in rollback, even if its ACK is lost.
            written.append((entry,old))
            write_key(kb,entry['row'],entry['col'],entry['code'])
    except Exception as error:
        failed=0
        for entry,old in reversed(written):
            try: write_key(kb,entry['row'],entry['col'],old)
            except Exception: failed+=1
        raise RuntimeError(f'Install failed; rollback failures: {failed}. Private backup: {destination}') from error
    print(f'Installed {len(changes)} Fn bindings. Base layer and macros were preserved.')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply',action='store_true')
    parser.add_argument('--edition',choices=['full','core'],default='full')
    parser.add_argument('--backup',type=Path)
    parser.add_argument('--hid-dll',type=Path,default=Path(r'C:\Program Files\OpenRGB\hidapi-hotplug.dll'))
    args=parser.parse_args()
    if args.apply and not args.backup: parser.error('--apply requires --backup with a private new filename')
    mapping=check()
    if args.edition=='core': mapping=json.loads((Path(__file__).resolve().parents[1]/'fn-shortcuts-core.json').read_text(encoding='utf-8'))
    bindings=mapping['bindings']+mapping['clear_bindings']
    if not args.apply:
        print(f'Dry run: {len(bindings)} bindings on Fn layer 1; base keys/macros will be kept. Close RGB clients, then use --apply --backup <private-file>.')
        return 0
    kb=Keyboard(args.hid_dll)
    try:
        data=kb.request(0xFE,0x81)
        if data[:3]!=bytes([0xFE,0x81,1]) or data[24:32]!=b'RDBLFS01' or tuple(data[3:6])<tuple(mapping['minimum_firmware']):
            edition='OpenRGB Core' if args.edition=='core' else 'RedBlade+'
            minimum='.'.join(map(str,mapping['minimum_firmware']))
            raise RuntimeError(f'Flash {edition} {minimum} or newer before installing its Fn bindings')
        if bool(data[19]&128)!=(args.edition=='core'): raise RuntimeError('The selected Fn map does not match the installed firmware edition')
        apply(kb,bindings,args.backup)
    finally: kb.close()
    return 0


if __name__=='__main__': raise SystemExit(main())
