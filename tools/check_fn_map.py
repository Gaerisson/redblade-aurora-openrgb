"""Verify that published Fn bindings match the actual compiled keymap."""
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def keymap_layers(root=ROOT):
    text = (root/'firmware/aurora_via/keymap.c').read_text(encoding='utf-8')
    return {int(layer): [code.strip() for code in body.split(',') if code.strip()]
            for layer, body in re.findall(r'\[(\d+)\]\s*=\s*LAYOUT_fullsize_extended_iso\((.*?)\n\s*\)', text, re.S)}


def check(root=ROOT):
    doc = json.loads((root/'fn-shortcuts.json').read_text(encoding='utf-8'))
    assert doc['layer'] == 1 and doc['minimum_firmware'] == [0, 13, 0]
    layers = keymap_layers(root)
    assert len(layers[0]) == len(layers[1]) == 109
    positions = [tuple(led['matrix']) for led in json.loads((root/'led-map.json').read_text(encoding='utf-8'))]
    points=json.loads((root/'led-map.json').read_text(encoding='utf-8'))
    fixture=re.findall(r'\{(\d+), (\d+)\}',(root/'tests/led_points.inc').read_text(encoding='utf-8'))
    assert [(int(x),int(y)) for x,y in fixture]==[(p['x'],p['y']) for p in points], 'Benchmark LED geometry differs from the board'
    header = (root/'firmware/aurora_via/redblade_controls.h').read_text(encoding='utf-8')
    symbols = re.findall(r'\bRB_[A-Z0-9_]+\b', re.search(r'enum redblade_keycodes\s*\{(.*?)\}', header, re.S).group(1))
    codes = {name:0x7E40+i for i,name in enumerate(symbols)}
    codes['KC_MSTP'] = 0xAD
    expected = {pos:code for pos,code in zip(positions,layers[1]) if code != '_______'}
    actual = {}
    for binding in doc['bindings']:
        pos = (binding['row'],binding['col'])
        assert pos not in actual and pos in positions
        assert codes[binding['symbol']] == binding['code']
        actual[pos] = binding['symbol']
    assert expected == actual, 'Fn mapping differs from compiled keymap'
    assert actual[(3,0)] == 'RB_CAPS_WORD'
    assert actual[(0,17)] == 'RB_REC_ARM' and actual[(0,18)] == 'RB_PLAY'
    assert [actual[(1,col)] for col in (1,2,3)] == ['RB_REC_1','RB_REC_2','RB_REC_3']
    removed={(3,13),(4,15),(5,15)}
    assert {(e['row'],e['col']) for e in doc['clear_bindings']} == removed
    for e in doc['clear_bindings']:
        assert e['code']==1 and layers[1][positions.index((e['row'],e['col']))]=='_______'
    assert [layers[0][i] for i in (21,22,23)] == ['KC_1','KC_2','KC_3']
    assert actual[(5,1)] == actual[(5,12)] == 'RB_GAME'
    assert actual[(0,19)] == 'RB_PREV' and actual[(0,20)] == 'RB_NEXT'
    matrix_fixture=re.findall(r'\d+', (root/'tests/led_matrix.inc').read_text(encoding='utf-8'))
    expected_matrix=[255]*126
    for i,(row,col) in enumerate(positions): expected_matrix[row*21+col]=i
    assert list(map(int,matrix_fixture))==expected_matrix, 'Test LED matrix differs from the board'
    for name,position in [('W',(2,2)),('E',(2,3)),('R',(2,4)),('T',(2,5)),('F',(3,4)),('SPACE',(5,6)),('FN',(5,11)),('MINUS',(1,20)),('PLUS',(2,20))]:
        index=int(re.search(r'RB_LED_'+name+r'\s*=\s*(\d+)',header).group(1))
        assert positions[index] == position, 'Game-helper LED indices differ from the board'
    semantic={'FN':'MO(1)','LGUI':'KC_LGUI','RGUI':'KC_RGUI','CAPS':'KC_CAPS','LSHIFT':'KC_LSFT','RSHIFT':'KC_RSFT','NUM':'KC_NUM','SCROLL':'KC_SCRL'}
    for name,key in semantic.items():
        index=int(re.search(r'RB_LED_'+name+r'\s*=\s*(\d+)',header).group(1))
        assert layers[0][index]==key, f'{name} status is on the wrong physical key'
    # This is a contract between the light mask, public VIA bindings and board
    # geometry; a remapped/default shortcut must not silently stay hidden.
    controls=(root/'firmware/aurora_via/redblade_controls.c').read_text(encoding='utf-8')
    mask_text=re.search(r'fn_shortcut_led_mask\[\]\s*=\s*\{(.*?)\}',controls,re.S).group(1)
    mask=bytes(int(n,16) for n in re.findall(r'0x[0-9A-Fa-f]+',mask_text))
    assert len(mask)==14
    fn_index=int(re.search(r'RB_LED_FN\s*=\s*(\d+)',header).group(1))
    expected_leds={positions.index(pos) for pos in actual}|{fn_index}
    lit_leds={i for i in range(112) if mask[i//8]&(1<<(i%8))}
    assert lit_leds==expected_leds, 'Fn focus mask differs from the published shortcuts'
    return doc


if __name__ == '__main__':
    print(f'PASS: {len(check()["bindings"])} Fn bindings match firmware and all four media keys')
