const test = require('node:test');
const assert = require('node:assert/strict');
const { fit, constrain, zoomAt } = require('../map-viewer.js');
const view = { width: 1200, height: 700 }, image = { width: 1600, height: 1540 };
test('map fits its whole image in the available viewer', () => {
    const p = fit(view, image);
    assert.ok(p.scale * image.height <= view.height);
    assert.ok(p.scale * image.width <= view.width);
    assert.ok(Math.abs(p.y) < 1e-9);
    assert.ok(Math.abs(p.x - (view.width - image.width * p.scale) / 2) < 1e-9);
});
test('wheel zoom keeps the image point under the cursor', () => {
    const initial = { scale: 1, x: -200, y: -300 }, point = { x: 500, y: 400 };
    const next = zoomAt(initial, 2, point, view, image);
    assert.equal((point.x - initial.x) / initial.scale, (point.x - next.x) / next.scale);
    assert.equal((point.y - initial.y) / initial.scale, (point.y - next.y) / next.scale);
});
test('pan is bounded and small images stay centered', () => {
    assert.deepEqual(constrain(view, image, 1, 10000, -10000), { scale: 1, x: 0, y: -840 });
    assert.deepEqual(constrain(view, image, .25, -10000, 10000), { scale: .25, x: 400, y: 157.5 });
    assert.equal(zoomAt(fit(view, image), 100, { x: 600, y: 350 }, view, image).scale, 6);
    assert.equal(zoomAt(fit(view, image), 0, { x: 600, y: 350 }, view, image).scale, .1);
});
