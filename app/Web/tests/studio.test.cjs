const test = require('node:test');
const assert = require('node:assert/strict');
const { hsvToHex, hexToHsv, canStartEffect, capsWordOptions, findShortcuts, createBridge, selectedLeds } = require('../app.js');
test('firmware versions follow the selected edition independently', () => {
  const {firmwareForEdition}=require('../app.js');
  const info={bundledVersion:'0.13.0',latestVersion:'0.13.0',recommendedSource:'latest',bundledCoreVersion:'0.12.0',latestCoreVersion:'0.12.0',recommendedCoreSource:'bundled'};
  assert.equal(firmwareForEdition(info,'full').bundledVersion,'0.13.0');
  const core=firmwareForEdition(info,'core');
  assert.equal(core.bundledVersion,'0.12.0');assert.equal(core.latestVersion,'0.12.0');assert.equal(core.recommendedSource,'bundled');
  assert.equal(info.bundledVersion,'0.13.0');
});
test('converts HSV keyboard colours to RGB', () => {
  assert.equal(hsvToHex({h:0,s:255,v:255}), '#ff0000');
  assert.equal(hsvToHex({h:0,s:0,v:64}), '#404040');
});
test('selection only includes valid unique LED indices', () => {
  assert.deepEqual(selectedLeds([1,1,-1,108,109,2.5]), [1,108]);
});
test('bridge correlates responses and rejects host errors', async () => {
  const sent=[]; const bridge=createBridge(m=>sent.push(m));
  const result=bridge.call('setKeys',{leds:[1],color:'#ff0000'});
  assert.deepEqual(sent[0],{id:'1',action:'setKeys',args:{leds:[1],color:'#ff0000'}});
  bridge.receive({id:'1',ok:true,data:{saved:true}});
  assert.deepEqual(await result,{saved:true});
  const failure=bridge.call('save',{}); bridge.receive({id:'2',ok:false,error:'offline'});
  await assert.rejects(failure,/offline/);
});

test('converts preview color back to keyboard HSV', () => {
  assert.deepEqual(hexToHsv('#ff0000'), {h:0,s:255,v:255});
  assert.deepEqual(hexToHsv('#404040'), {h:0,s:0,v:64});
});

test('software effects use SDK capability independent of Studio connection', () => {
  assert.equal(canStartEffect({app:{},keyboard:{status:'offline'},capabilities:{softwareEffects:true}}),true);
  assert.equal(canStartEffect({app:{preview:true},capabilities:{softwareEffects:true}}),false);
  assert.equal(canStartEffect({app:{busy:true},capabilities:{softwareEffects:true}}),false);
  assert.equal(canStartEffect({app:{},capabilities:{softwareEffects:false}}),false);
});

test('Caps Word offers AZERTY 0 and QWERTY 1 independently of keyboard labels', () => {
  assert.deepEqual(capsWordOptions(), [['0','AZERTY'],['1','QWERTY']]);
});

