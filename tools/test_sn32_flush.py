"""Exercise the production-patched Sonix flush with an interrupt observer.

The upstream fixture is from pinned SonixQMK 62973ee4. Extract the real patched
function, rather than reproducing its ordering in the synchronous QMK fixture.
"""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile

from prepare_firmware import patched_driver


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cc', default=os.environ.get('CC', 'cc'))
    parser.add_argument('--sanitize', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    patched = patched_driver((root/'tests/upstream/sn32f2xx.c').read_text(encoding='utf-8'))
    start = patched.index('void sn32f2xx_flush(void) {')
    end = patched.index('\n}\n', start) + 3
    prefix = '''#include "qmk_fixture.h"
#include <string.h>
typedef rgb_t RGB;
#define SN32F2XX_LED_COUNT 109
extern RGB led_state[109], led_state_buf[109];
extern bool led_state_buf_update_required;
void redblade_controls_dim_frame(uint8_t *pixels);
void redblade_controls_copy_frame(uint8_t *dst, const uint8_t *src);
void *observing_memcpy(void *dst, const void *src, size_t size);
#define memcpy observing_memcpy
'''
    with tempfile.TemporaryDirectory(prefix='redblade-flush-') as directory:
        generated = Path(directory)/'production_flush.c'
        generated.write_text(prefix + patched[start:end], encoding='utf-8')
        executable = Path(directory)/('flush-test.exe' if os.name == 'nt' else 'flush-test')
        command = [args.cc, '-std=c11', '-Wall', '-Wextra', '-Werror', '-O1', '-g', '-DUSE_CIE1931_CURVE']
        if args.sanitize:
            command += ['-fsanitize=address,undefined']
        command += ['-I'+str(root/path) for path in ('tests/stubs','tests','tests/upstream','firmware/common','firmware/aurora_via')]
        command += [str(root/path) for path in (
            'tests/sn32_flush_test.c', 'tests/qmk_fixture.c', 'tests/store_flash_fixture.c',
            'tests/upstream/color.c', 'tests/upstream/led_tables.c',
            'firmware/aurora_via/redblade_rgb.c', 'firmware/aurora_via/redblade_controls.c',
            'firmware/aurora_via/redblade_recorder.c', 'firmware/aurora_via/redblade_config.c',
            'firmware/aurora_via/redblade_store.c')]
        subprocess.run(command + [str(generated), '-o', str(executable)], check=True)
        subprocess.run([str(executable)], check=True)


if __name__ == '__main__':
    main()
