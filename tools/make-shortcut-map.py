"""Generate EN/FR SVG shortcut maps from the pinned QMK physical layout."""
import argparse
from html import escape
import json
from pathlib import Path
import re
from check_fn_map import ROOT, keymap_layers


def generate(qmk):
    source=qmk/'keyboards/designedbygg/redblade/rbfs/keyboard.json'
    board=json.loads(re.sub(r',\s*([}\]])',r'\1',source.read_text(encoding='utf-8')))
    layout=board['layouts']['LAYOUT_fullsize_extended_iso']['layout']
    keys=keymap_layers()[0]
    leds=json.loads((ROOT/'led-map.json').read_text(encoding='utf-8'))
    assert [x['matrix'] for x in layout]==[x['matrix'] for x in leds]
    assert len(layout)==len(keys)==109 and keys[102]=='MO(1)'
    dest=ROOT/'docs/assets';dest.mkdir(parents=True,exist_ok=True)
    for lang in ('en','fr'):
        fr=lang=='fr'
        parts=['<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1540" viewBox="0 0 1600 1540">',
               '<rect width="1600" height="1540" fill="#0c1019"/>',
               '<style>text{font-family:Arial,DejaVu Sans,sans-serif;fill:#e9eef7}.title{font-size:40px;font-weight:700}.sub{font-size:19px;fill:#a5b4c9}.key{font-size:15px;font-weight:700;text-anchor:middle}.tiny{font-size:10px;font-weight:700;text-anchor:middle;fill:#a5b4c9}.card{font-size:23px;font-weight:700}.body{font-size:19px}</style>']
        def text(x,y,value,style='body',fill=None):
            parts.append(f'<text x="{x}" y="{y}" class="{style}"'+(f' style="fill:{fill}"' if fill else '')+'>'+escape(value)+'</text>')
        def rect(x,y,w,h,color='#1c2637',stroke='#3b4b63',radius=8):
            parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{color}" stroke="{stroke}" stroke-width="2"/>')
        text(48,62,'RedBlade+ 0.9.0 FULL  /  OpenRGB Firmware','title')
        text(48,98,'Raccourcis + touches dynamiques · positions AZERTY' if fr else 'Shortcuts + dynamic key lighting · AZERTY positions','sub')
        text(48,128,'Focus Fn : seules les touches utiles restent allumées. Couleurs personnalisables dans Studio.' if fr else 'Fn focus: only useful keys stay lit. Customize colors and modules in Studio.','sub')
        special={0:('ESC','#e13743'),16:('CAL','#e13743'),17:('MUTE','#a593ed'),18:('VOL−','#a593ed'),19:('VOL+','#a593ed'),21:('1','#e13743'),22:('2','#e13743'),23:('3','#e13743'),37:('NUM','#49d68b'),61:('CAPS','#49d68b'),97:('WIN','#e13743'),101:('WIN','#e13743'),102:('FN','#4fcde8'),43:('Z','#49d68b'),44:('E','#f3bf57'),45:('R','#f3bf57'),46:('T','#f3bf57'),65:('F','#f3bf57'),99:('SPACE','#f3bf57'),40:('−','#a593ed'),60:('+','#a593ed'),14:('SCROLL','#f3bf57'),104:('←','#e9eef7'),106:('→','#e9eef7')}
        aliases={'KC_ESC':'ESC','KC_LCTL':'CTRL','KC_RCTL':'CTRL','KC_LSFT':'SHIFT','KC_RSFT':'SHIFT','KC_LALT':'ALT','KC_RALT':'ALT GR','KC_BSPC':'BACK','KC_TAB':'TAB','KC_PSCR':'PRINT','KC_SCRL':'SCROLL','KC_PAUS':'PAUSE','KC_CAPS':'CAPS','KC_INS':'INS','KC_HOME':'HOME','KC_PGUP':'PGUP','KC_DEL':'DEL','KC_END':'END','KC_PGDN':'PGDN','KC_UP':'↑','KC_LEFT':'←','KC_DOWN':'↓','KC_RGHT':'→','KC_NUM':'NUM','KC_PSLS':'/','KC_PAST':'*','KC_PMNS':'−','KC_PPLS':'+','KC_PENT':'ENTER','KC_PDOT':'.'}
        pad={'KC_NUM','KC_PSLS','KC_PAST','KC_PMNS','KC_PPLS','KC_PENT','KC_PDOT',*(f'KC_P{i}' for i in range(10))}
        for i,(cell,key) in enumerate(zip(layout,keys)):
            x=48+cell['x']*66;y=164+cell['y']*66;w=cell.get('w',1)*66-6;h=cell.get('h',1)*66-6
            azerty={'KC_Q':'A','KC_W':'Z','KC_A':'Q','KC_Z':'W','KC_M':',','KC_SCLN':'M','KC_MINS':')','KC_EQL':'=','KC_LBRC':'^','KC_RBRC':'$','KC_QUOT':'ù','KC_NUHS':'*','KC_NUBS':'< >','KC_COMM':';','KC_DOT':':','KC_SLSH':'!','KC_GRV':'²'}
            label=azerty.get(key,aliases.get(key,key[3:] if key.startswith('KC_') else key))
            if key in pad and key.startswith('KC_P') and key[-1].isdigit():label=key[-1]
            if len(label)>8:label=''
            stroke='#a4b2c8' if key in pad else '#344157'
            if i in special:label,stroke=special[i]
            if i in (33,36,56):stroke='#e9eef7'
            if i==15:stroke='#a593ed'
            if i==14:label='SCRL'
            if 1<=i<=9:stroke={1:'#4fcde8',2:'#e9eef7',3:'#e13743',4:'#546074'}.get(i,'#49d68b')
            rect(round(x,2),round(y,2),round(w,2),round(h,2),'#141e2c',stroke)
            text(round(x+w/2,2),round(y+h/2+(-5 if 1<=i<=9 else 5),2),label,'key')
            if 1<=i<=9:
                labels=['AURORA','BLANC' if fr else 'WHITE','ROUGE' if fr else 'RED','OFF','VAGUE' if fr else 'WAVE','MEDIA','SECOURS' if fr else 'IDLE','RESET','SAUVER' if fr else 'SAVE']
                text(round(x+w/2,2),round(y+h/2+15,2),labels[i-1],'tiny')
        def panel(x,y,w,h,title,lines,color):
            rect(x,y,w,h,'#141e2c',color);text(x+18,y+32,title,'card',color)
            for n,line in enumerate(lines):text(x+18,y+65+n*29,line)
        panel(48,606,744,270,'1 · RECORDER',[
            'Fn + CAL → Fn + 1 / 2 / 3 : enregistrer' if fr else 'Fn + CAL → Fn + 1 / 2 / 3: record into a slot',
            'Fn + CAL : terminer et garder la séquence' if fr else 'Fn + CAL: finish and keep the sequence',
            'Fn + 1 / 2 / 3 : lire une fois' if fr else 'Fn + 1 / 2 / 3: play once',
            'Fn + Maj + 1 / 2 / 3 : boucle ; même raccourci = arrêt' if fr else 'Fn + Shift + 1 / 2 / 3: loop; same shortcut = stop',
            'Fn + num − / + : 0,5× → 2× → 4× → 8× → HYPER' if fr else 'Fn + num − / +: 0.5× → 2× → 4× → 8× → HYPER',
            'Maintenir : enchaîner ; relâcher : valeur conservée' if fr else 'Hold: step repeatedly; release: keep selected speed',
            '1/2/3 = chiffres du haut · Échap : arrêter' if fr else '1/2/3 = top number row · Esc: stop'], '#e13743')
        panel(816,606,736,270,'2 · TURBO CLAVIER / SOURIS' if fr else '2 · KEYBOARD / MOUSE TURBO',[
            'Fn + T, puis une touche : choisir la cible' if fr else 'Fn + T, then a key: choose the repeat target',
            'Fn + T → VOL− / VOL+ : clic gauche / droit' if fr else 'Fn + T → VOL− / VOL+: left / right click',
            'Clic : ambre / violet · E / F / Espace : Turbo direct' if fr else 'Click: amber / purple · E / F / Space: quick Turbo',
            'Fn + R : 25 → 50 → 100 appuis/s (Maj : inverse)' if fr else 'Fn + R: 25 → 50 → 100 presses/s (Shift: reverse)',
            'Fn + num − / + : 5 → 250/s ; maintien pour régler' if fr else 'Fn + num − / +: 5 → 250/s; hold to step',
            'Fn + Z (AZERTY) : maintien pour avancer' if fr else 'Fn + Z (AZERTY): forward hold',
            'Échap ou appui normal sur la cible : arrêter' if fr else 'Esc or a normal target press: stop'], '#f3bf57')
        panel(48,900,488,238,'3 · ÉCLAIRAGE' if fr else '3 · LIGHTING',[
            'Fn + F1 : Aurora / OpenRGB',
            'Fn + F2 / F3 / F4 : blanc / rouge / éteint' if fr else 'Fn + F2 / F3 / F4: white / red / Off',
            'Fn + Retour arrière : éteindre / rétablir' if fr else 'Fn + Backspace: Off / restore',
            'Fn + ← / → : luminosité − / +' if fr else 'Fn + ← / →: brightness − / +',
            'Fn + Page bas / haut : luminosité − / +' if fr else 'Fn + Page Down / Up: brightness − / +'], '#4fcde8')
        panel(556,900,488,238,'4 · OPTIONS F5–F9',[
            'Fn + F5 : vague automatique oui / non' if fr else 'Fn + F5: automatic startup wave on / off',
            'Fn + F6 : blanc sur les 4 médias oui / non' if fr else 'Fn + F6: four media-key white fallback',
            'Fn + F7 : secours blanc / sombre' if fr else 'Fn + F7: white / dark idle fallback',
            'Fn + F8 : réglages par défaut + arrêt' if fr else 'Fn + F8: defaults + stop helpers',
            'Fn + F9 : enregistrer l’éclairage' if fr else 'Fn + F9: save lighting preferences'], '#a593ed')
        panel(1064,900,488,238,'5 · CONTRÔLES' if fr else '5 · CONTROLS',[
            'Fn + WIN : verrou (logos rouges + voyant)' if fr else 'Fn + WIN: lock (red keys + status LED)',
            'Fn + MUTE : lecture / pause (Maj : stop)' if fr else 'Fn + MUTE: play / pause (Shift: stop)',
            'Fn + VOL −/+ : piste précédente / suivante' if fr else 'Fn + VOL −/+: previous / next track',
            'Maj + Fn + VOL : luminosité − / +' if fr else 'Shift + Fn + VOL: brightness − / +',
            'Fn + PAUSE : arrêter la lecture média' if fr else 'Fn + PAUSE: stop media playback',
            'Fn + ESC : arrêt ; maintien 2 s : flash' if fr else 'Fn + ESC: stop; hold 2 s: bootloader'], '#49d68b')
        text(48,1185,'−/+ agit sur la dernière fonction choisie : Recorder = vitesse ; Turbo = cadence. Fn + R sélectionne Turbo.' if fr else '−/+ adjusts the last selected function: Recorder = speed; Turbo = rate. Fn + R selects Turbo.','body')
        text(48,1226,'CAL + FN respirent : rouge = enregistrement · vert = lecture simple · cyan = boucle · ambre = choix / limite' if fr else 'CAL + FN breathe: red = recording · green = one-shot · cyan = loop · amber = selection / limit','body')
        text(48,1267,'3 slots RAM partagés · environ 48 appuis simples au total · 30 s maximum · effacés au reset/débranchement USB' if fr else '3 shared RAM slots · about 48 simple presses total · 30 s maximum · cleared at USB reset/disconnect','sub')
        text(48,1308,'F8 conserve les enregistrements ; F9 sauvegarde seulement l’éclairage, pas les macros temporaires.' if fr else 'F8 keeps recordings; F9 saves lighting only, not the temporary recordings.','sub')
        text(48,1352,'Sans flux RGB depuis 5 s : CAPS vert si actif · SCROLL ambre si actif' if fr else 'After 5 s without RGB: CAPS green when active · SCROLL amber when active','body')
        text(48,1390,'NUM actif : pavé blanc + NUM vert · NUM inactif : pavé blanc très faible' if fr else 'NUM on: white numpad + green NUM · NUM off: very dim white numpad','body')
        text(48,1431,'Le flux RGB reprend la priorité sur CAPS/NUM/SCROLL. Éteint et suspension USB restent prioritaires.' if fr else 'Live RGB takes priority over CAPS/NUM/SCROLL. Lighting Off and USB suspension stay authoritative.','sub')
        text(48,1470,'Red Blade Full Size ISO · 4 touches multimédias · Les touches normales gardent leur fonction' if fr else 'Red Blade Full Size ISO · 4 media keys · Normal typing and media functions are preserved','sub')
        text(48,1510,'Voyant Windows en haut à droite : allumé = Windows verrouillée ; Fn seule ne l’allume pas.' if fr else 'Upper-right Windows indicator: on = Windows keys locked; holding Fn alone does not turn it on.','sub')
        parts.append('</svg>')
        (dest/('shortcuts.fr.svg' if fr else 'shortcuts.svg')).write_text('\n'.join(parts)+'\n',encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--qmk-tree',type=Path,required=True)
    generate(parser.parse_args().qmk_tree)
