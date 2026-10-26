import test from 'node:test';
import assert from 'node:assert/strict';
import { bindHorizontalPan } from '../src/lib/horizontal-pan.ts';
import { bindScrollPan } from '../src/lib/scroll-pan.ts';

function setup(axes = 'horizontal') {
 const view = new EventTarget(), classes = new Set(), captured = new Set();
 let left = 0, top = 25;
 const surface = Object.assign(new EventTarget(), {
  ownerDocument: { defaultView: view }, parentElement: null,
  clientLeft: 0, clientTop: 0, clientWidth: 600, clientHeight: 400, scrollWidth: 2000, scrollHeight: 1500,
  hasAttribute: name => name === 'data-pan-surface',
  getBoundingClientRect: () => ({ left: 0, top: 0 }),
  classList: { add: value => classes.add(value), remove: value => classes.delete(value) },
  setPointerCapture: id => captured.add(id), hasPointerCapture: id => captured.has(id), releasePointerCapture: id => captured.delete(id),
 });
 Object.defineProperty(surface, 'scrollLeft', { get: () => left, set: value => { left = Math.max(0, Math.min(value, surface.scrollWidth - surface.clientWidth)); } });
 Object.defineProperty(surface, 'scrollTop', { get: () => top, set: value => { top = Math.max(0, Math.min(value, surface.scrollHeight - surface.clientHeight)); } });
 const cleanup = axes === 'horizontal' ? bindHorizontalPan(surface) : bindScrollPan(surface);
 function send(type, values = {}, recipient = surface) {
  const event = new Event(type, { cancelable: true });
  const { target = surface, ...rest } = values;
  Object.assign(event, { pointerType: 'mouse', pointerId: 1, isPrimary: true, button: 0, buttons: 1, clientX: 500, clientY: 150, detail: 1 }, rest);
  Object.defineProperty(event, 'target', { value: target });
  recipient.dispatchEvent(event); return event;
 }
 return { surface, view, classes, captured, send, cleanup };
}

test('horizontal grab pans after deliberate movement and suppresses only its trailing click', () => {
 const f = setup();
 assert.equal(f.send('pointerdown').defaultPrevented, false);
 f.send('pointermove', { clientX: 496 });
 assert.equal(f.surface.scrollLeft, 0); assert.equal(f.captured.size, 0);
 assert.equal(f.send('pointermove', { clientX: 300 }).defaultPrevented, true);
 assert.equal(f.surface.scrollLeft, 200); assert.equal(f.classes.has('is-horizontal-panning'), true);
 assert.equal(f.captured.has(1), true);
 f.send('pointermove', { clientX: 200 }); assert.equal(f.surface.scrollLeft, 300);
 f.send('pointerup', {}, f.view);
 assert.equal(f.captured.size, 0); assert.equal(f.classes.size, 0);
 assert.equal(f.send('click').defaultPrevented, true);
 assert.equal(f.send('click').defaultPrevented, false);
 assert.equal(f.surface.scrollTop, 25);
 f.cleanup();
});

test('controls, text, drag handles, native scrollbars and touch retain native behavior', () => {
 for (const values of [
  { target: { hasAttribute: () => false } }, // buttons, text or grips are not blank pan surfaces
  { pointerType: 'touch' }, { pointerType: 'pen' }, { button: 1 }, { button: 2 },
  { isPrimary: false }, { ctrlKey: true }, { metaKey: true }, { shiftKey: true }, { altKey: true },
  { clientX: 600 }, { clientY: 400 },
 ]) {
  const f = setup(); f.send('pointerdown', values); f.send('pointermove', { clientX: 200 });
  assert.equal(f.surface.scrollLeft, 0); assert.equal(f.captured.size, 0);
  assert.equal(f.send('click').defaultPrevented, false); f.cleanup();
 }
 const f = setup();
 const card = { hasAttribute: () => true, getBoundingClientRect: () => ({ left: 100, top: 0 }), clientLeft: 1, clientTop: 1, clientWidth: 280, clientHeight: 350, parentElement: f.surface };
 f.send('pointerdown', { target: card, clientX: 390 }); f.send('pointermove', { clientX: 200 });
 assert.equal(f.surface.scrollLeft, 0, 'nested scrollbar remains available'); f.cleanup();
});

test('vertical gestures, other pointers and surfaces without overflow never become a pan', () => {
 const f = setup(); f.send('pointerdown'); f.send('pointermove', { clientX: 498, clientY: 190 });
 f.send('pointermove', { clientX: 200, clientY: 190 }); assert.equal(f.surface.scrollLeft, 0);
 f.send('pointerdown'); f.send('pointermove', { pointerId: 2, clientX: 100 }); assert.equal(f.captured.size, 0);
 f.send('pointerup', {}, f.view);
 f.surface.scrollWidth = f.surface.clientWidth;
 f.send('pointerdown'); f.send('pointermove', { clientX: 100 }); assert.equal(f.classes.size, 0); f.cleanup();
});

