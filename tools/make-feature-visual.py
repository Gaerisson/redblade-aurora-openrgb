"""Render exact-layout, repository-owned EN/FR feature showcase SVGs."""
import argparse,json,re
from html import escape
from pathlib import Path
from check_fn_map import ROOT,keymap_layers

def generate(qmk):
 board=json.loads(re.sub(r',\s*([}\]])',r'\1',(qmk/'keyboards/designedbygg/redblade/rbfs/keyboard.json').read_text(encoding='utf-8')))
 layout=board['layouts']['LAYOUT_fullsize_extended_iso']['layout'];keys=keymap_layers()[0]
 assert len(layout)==109 and keys[102]=='MO(1)'
 for fr in [False,True]:
  a=['<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1080" viewBox="0 0 1600 1080">',
     '<defs><radialGradient id="glow"><stop stop-color="#af1733" stop-opacity=".3"/><stop offset="1" stop-color="#090b10" stop-opacity="0"/></radialGradient></defs>',
     '<rect width="1600" height="1080" fill="#090b10"/><ellipse cx="600" cy="400" rx="1000" ry="520" fill="url(#glow)"/>',
     '<style>text{font-family:Arial,DejaVu Sans,sans-serif;fill:#f3f4f8}.brand{font-size:64px;font-weight:800;letter-spacing:-2px}.sub{font-size:25px;fill:#b8becb}.label{font-size:18px;font-weight:700;letter-spacing:2px}.card{font-size:28px;font-weight:700}.body{font-size:21px;fill:#b8becb}.key{font-size:12px;font-weight:700;text-anchor:middle}.small{font-size:16px;fill:#858e9f}</style>']
  def text(x,y,s,cl='body',color=None): a.append(f'<text x="{x}" y="{y}" class="{cl}"'+(f' style="fill:{color}"' if color else '')+'>'+escape(s)+'</text>')
  def rect(x,y,w,h,fill,stroke='none',r=12): a.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}"/>')
  text(64,88,'RedBlade+','brand');text(444,80,'OPENRGB FIRMWARE  /  STUDIO 0.12 PREVIEW','label','#ff5267')
  text(64,141,'Le clavier que tu connais. Plus de possibilités.' if fr else 'Your keyboard. More capable.','sub')
  text(64,201,'109 LED PAR TOUCHE' if fr else '109 PER-KEY LEDS','label','#e9eef7')
  text(465,201,'AURORA → OPENRGB','label','#58cde8')
  text(1050,201,'FN + ESC  ·  2 s → FLASH','label','#ff5267')
  # Full-size keyboard geometry from the actual pinned board, including all 4 media keys.
  rect(46,240,1508,430,'#11141b','#303641',18)
  labels={'KC_ESC':'ESC','KC_CAPS':'CAPS','KC_BSPC':'BACK','KC_NUM':'NUM','KC_CALC':'CAL','KC_MUTE':'MUTE','KC_VOLD':'VOL−','KC_VOLU':'VOL+','KC_SPC':'SPACE','MO(1)':'FN','KC_LGUI':'WIN','KC_RGUI':'WIN','KC_ENT':'ENTER','KC_PENT':'ENTER'}
  red={42,43,44,62,63,64,65,66};amber={18};purple={19};cyan={16,21,22,23,102}
  for i,(cell,key) in enumerate(zip(layout,keys)):
   x=66+cell['x']*64;y=260+cell['y']*64;w=cell.get('w',1)*64-6;h=cell.get('h',1)*64-6
   fill='#382028' if i in red else '#34291b' if i in amber else '#2b203e' if i in purple else '#17323a' if i in cyan else '#1c202a'
   stroke='#ff344e' if i in red else '#ffc15b' if i in amber else '#be8bff' if i in purple else '#59d2e9' if i in cyan else '#606775'
   rect(round(x,2),round(y,2),round(w,2),round(h,2),fill,stroke,6)
   label=labels.get(key,key[3:] if key.startswith('KC_') else key)
   label={'Q':'A','W':'Z','A':'Q','Z':'W','SCLN':'M','LCTL':'CTRL','RCTL':'CTRL','LSFT':'SHIFT','RSFT':'SHIFT','LALT':'ALT','RALT':'ALTGR','LEFT':'←','RGHT':'→','UP':'↑','DOWN':'↓'}.get(label,label)
   if len(label)>6:label=''
   text(round(x+w/2,2),round(y+h/2+4,2),label,'key',stroke if i in red|amber|purple|cyan else '#e9ecf2')
  text(64,709,'ILLUSTRATION DES FONCTIONS · LES ÉTATS NE SONT PAS SIMULTANÉS' if fr else 'FEATURE ILLUSTRATION · STATES ARE NOT SIMULTANEOUS','small')
  cards=[
   ('01','STUDIO','Profil actif · 109 touches' if fr else 'Active profile · 109 keys','24 couleurs · guide intégré' if fr else '24 colors · built-in guide','#5ad0e7'),
   ('02','HYPER + TURBO','Clavier + clic gauche/droit' if fr else 'Keys + left/right click','Cadence conservée après +' if fr else 'Speed stays after release','#ffba5a'),
   ('03','RGB STUDIO','12 effets logiciels' if fr else '12 software effects','OpenRGB · Aurora restauré' if fr else 'OpenRGB · Aurora restored','#ff5069'),
   ('04','OPTIONS','Caps Word · Fn + Caps','Arrêt auto configurable' if fr else 'Configurable safety stop','#b28af5')]
  for n,(num,title,l1,l2,color) in enumerate(cards):
   x=64+n*376;rect(x,752,352,224,'#141820','#303641');text(x+22,788,num,'label',color)
   text(x+22,831,title,'card',color);text(x+22,878,l1);text(x+22,915,l2)
  text(64,1027,'3 slots RAM partagés ≈ 48 appuis simples · Vitesses cibles, selon USB/application · Échap pour arrêter' if fr else '3 shared RAM slots ≈ 48 simple presses · Target speeds depend on USB/application · Esc to stop','small')
  text(64,1057,'Full 0.12 + Studio · Core : OpenRGB + VIA · Validation et limites dans la release' if fr else 'Full 0.12 + Studio · Core: OpenRGB + VIA · See release for checks and limits','small')
  a.append('</svg>');(ROOT/'docs/assets'/('features.fr.svg' if fr else 'features.svg')).write_text('\n'.join(a)+'\n',encoding='utf-8')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--qmk-tree',type=Path,required=True);generate(p.parse_args().qmk_tree)