test('shortcut search finds localized recorder loops and mouse turbo instructions', () => {
  const loop=findShortcuts('boucle','fr');
  assert.ok(loop.some(x=>x.group==='Enregistreur' && x.keys.includes('Maj')));
  const mouse=findShortcuts('mouse','en');
  assert.ok(mouse.some(x=>x.group==='Turbo & game' && x.keys.includes('Volume')));
  assert.ok(findShortcuts('F9','fr').some(x=>x.description.includes('sauvegarde')));
});
test('linear and radial gradients respect selected geometry', () => {
 const {gradientColors}=require('../app.js'); const keys=[{led:0,x:0,y:0,w:1,h:1},{led:1,x:1,y:0,w:1,h:1},{led:2,x:2,y:0,w:1,h:1}];
 assert.deepEqual(gradientColors(keys,'linear','#000000','#ffffff',0).map(x=>x.color),['#000000','#808080','#ffffff']);
 assert.deepEqual(gradientColors(keys,'radial','#000000','#ffffff',0).map(x=>x.color),['#ffffff','#000000','#ffffff']);
});
test('marquee intersects key geometry and selection modes are deterministic', () => {
 const {keysInBox,combineSelection,dragExceeded}=require('../app.js');
 assert.deepEqual(keysInBox([{led:1,x:0,y:0,w:1,h:1},{led:2,x:2,y:0,w:1,h:1}],{x1:.5,y1:.5,x2:1.5,y2:1.5}),[1]);
 assert.deepEqual(combineSelection([1,2],[2,3],'subtract'),[1]);
 assert.deepEqual(combineSelection([1,2],[2,3],'add'),[1,2,3]);
 assert.equal(dragExceeded(0,0,2,2),false); assert.equal(dragExceeded(0,0,5,0),true);
});
test('ISO Enter marquee excludes the missing lower-left corner', () => {
 const {keysInBox}=require('../app.js'); const key={led:74,x:0,y:0,w:1.25,h:2};
 assert.deepEqual(keysInBox([key],{x1:0,y1:1.1,x2:.2,y2:1.9}),[]);
 assert.deepEqual(keysInBox([key],{x1:.3,y1:1.1,x2:.6,y2:1.9}),[74]);
});
test('effect settings defaults and zone validation preserve old requests', () => {
 const {normalizeEffectOptions}=require('../app.js');
 const defaults=normalizeEffectOptions({});
 assert.deepEqual(defaults,{angle:0,scale:100,reverse:false,palette:[],targetLeds:null,background:'#000000',reactiveEnabled:false,reactiveStyle:'key',reactiveColor:'#ff274e',reactiveDurationMs:700,reactiveWidth:2,reactiveOpacity:100});
 assert.equal(normalizeEffectOptions({targetLeds:[]}),null);
 assert.equal(normalizeEffectOptions({palette:['#ffffff']}),null);
 assert.equal(normalizeEffectOptions({targetLeds:[109]}),null);
 assert.equal(normalizeEffectOptions({reactiveWidth:9}),null);
});
test('preview gradient normalizes actual key origins and respects targets', () => {
 const {renderPreviewFrame}=require('../app.js');
 const keys=[{led:0,x:10,y:3,column:0},{led:1,x:20,y:3,column:1},{led:2,x:30,y:3,column:2}];
 const args={preset:'gradient',color1:'#000000',color2:'#ffffff',speed:50,brightness:255,angle:0,scale:100,reverse:false,palette:[],targetLeds:[0,1],background:'#102030',reactiveEnabled:false,reactiveStyle:'key',reactiveColor:'#ff0000',reactiveDurationMs:1000,reactiveWidth:2,reactiveOpacity:100};
 assert.deepEqual(renderPreviewFrame(args,keys,0,[]),[{led:0,rgb:[0,0,0]},{led:1,rgb:[128,128,128]},{led:2,rgb:[16,32,48]}]);
});
test('preview rain uses physical column and real elapsed time', () => {
 const {renderPreviewFrame}=require('../app.js');
 const keys=[{led:0,x:0,y:0,column:0},{led:1,x:1,y:1,column:0}];
 const args={preset:'rain',color1:'#ffffff',color2:'#ffffff',speed:50,brightness:255,angle:0,scale:100,reverse:false,palette:[],targetLeds:null,background:'#000000',reactiveEnabled:false,reactiveStyle:'key',reactiveColor:'#ff0000',reactiveDurationMs:1000,reactiveWidth:2,reactiveOpacity:100};
 assert.deepEqual(renderPreviewFrame(args,keys,0,[]).map(x=>x.rgb),[[255,255,255],[0,0,0]]);
 assert.notDeepEqual(renderPreviewFrame(args,keys,0,[]),renderPreviewFrame(args,keys,6000,[]));
});
test('preview reactive ripple uses physical geometry and duration', () => {
 const {renderPreviewFrame}=require('../app.js');
 const keys=[{led:0,x:10,y:3,column:0},{led:1,x:21,y:3,column:1}];
 const args={preset:'reactive',color1:'#000000',color2:'#ffffff',speed:50,brightness:255,angle:0,scale:100,reverse:false,palette:[],targetLeds:null,background:'#000000',reactiveEnabled:true,reactiveStyle:'ripple',reactiveColor:'#ff0000',reactiveDurationMs:1000,reactiveWidth:2,reactiveOpacity:100};
 assert.deepEqual(renderPreviewFrame(args,keys,500,[{led:0,at:0}]).map(x=>x.rgb),[[0,0,0],[128,0,0]]);
 assert.deepEqual(renderPreviewFrame(args,keys,1000,[{led:0,at:0}]).map(x=>x.rgb),[[0,0,0],[0,0,0]]);
});
test('preview spectrum starts red and uses the native hue ordering', () => {
 const {renderPreviewFrame}=require('../app.js');
 const keys=[{led:0,x:0,y:0,column:0},{led:1,x:1,y:0,column:1},{led:2,x:2,y:0,column:2}];
 const args={preset:'spectrum',color1:'#000000',color2:'#ffffff',speed:50,brightness:255,angle:0,scale:100,reverse:false,palette:[],targetLeds:null,background:'#000000',reactiveEnabled:false,reactiveStyle:'key',reactiveColor:'#ff0000',reactiveDurationMs:1000,reactiveWidth:2,reactiveOpacity:100};
 assert.deepEqual(renderPreviewFrame(args,keys,0,[]).map(x=>x.rgb),[[255,0,0],[0,255,255],[255,0,0]]);
});
test('visual effect palette follows primary colors when empty and rejects invalid custom lists', () => {
 const {visualPalette}=require('../app.js');
 assert.deepEqual(visualPalette('', '#112233', '#445566'),['#112233','#445566']);
 assert.deepEqual(visualPalette('#abcdef, #123456', '#112233', '#445566'),['#abcdef','#123456']);
 assert.equal(visualPalette('#abcdef', '#112233', '#445566'),null);
 assert.equal(visualPalette('#abcdef, invalid', '#112233', '#445566'),null);
});

