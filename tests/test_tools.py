import importlib.util
import json
import shutil
import tempfile
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import benchmark
import ctypes
import redblade
import check_release
import check_core_release
from unittest.mock import MagicMock
from check_fn_map import check
spec=importlib.util.spec_from_file_location('fn_installer',ROOT/'tools/install-fn-layer.py')
installer=importlib.util.module_from_spec(spec);spec.loader.exec_module(installer)


class ToolTests(unittest.TestCase):
    def test_installer_reports_core_minimum_and_closes_rejected_device(self):
        info=bytearray(32);info[:6]=bytes([0xFE,0x81,1,0,7,0]);info[19]=128;info[24:]=b'RDBLFS01'
        kb=MagicMock();kb.request.return_value=bytes(info)
        with patch.object(sys,'argv',['install-fn-layer.py','--edition','core','--apply','--backup','unused-private.json']), \
             patch.object(installer,'Keyboard',return_value=kb),patch.object(installer,'apply') as install:
            with self.assertRaisesRegex(RuntimeError,r'OpenRGB Core 0\.8\.0 or newer'):
                installer.main()
        install.assert_not_called()
        kb.close.assert_called_once()

    def test_installer_accepts_full_at_its_own_minimum_version(self):
        info=bytearray(32);info[:6]=bytes([0xFE,0x81,1,0,13,0]);info[24:]=b'RDBLFS01'
        kb=MagicMock();kb.request.return_value=bytes(info)
        with patch.object(sys,'argv',['install-fn-layer.py','--edition','full','--apply','--backup','unused-private.json']), \
             patch.object(installer,'Keyboard',return_value=kb),patch.object(installer,'apply') as install:
            self.assertEqual(installer.main(),0)
        install.assert_called_once()
        kb.close.assert_called_once()

    def test_installer_rejects_new_full_map_on_old_firmware(self):
        info=bytearray(32);info[:6]=bytes([0xFE,0x81,1,0,12,0]);info[24:]=b'RDBLFS01'
        kb=MagicMock();kb.request.return_value=bytes(info)
        with patch.object(sys,'argv',['install-fn-layer.py','--edition','full','--apply','--backup','unused-private.json']), \
             patch.object(installer,'Keyboard',return_value=kb),patch.object(installer,'apply') as install:
            with self.assertRaisesRegex(RuntimeError,r'RedBlade\+ 0\.13\.0 or newer'):
                installer.main()
        install.assert_not_called()
        kb.close.assert_called_once()

    def test_either_manifest_update_order_preserves_both_binary_checksums(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as d:
            root=Path(d)
            root.resolve().relative_to(ROOT.resolve()) # Cleanup stays inside this checkout.
            shutil.copytree(ROOT/'firmware',root/'firmware')
            (root/'dist').mkdir();(root/'tests').mkdir()
            for name in ['led_points.inc','led_matrix.inc']:shutil.copy2(ROOT/'tests'/name,root/'tests'/name)
            for name in ['fn-shortcuts.json','led-map.json']:shutil.copy2(ROOT/name,root/name)
            for name in ['manifest.json','manifest-core.json','redblade-fs-aurora-via.bin','redblade-fs-openrgb-core.bin','SHA256SUMS']:shutil.copy2(ROOT/'dist'/name,root/'dist'/name)
            def output(args,**kw):
                return '20001fb8 B __heap_base__\n20002000 B __heap_end__\n' if '-n' in args else 'test compiler\n'
            with patch.object(check_release.subprocess,'check_output',side_effect=output),patch.object(check_core_release,'ROOT',root):
                check_core_release.validate('dummy.elf',update=True)
                check_release.validate(root,elf='dummy.elf',update=True)
                check_core_release.validate()
                check_release.validate(root,elf='dummy.elf',update=True)
                check_core_release.validate('dummy.elf',update=True)
                check_release.validate(root)

    def test_ram_guard_uses_aligned_heap_symbols(self):
        with patch.object(check_release.subprocess,'check_output',return_value='20001fc8 B __heap_base__\n20002000 B __heap_end__\n'):
            with self.assertRaisesRegex(AssertionError,'64 usable bytes'):check_release.ram_usage('dummy.elf')
        with patch.object(check_release.subprocess,'check_output',return_value='20001fc0 B __heap_base__\n20002000 B __heap_end__\n'):
            self.assertEqual(check_release.ram_usage('dummy.elf')['ram_remaining_bytes'],64)

    def test_core_map_removes_known_helpers_without_changing_unrelated_cells(self):
        core=json.loads((ROOT/'fn-shortcuts-core.json').read_text(encoding='utf-8'))
        entries=core['bindings']+core['clear_bindings']
        keys={(e['row'],e['col']):0x7E40 for e in entries};keys[(2,12)]=0x1234
        with patch.object(installer,'backup'), \
             patch.object(installer,'read_key',side_effect=lambda kb,r,c:keys[(r,c)]), \
             patch.object(installer,'write_key',side_effect=lambda kb,r,c,v:keys.__setitem__((r,c),v)):
            installer.apply(object(),entries,Path('mocked-private-backup.json'))
        self.assertEqual(keys[(0,0)],0x7E53)
        self.assertEqual(keys[(0,17)],1)
        self.assertEqual(keys[(2,3)],1)
        self.assertEqual(keys[(3,0)],1)
        self.assertEqual(keys[(2,12)],0x1234)
        for e in core['clear_bindings']:self.assertEqual(keys[(e['row'],e['col'])],1)

    def test_installer_refuses_full_map_on_core_firmware(self):
        info=bytearray(32);info[:6]=bytes([0xFE,0x81,1,0,13,0]);info[19]=128;info[24:]=b'RDBLFS01'
        kb=MagicMock();kb.request.return_value=bytes(info)
        with patch.object(sys,'argv',['install-fn-layer.py','--apply','--backup','unused-private.json']),patch.object(installer,'Keyboard',return_value=kb):
            with self.assertRaisesRegex(RuntimeError,'edition'):installer.main()

    def test_hid_enumeration_is_freed_on_wrong_identity(self):
        device=redblade.Device()
        device.usage_page=0xFF60;device.usage=0x61
        device.manufacturer='DesignedbyGG';device.product='Wrong model'
        pointer=ctypes.pointer(device)
        library=MagicMock();library.hid_enumerate.return_value=pointer
        with patch.object(redblade.C,'CDLL',return_value=library):
            with self.assertRaises(RuntimeError): redblade.Keyboard(Path('unused.dll'))
        library.hid_free_enumeration.assert_called_once_with(pointer)

    def test_hid_read_rejects_wrong_rgb_subcommand(self):
        device=object.__new__(redblade.Keyboard)
        device.request=lambda *args:bytes([8,0x41])+bytes(30)
        with self.assertRaises(RuntimeError):device.via(8,0x40)

    def test_public_fn_map_matches_actual_firmware(self):
        self.assertEqual(len(check()['bindings']),35)

    def test_timing_statistics_and_units(self):
        result=benchmark.summarize([1,2,3,4,5])
        self.assertEqual(result['median_ms'],3)
        self.assertEqual(result['p95_ms'],4.8)
        self.assertEqual(result['p99_ms'],4.96)
        self.assertEqual(result['count'],5)
        with self.assertRaises(ValueError): benchmark.summarize([])

    def test_hid_benchmark_only_uses_read_commands(self):
        class Device:
            count=0
            def request(self,*payload):
                if payload==(0xFE,0): return bytes([4,0,0,0])+b'RDBLFS01'+bytes([1])+bytes(19)
                assert payload==(0xFE,0x81)
                return bytes([0xFE,0x81,1,0,4,0])+bytes(18)+b'RDBLFS01'
            def via(self,*payload):
                assert payload==(8,0x40)
                self.count+=1
                return bytes([8,0x40,1])+bytes(29)
        device=Device();report=benchmark.hid_round_trips(device,10)
        self.assertEqual(device.count,42)
        self.assertEqual(report['count'],10)
        self.assertFalse(report['physical_key_latency_measured'])

    def test_fn_install_rolls_back_a_write_with_lost_ack(self):
        bindings=[dict(row=0,col=2,code=0x7E44),dict(row=0,col=3,code=0x7E45)]
        keys={(0,2):100,(0,3):101}
        backup_done=[]
        fail_once=[True]
        def write(kb,row,col,code):
            self.assertTrue(backup_done)
            keys[(row,col)]=code
            if col==3 and fail_once[0]:
                fail_once[0]=False
                raise RuntimeError('ACK lost after device accepted the write')
        path=Path('private-test-backup.json') # Mocked backup: no filesystem access.
        with patch.object(installer,'backup',side_effect=lambda *a:backup_done.append(True)), \
             patch.object(installer,'read_key',side_effect=lambda kb,row,col:keys[(row,col)]), \
             patch.object(installer,'write_key',side_effect=write):
            with self.assertRaises(RuntimeError): installer.apply(object(),bindings,path)
        self.assertEqual(keys,{(0,2):100,(0,3):101})

    def test_removed_fn_bindings_are_explicitly_cleared(self):
        removed=check()['clear_bindings']
        keys={(e['row'],e['col']):0x7E4B for e in removed}
        keys[(2,12)]=0x1234
        with patch.object(installer,'backup'), \
             patch.object(installer,'read_key',side_effect=lambda kb,r,c:keys[(r,c)]), \
             patch.object(installer,'write_key',side_effect=lambda kb,r,c,value:keys.__setitem__((r,c),value)):
            installer.apply(object(),removed,Path('mocked-private-backup.json'))
        for entry in removed:self.assertEqual(keys[(entry['row'],entry['col'])],1)
        self.assertEqual(keys[(2,12)],0x1234)

    def test_hid_benchmark_refuses_an_active_recorder(self):
        class Device:
            def request(self,*payload):
                if payload==(0xFE,0): return bytes([4,0,0,0])+b'RDBLFS01'+bytes([1])+bytes(19)
                self_payload=bytearray(32);self_payload[:6]=bytes([0xFE,0x81,1,0,7,0]);self_payload[19]=4;self_payload[24:]=b'RDBLFS01'
                return bytes(self_payload)
            def via(self,*payload):raise AssertionError('No timing requests while a recorder is active')
        with self.assertRaises(RuntimeError):benchmark.hid_round_trips(Device(),10)


if __name__=='__main__': unittest.main()
