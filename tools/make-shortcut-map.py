"""Generate bilingual, printable shortcut maps from the shipped keyboard geometry."""
import argparse
from html import escape
import json
from pathlib import Path
import re
from check_fn_map import ROOT, keymap_layers

W, H = 2400, 2530
RED, WHITE, MUTED = '#f23b5c', '#f2f1f4', '#a9a7b1'

def generate(qmk=None):
    layout = json.loads((ROOT/'app/Resources/keyboard-layout.json').read_text(encoding='utf-8'))
    leds = json.loads((ROOT/'led-map.json').read_text(encoding='utf-8'))
    keys = keymap_layers()[0]
    assert len(layout) == len(leds) == len(keys) == 109 and keys[102] == 'MO(1)'
    assert [(k['Row'],k['Column']) for k in layout] == [tuple(k['matrix']) for k in leds]
    if qmk:
        source = qmk/'keyboards/designedbygg/redblade/rbfs/keyboard.json'
        board = json.loads(re.sub(r',\s*([}\]])',r'\1',source.read_text(encoding='utf-8')))
        assert [tuple(k['matrix']) for k in board['layouts']['LAYOUT_fullsize_extended_iso']['layout']] == [tuple(k['matrix']) for k in leds]
    dest = ROOT/'docs/assets'; dest.mkdir(parents=True,exist_ok=True)
    for lang in ('en','fr'):
        fr = lang == 'fr'
        t = lambda en, french: french if fr else en
        parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
                 f'<rect width="{W}" height="{H}" fill="#0d0e12"/>',
                 '<style>text{font-family:Segoe UI,Arial,sans-serif;fill:#f2f1f4}.brand{font-size:30px;font-weight:750;letter-spacing:3px}.title{font-size:61px;font-weight:750;letter-spacing:-1px}.subtitle{font-size:27px;fill:#aaa8b1}.section{font-size:31px;font-weight:700}.body{font-size:25px}.small{font-size:22px;fill:#aaa8b1}.tiny{font-size:17px;font-weight:700;text-anchor:middle;fill:#aaa8b1}.key{font-size:19px;font-weight:700;text-anchor:middle}.tag{font-size:18px;font-weight:700;letter-spacing:1px}.num{font-size:24px;font-weight:750;text-anchor:middle}.line{font-size:24px}</style>']
        def text(x,y,value,cls='body',color=None):
            parts.append(f'<text x="{x}" y="{y}" class="{cls}"'+(f' fill="{color}" style="fill:{color}"' if color else '')+'>'+escape(value)+'</text>')
        def rect(x,y,w,h,fill,stroke='none',rx=14):
            parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}" stroke="{stroke}" stroke-width="2"/>')
        def line(x,y,w): parts.append(f'<path d="M{x} {y}h{w}" stroke="#303039" stroke-width="2"/>')
        def card(x,y,w,h,title,tag,accent=RED):
            rect(x,y,w,h,'#18191f','#303039',20);rect(x,y,6,h,accent,'none',3)
            text(x+30,y+43,tag,'tag',accent); text(x+30,y+87,title,'section');line(x+30,y+110,w-60)
        def bullet(x,y,shortcut,description):
            text(x,y,shortcut,'body',WHITE);text(x,y+30,description,'small')
        text(64,65,'REDBLADE+', 'brand');text(315,65,'STUDIO  /  FULL SIZE ISO','tag',MUTED)
        text(64,138,t('Your Fn shortcut guide','Votre guide des raccourcis Fn'),'title')
        text(65,182,t('Hold Fn, then press the highlighted key. Factory layout shown in AZERTY.', 'Maintenez Fn, puis appuyez sur une touche repérée. Disposition d’usine AZERTY.'),'subtitle')
        rect(64,220,2272,584,'#15161c','#2c2d34',20)
        text(96,264,t('YOUR KEYBOARD','VOTRE CLAVIER'),'tag',RED)
        text(96,301,t('Colored outlines: factory Fn hints  •  Fn is between right Windows and Ctrl', 'Contours colorés : repères Fn d’usine  •  Fn est entre Windows droit et Ctrl'),'small')
        highlights={0,1,2,3,4,5,6,7,8,9,15,16,17,18,19,21,22,23,33,36,40,43,44,45,46,49,56,60,61,65,97,99,101,102,104,106}
        labels={0:'ESC',15:'PAUSE',16:'CALC',17:'MUTE',18:'VOL−',19:'VOL+',102:'FN',33:'← BACK',61:'CAPS',97:'WIN',101:'WIN',99:'SPACE',40:'−',60:'+'}
        sublabels=[t('HOST','HÔTE'),t('WHITE','BLANC'),t('RED','ROUGE'),'OFF',t('WAVE','VAGUE'),'MEDIA',t('IDLE','REPOS'),t('RESET','RAZ'),t('SAVE','SAUVER')]
        for i,k in enumerate(layout):
            x=104+k['X']*89;y=325+k['Y']*65;w=k['W']*89-8;h=k['H']*65-8
            label=labels.get(i,k.get('AzertyLabel') or k.get('Label') or '')
            if label==' ':label=''
            if len(label)>8:label=label[:7]
            if i==99:label=t('SPACE','ESPACE')
            # Default Fn-held hints in redblade_controls.c / redblade_recorder.c.
            # Slots are shown empty; -/+ and R show the default 60/s Turbo context.
            palette={102:'#41d6df',1:'#658fff',2:'#e4e8ec',3:'#f35465',4:'#777981',5:'#65d995',6:'#65d995',7:'#65d995',8:'#b589ef',9:'#65d995',0:'#f35465',15:'#f35465',16:'#41d6df',17:'#41d6df',18:'#658fff',19:'#658fff',21:'#777981',22:'#777981',23:'#777981',33:'#e4e8ec',36:'#e4e8ec',40:'#e9c25a',43:'#65d995',44:'#f35465',45:'#e9c25a',46:'#e5a34d',49:'#65d995',56:'#e4e8ec',60:'#e9c25a',61:'#65d995',65:'#f35465',97:'#65d995',99:'#f35465',101:'#65d995',104:'#e4e8ec',106:'#e4e8ec'}
            stroke=palette.get(i,'#3d3d45');fill='#202127' if i not in highlights else '#28262c'
            if i==102:fill='#15383d'
            rect(round(x,1),round(y,1),round(w,1),round(h,1),fill,stroke,7)
            text(round(x+w/2,1),round(y+h/2+(0 if 1<=i<=9 else 7),1),label,'key')
            if 1<=i<=9:text(round(x+w/2,1),round(y+h/2+20,1),sublabels[i-1],'tiny', stroke)
        text(98,769,t('1 / 2 / 3 use the top number row  ·  − / + use the numeric keypad', '1 / 2 / 3 : rangée supérieure  ·  − / + : pavé numérique'),'small')
        rect(64,825,2272,190,'#18191f','#303039',20)
        text(96,867,t('FACTORY COLOR GUIDE','LÉGENDE DES COULEURS D’USINE'),'tag',RED)
        legend=[('#41d6df',t('Cyan: Fn / recorder','Cyan : Fn / enregistreur')),('#f35465',t('Red: Turbo','Rouge : Turbo')),('#65d995',t('Green: game / options','Vert : jeu / options')),('#e5a34d',t('Amber: selection','Ambre : sélection')),('#658fff',t('Blue: host / media','Bleu : hôte / média')),('#e4e8ec',t('White: lighting','Blanc : éclairage')),('#b589ef',t('Violet: reset / context','Violet : reset / contexte'))]
        for n,(color,label) in enumerate(legend):
            x=96+(n%4)*550;y=893+(n//4)*36;rect(x,y,20,20,color,'none',10);text(x+31,y+18,label,'small')
        text(96,989,t('Fn held, default 60/s: empty slots gray; F5–F7 green if on. R and −/+ change with context. Colors are configurable.', 'Fn maintenue, Turbo à 60/s : emplacements vides gris ; F5–F7 verts si actifs. R et −/+ varient. Couleurs personnalisables.'),'small')
        card(64,1036,1118,600,t('Record in three steps','Enregistrer en trois étapes'),t('01  /  RECORDER','01  /  ENREGISTREUR'),'#41d6df')
        steps=[('1','Fn + CALC',t('Select recording','Préparer l’enregistrement')),('2','Fn + 1 / 2 / 3',t('Choose a slot, then type','Choisir un emplacement, puis taper')),('3','Fn + CALC',t('Finish and keep the sequence','Terminer et conserver la séquence'))]
        for n,(number,shortcut,description) in enumerate(steps):
            yy=1184+n*79;rect(96,yy-32,45,45,'#18434a','#41d6df',9);text(118,yy,number,'num','#a3f5f4');text(158,yy,shortcut,'body');text(158,yy+27,description,'small')
        bullet(96,1434,'Fn + 1 / 2 / 3',t('Play once; Fn + Shift + 1 / 2 / 3 loops. Repeat to stop.', 'Lire une fois ; Fn + Maj + 1 / 2 / 3 boucle, répéter pour arrêter.'))
        bullet(96,1510,t('Fn + numpad − / +','Fn + pavé − / +'),t('Playback speed: 0.5× to Hyper · Esc stops playback.', 'Vitesse de lecture : 0,5× à Hyper · Échap arrête la lecture.'))
        text(96,1568,t('Red recording  ·  green playback  ·  cyan loop', 'Rouge capture  ·  vert lecture  ·  cyan boucle'),'small')
        text(96,1598,t('3 RAM slots: erased on USB reset or power loss.', '3 emplacements en mémoire : effacés à la réinitialisation USB ou à la coupure.'),'small')
        card(1200,1036,1136,600,t('Turbo and game','Turbo et jeu'),t('02  /  REPEAT','02  /  RÉPÉTITION'),'#f35465')
        bullet(1232,1184,'Fn + T → '+t('key','touche'),t('Choose a key. Volume − / + / Mute: left / right / middle click.', 'Choisir une touche. Volume − / + / Muet : clic gauche / droit / milieu.'))
        bullet(1232,1270,'Fn + E / F / '+t('Space','Espace'),t('Toggle a quick keyboard Turbo target.', 'Activer ou arrêter un Turbo clavier direct.'))
        bullet(1232,1356,'Fn + R  /  Fn + '+t('Shift','Maj')+' + R',t('Next / previous preset: 25, 50, 100 presses/s.', 'Préréglage suivant / précédent : 25, 50, 100 appuis/s.'))
        bullet(1232,1442,t('Fn + numpad − / +','Fn + pavé − / +'),t('Adjust the rate; Esc stops the helper.', 'Régler la cadence ; Échap arrête la fonction.'))
        text(1232,1540,t('Active target and Fn: keyboard red  ·  left click amber', 'Cible et Fn actives : clavier rouge  ·  clic gauche ambre'),'small')
        text(1232,1575,t('Right click violet · middle click cyan; Fn and target pulse together.', 'Clic droit violet · milieu cyan ; Fn et cible pulsent ensemble.'),'small')
        card(64,1666,1118,423,t('Lighting profiles','Profils d’éclairage'),'03  /  F1–F4','#e4e8ec')
        for n,(key,en,frtxt) in enumerate([('F1','Host / OpenRGB','Logiciel / OpenRGB'),('F2','Reactive white','Blanc réactif'),('F3','Local red','Rouge autonome'),('F4','Lighting off','Éclairage éteint')]):
            yy=1821+n*56;rect(96,yy-32,72,43,'#25262d',['#658fff','#e4e8ec','#f35465','#777981'][n],7);text(132,yy,key,'key');text(192,yy,t(en,frtxt),'body')
        text(96,2059,t('Fn + Backspace: off / restore  ·  Fn + ← / →: brightness', 'Fn + Retour arrière : éteindre / rétablir  ·  Fn + ← / → : luminosité'),'small')
        card(1200,1666,1136,423,t('Lighting options','Options d’éclairage'),'04  /  F5–F9','#65d995')
        for n,(key,en,frtxt) in enumerate([('F5','Startup / resume wave','Vague au démarrage / à la reprise'),('F6','White fallback on media keys','Blanc de secours des touches média'),('F7','White / dark after 5 s without RGB','Blanc / sombre après 5 s sans RGB'),('F8','Restore defaults and stop helpers','Réinitialiser et arrêter les automatismes'),('F9','Save lighting settings only','Sauvegarder uniquement l’éclairage')]):
            yy=1818+n*52;rect(1232,yy-31,70,41,'#25262d',['#65d995','#65d995','#65d995','#b589ef','#65d995'][n],7);text(1267,yy,key,'key');text(1325,yy,t(en,frtxt),'body')
        card(64,2110,2272,295,t('Useful controls','Commandes utiles'),t('05  /  QUICK REFERENCE','05  /  REPÈRES RAPIDES'),'#65d995')
        bullet(96,2248,'Fn + WIN  /  Fn + '+t('Shift','Maj')+' + WIN',t('Lock / explicitly unlock Windows keys.', 'Verrouiller / déverrouiller les touches Windows.'))
        bullet(96,2331,'Fn + ESC  /  Fn + ESC 2 s',t('Stop helpers and unlock / enter bootloader.', 'Arrêter et déverrouiller / ouvrir le mode flash.'))
        bullet(1220,2248,'Fn + '+t('W (QWERTY) / Z (AZERTY)','Z (AZERTY) / W (QWERTY)'),t('Toggle forward hold.', 'Activer ou arrêter le maintien avant.'))
        bullet(1220,2331,'Fn + I',t('Anti-AFK: alternate mouse movement every 30 s; typing stops it.', 'Anti-AFK : mouvement alterné toutes les 30 s ; frappe pour arrêter.'))
        text(64,2455,t('Hold Fn to reveal shortcuts, even after Fn + F4; USB suspend keeps the keyboard dark.', 'Maintenez Fn pour afficher les raccourcis, même après Fn + F4 ; la veille USB garde le clavier éteint.'),'small')
        text(64,2493,t('Factory bindings may differ after VIA remapping  ·  Studio includes an interactive Fn explorer', 'Les raccourcis d’usine peuvent changer dans VIA  ·  Studio propose un explorateur Fn interactif'),'small')
        parts.append('</svg>')
        (dest/('shortcuts.fr.svg' if fr else 'shortcuts.svg')).write_text('\n'.join(parts)+'\n',encoding='utf-8')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--qmk-tree',type=Path,help='Optionally verify the pinned QMK layout')
    generate(parser.parse_args().qmk_tree)
