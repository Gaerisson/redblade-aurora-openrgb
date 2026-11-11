(function (root) {
'use strict';
const MIN_SCALE = 0.1, MAX_SCALE = 6;
function constrain(view, image, scale, x, y) {
    const width = image.width * scale, height = image.height * scale;
    return {
        scale,
        x: width <= view.width ? (view.width - width) / 2 : Math.max(view.width - width, Math.min(0, x)),
        y: height <= view.height ? (view.height - height) / 2 : Math.max(view.height - height, Math.min(0, y))
    };
}
function fit(view, image) {
    const scale = Math.max(MIN_SCALE, Math.min(MAX_SCALE, view.width / image.width, view.height / image.height));
    return constrain(view, image, scale, 0, 0);
}
function zoomAt(current, scale, point, view, image) {
    scale = Math.max(MIN_SCALE, Math.min(MAX_SCALE, scale));
    const ratio = scale / current.scale;
    return constrain(view, image, scale, point.x - (point.x - current.x) * ratio, point.y - (point.y - current.y) * ratio);
}
class MapViewer {
    constructor(dialog) {
        this.dialog = dialog;
        this.viewport = dialog.querySelector('.map-viewport');
        this.image = dialog.querySelector('img');
        this.output = dialog.querySelector('output');
        this.position = { scale: 1, x: 0, y: 0 };
        this.fitted = 'width';
        this.drag = null;
        dialog.querySelector('[data-map="close"]').onclick = () => dialog.close();
        dialog.querySelector('[data-map="fit"]').onclick = () => this.fit();
        dialog.querySelector('[data-map="width"]').onclick = () => this.fitWidth();
        dialog.querySelector('[data-map="actual"]').onclick = () => this.zoom(1);
        dialog.querySelector('[data-map="in"]').onclick = () => this.zoom(this.position.scale * 1.25);
        dialog.querySelector('[data-map="out"]').onclick = () => this.zoom(this.position.scale / 1.25);
        this.image.onload = () => { if (dialog.open) this.fitWidth(); };
        this.viewport.addEventListener('wheel', event => {
            event.preventDefault();
            const rect = this.viewport.getBoundingClientRect();
            const delta = event.deltaY * (event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? rect.height : 1);
            this.zoom(this.position.scale * Math.exp(-Math.max(-500, Math.min(500, delta)) * .002), { x: event.clientX - rect.left, y: event.clientY - rect.top });
        }, { passive: false });
        this.viewport.addEventListener('pointerdown', event => {
            if (event.button !== 0) return;
            event.preventDefault();
            this.viewport.focus();
            this.viewport.setPointerCapture(event.pointerId);
            this.drag = { id: event.pointerId, x: event.clientX, y: event.clientY };
            this.viewport.classList.add('dragging');
        });
        this.viewport.addEventListener('pointermove', event => {
            if (this.drag?.id !== event.pointerId) return;
            const { scale, x, y } = this.position;
            this.position = constrain(this.view(), this.size(), scale, x + event.clientX - this.drag.x, y + event.clientY - this.drag.y);
            this.drag.x = event.clientX;
            this.drag.y = event.clientY;
            this.fitted = false;
            this.paint();
        });
        const stopDrag = () => { this.drag = null; this.viewport.classList.remove('dragging'); };
        this.viewport.addEventListener('pointerup', stopDrag);
        this.viewport.addEventListener('pointercancel', stopDrag);
        this.viewport.addEventListener('lostpointercapture', stopDrag);
        dialog.addEventListener('close', stopDrag);
        this.viewport.addEventListener('keydown', event => {
            if (event.key === '+' || event.key === '=') this.zoom(this.position.scale * 1.25);
            else if (event.key === '-') this.zoom(this.position.scale / 1.25);
            else if (event.key === '0') this.fit();
            else if (event.key === '1') this.zoom(1);
            else if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) {
                const { scale, x, y } = this.position;
                this.position = constrain(this.view(), this.size(), scale, x + (event.key === 'ArrowLeft' ? 80 : event.key === 'ArrowRight' ? -80 : 0), y + (event.key === 'ArrowUp' ? 80 : event.key === 'ArrowDown' ? -80 : 0));
                this.fitted = false;
                this.paint();
            } else return;
            event.preventDefault();
        });
        this.observer = new ResizeObserver(() => {
            if (!dialog.open) return;
            if (this.fitted) { if (this.fitted === 'width') this.fitWidth(); else this.fit(); }
            else { const p = this.position; this.position = constrain(this.view(), this.size(), p.scale, p.x, p.y); this.paint(); }
        });
        this.observer.observe(this.viewport);
    }
    view() { return { width: this.viewport.clientWidth, height: this.viewport.clientHeight }; }
    size() { return { width: this.image.naturalWidth || 1600, height: this.image.naturalHeight || 1540 }; }
    open(source) {
        this.image.alt = source.alt;
        if (this.image.getAttribute('src') !== source.getAttribute('src')) this.image.src = source.getAttribute('src');
        if (!this.dialog.open) this.dialog.showModal();
        this.fitWidth();
        this.viewport.focus();
    }
    fitWidth() { const view = this.view(), image = this.size(); this.fitted = 'width'; this.position = constrain(view, image, Math.max(MIN_SCALE, Math.min(MAX_SCALE, view.width / image.width)), 0, 0); this.paint(); }
    fit() { this.fitted = 'whole'; this.position = fit(this.view(), this.size()); this.paint(); }
    zoom(scale, point) {
        const view = this.view();
        this.fitted = false;
        this.position = zoomAt(this.position, scale, point || { x: view.width / 2, y: view.height / 2 }, view, this.size());
        this.paint();
    }
    paint() {
        const p = this.position;
        this.image.style.transform = `translate(${p.x}px, ${p.y}px) scale(${p.scale})`;
        this.output.textContent = `${Math.round(p.scale * 100)}%`;
        this.dialog.querySelector('[data-map="out"]').disabled = p.scale <= MIN_SCALE;
        this.dialog.querySelector('[data-map="in"]').disabled = p.scale >= MAX_SCALE;
    }
}
if (typeof module !== 'undefined' && module.exports) module.exports = { constrain, fit, zoomAt };
else root.RedBladeMapViewer = MapViewer;
})(typeof globalThis === 'undefined' ? this : globalThis);