test('extended reaction duration chooses valid bounded firmware fields',()=>{const {reactionPair}=require('../app.js');assert.deepEqual(reactionPair(3960),{trailFrames:15,reactiveRate:8});assert.deepEqual(reactionPair(1980),{trailFrames:15,reactiveRate:4});assert.deepEqual(reactionPair(8000,false),{trailFrames:15,reactiveRate:0});});
test('update notice only selects newer Studio versions',()=>{const {newerVersion}=require('../app.js');assert.equal(newerVersion('0.13.0','0.12.0'),true);assert.equal(newerVersion('0.12.0','0.12.0'),false);assert.equal(newerVersion('0.11','0.12'),false);assert.equal(newerVersion('unknown','0.12'),false);});
test('gradient preview is stable over time',()=>{const {renderPreviewFrame}=require('../app.js');const a={preset:'gradient',color1:'#ff0000',color2:'#0000ff',speed:50,brightness:255,angle:0,scale:100,reverse:false,palette:[],targetLeds:null,background:'#000000',reactiveEnabled:false,reactiveColor:'#ffffff',reactiveDurationMs:1000,reactiveWidth:2,reactiveOpacity:100};const keys=[{led:0,x:0,y:0},{led:1,x:2,y:0}];assert.deepEqual(renderPreviewFrame(a,keys,0),renderPreviewFrame(a,keys,5000));});
test('gaming group selects four physical positions on both layouts',()=>{const {gamingLeds}=require('../app.js');const raw=require('../../Resources/keyboard-layout.json');const layout=raw.map(k=>({led:k.Led,label:k.Label,azertyLabel:k.AzertyLabel}));const ids=gamingLeds(layout);assert.equal(ids.length,4);const keys=layout.filter(k=>ids.includes(k.led));assert.deepEqual(keys.map(k=>k.label).sort(),['A','D','S','W']);assert.deepEqual(keys.map(k=>k.azertyLabel).sort(),['D','Q','S','Z']);});
test('keyboard label contrast follows solid key brightness',()=>{const {keyTextColor}=require('../app.js');assert.equal(keyTextColor('#ffffff'),'#111217');assert.equal(keyTextColor('#000000'),'#ffffff');assert.equal(keyTextColor('#dd002b'),'#ffffff');});
test('update notice waits until editing and lighting effects stop',()=>{const {updateNoticeEligible}=require('../app.js');const s={app:{version:'0.12.0'},updates:{availableVersion:'0.13.0'}};assert.equal(updateNoticeEligible(s,new Set(),false,false),true);assert.equal(updateNoticeEligible(s,new Set(),false,true),false);assert.equal(updateNoticeEligible({...s,softwareEffect:{active:true}},new Set(),false,false),false);assert.equal(updateNoticeEligible(s,new Set(['0.13.0']),false,false),false);});
