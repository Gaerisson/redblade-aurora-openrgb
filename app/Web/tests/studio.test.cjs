const test = require('node:test');
const assert = require('node:assert/strict');
const { hsvToHex, hexToHsv, canStartEffect, capsWordOptions, findShortcuts, createBridge, selectedLeds } = require('../app.js');
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
  assert.ok(loop.some(x=>x.group==='Recorder' && x.keys.includes('Maj')));
  const mouse=findShortcuts('mouse','en');
  assert.ok(mouse.some(x=>x.group==='Turbo & game' && x.keys.includes('Volume')));
  assert.ok(findShortcuts('F9','fr').some(x=>x.description.includes('sauvegarde')));
});