test('reverse dragging clamps at the edge and keyboard or subsequent button clicks remain usable', () => {
 const f = setup(); f.surface.scrollLeft = 420; f.send('pointerdown');
 f.send('pointermove', { clientX: 1000 }); assert.equal(f.surface.scrollLeft, 0);
 f.send('pointerup', {}, f.view);
 assert.equal(f.send('click', { detail: 0 }).defaultPrevented, false, 'keyboard activation is never suppressed');
 f.send('pointerdown', { target: { hasAttribute: () => false } });
 assert.equal(f.send('click').defaultPrevented, false, 'next independent control click is usable'); f.cleanup();
});

test('cancel, Escape, blur, lost capture, released mouse and unmount release the grab', () => {
 for (const end of ['pointercancel', 'Escape', 'blur', 'lostpointercapture', 'released', 'cleanup']) {
  const f = setup(); f.send('pointerdown'); f.send('pointermove', { clientX: 300 });
  if (end === 'cleanup') f.cleanup();
  else if (end === 'Escape') f.send('keydown', { key: 'Escape' }, f.view);
  else if (end === 'released') f.send('pointermove', { buttons: 0, clientX: 100 });
  else f.send(end, {}, end === 'lostpointercapture' ? f.surface : f.view);
  assert.equal(f.captured.size, 0, end); assert.equal(f.classes.size, 0, end);
  f.send('pointermove', { clientX: 100 }); assert.equal(f.surface.scrollLeft, 200, end);
  f.cleanup();
 }
});


test('Gantt grab pans diagonally in both directions and clamps both scroll axes', () => {
 const f = setup('both'); f.surface.scrollLeft = 300; f.surface.scrollTop = 400;
 f.send('pointerdown'); f.send('pointermove', { clientX: 350, clientY: 50 });
 assert.equal(f.surface.scrollLeft, 450); assert.equal(f.surface.scrollTop, 500);
 assert.equal(f.classes.has('is-grab-panning'), true); assert.equal(f.captured.has(1), true);
 f.send('pointermove', { clientX: 650, clientY: 350 });
 assert.equal(f.surface.scrollLeft, 150); assert.equal(f.surface.scrollTop, 200);
 f.send('pointermove', { clientX: 2000, clientY: 2000 });
 assert.equal(f.surface.scrollLeft, 0); assert.equal(f.surface.scrollTop, 0);
 f.send('pointermove', { clientX: -2000, clientY: -2000 });
 assert.equal(f.surface.scrollLeft, 1400); assert.equal(f.surface.scrollTop, 1100);
 f.send('pointerup', {}, f.view); assert.equal(f.send('click').defaultPrevented, true); f.cleanup();
});

test('two-axis panning permits vertical-only overflow and preserves ordinary clicks below the threshold', () => {
 const f = setup('both'); f.surface.scrollWidth = f.surface.clientWidth;
 f.send('pointerdown'); f.send('pointermove', { clientX: 498, clientY: 148 });
 assert.equal(f.captured.size, 0); f.send('pointerup', {}, f.view); assert.equal(f.send('click').defaultPrevented, false);
 f.send('pointerdown'); f.send('pointermove', { clientY: 50 });
 assert.equal(f.surface.scrollTop, 125); assert.equal(f.surface.scrollLeft, 0);
 f.send('pointerup', {}, f.view); f.surface.scrollHeight = f.surface.clientHeight;
 f.send('pointerdown'); f.send('pointermove', { clientY: 0 }); assert.equal(f.captured.size, 0); f.cleanup();
});

test('Gantt controls, bar bodies, resize handles, scrollbar gutters and touch never start grab panning', () => {
 for (const values of [{ target: { hasAttribute: () => false } }, { pointerType: 'touch' }, { pointerType: 'pen' }, { button: 2 }, { ctrlKey: true }, { shiftKey: true }, { clientX: 600 }, { clientY: 400 }]) {
  const f = setup('both'); f.send('pointerdown', values); f.send('pointermove', { clientX: 200, clientY: 30 });
  assert.equal(f.surface.scrollLeft, 0); assert.equal(f.surface.scrollTop, 25); assert.equal(f.captured.size, 0); f.cleanup();
 }
});

test('two-axis grab releases on cancellation, focus loss, Escape and unmount', () => {
 for (const end of ['pointercancel', 'Escape', 'blur', 'lostpointercapture', 'released', 'cleanup']) {
  const f = setup('both'); f.send('pointerdown'); f.send('pointermove', { clientX: 300, clientY: 50 });
  if (end === 'cleanup') f.cleanup();
  else if (end === 'Escape') f.send('keydown', { key: 'Escape' }, f.view);
  else if (end === 'released') f.send('pointermove', { buttons: 0, clientX: 100 });
  else f.send(end, {}, end === 'lostpointercapture' ? f.surface : f.view);
  assert.equal(f.captured.size, 0, end); assert.equal(f.classes.size, 0, end);
  f.send('pointermove', { clientX: 100, clientY: 0 }); assert.equal(f.surface.scrollLeft, 200); assert.equal(f.surface.scrollTop, 125); f.cleanup();
 }
});
