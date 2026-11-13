"""Install the keymap and verified timer/status-pixel patches into pinned QMK."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
from check_fn_map import check as check_fn_map

QMK_COMMIT = '62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60'
BASE_TIMER = '(RGB_MATRIX_HUE_STEP * RGB_MATRIX_SAT_STEP * RGB_MATRIX_VAL_STEP * RGB_MATRIX_SPD_STEP * RGB_MATRIX_LED_PROCESS_LIMIT)'
FAST_COMMIT = '''
#ifdef REDBLADE_FAST_INDICATORS
// Full's two Turbo status pixels bypass the general frame flush. No extra
// buffer, transport, timer change or interrupt masking: copy one RGB pixel.
void sn32f2xx_commit_color(int index) {
    if (index >= 0 && index < SN32F2XX_LED_COUNT) {
        led_state[index] = led_state_buf[index];
    }
}
#endif
'''


def patched_driver(original, fast=True):
    line = next((line for line in original.splitlines() if 'static const uint32_t freq ' in line and BASE_TIMER in line), None)
    if line is None:
        raise SystemExit('Unexpected pinned Sonix driver; refusing an unverified patch')
    clock = '#ifndef SN32F2XX_PWM_CLOCK_FREQUENCY\n#    define SN32F2XX_PWM_CLOCK_FREQUENCY ' + BASE_TIMER + '\n#endif\nstatic const uint32_t freq = SN32F2XX_PWM_CLOCK_FREQUENCY;'
    return original.replace(line, clock, 1) + (FAST_COMMIT if fast else '')


def prepare(qmk, edition='full'):
    actual = subprocess.check_output(['git', '-C', str(qmk), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != QMK_COMMIT:
        raise SystemExit(f'Expected pinned SonixQMK {QMK_COMMIT}, found {actual}')
    root = Path(__file__).resolve().parents[1]
    check_fn_map(root)
    board = qmk / 'keyboards/designedbygg/redblade/rbfs'
    definition = json.loads(re.sub(r',\s*([}\]])', r'\1', (board/'keyboard.json').read_text(encoding='utf-8')))
    positions = [tuple(x['matrix']) for x in definition['rgb_matrix']['layout']]
    assert len(positions) == len(set(positions)) == 109
    assert positions[16:20] == [(0, 17), (0, 18), (0, 19), (0, 20)], 'Media-key fallback indices differ from the pinned board'
    original_map = (root/'firmware/aurora_via/led_positions.inc').read_text(encoding='utf-8')
    assert re.findall(r'\{(\d+), (\d+)\}', original_map) == [tuple(map(str, p)) for p in positions]
    keymap = 'openrgb_core' if edition == 'core' else 'aurora_via'
    target = board / 'keymaps' / keymap
    target.mkdir(parents=True, exist_ok=True)
    for source in (root/'firmware'/keymap).iterdir():
        if source.is_file():
            shutil.copy2(source, target/source.name)
    shutil.copy2(root/'firmware/common/redblade_usb_watchdog.h', target/'redblade_usb_watchdog.h')
    driver = qmk / 'drivers/led/sn32f2xx.c'
    text = driver.read_text(encoding='utf-8')
    original = subprocess.check_output(['git','-C',str(qmk),'show','HEAD:drivers/led/sn32f2xx.c']).decode('utf-8')
    expected = patched_driver(original)
    if text not in (original, patched_driver(original, fast=False), expected):
        raise SystemExit('Unexpected local Sonix driver changes; refusing to overwrite them')
    if text != expected:
        driver.write_text(expected, encoding='utf-8')
    print('Pinned Red Blade FS keymap installed; original 720896 Hz scan/PWM clock preserved')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('qmk', type=Path)
    parser.add_argument('--edition', choices=['full','core'], default='full')
    args=parser.parse_args()
    prepare(args.qmk.resolve(),args.edition)
