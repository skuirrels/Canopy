import test from 'node:test';
import assert from 'node:assert/strict';
import { getViewportForBounds } from '@xyflow/react';
import { initialMapDirection, mapDirectionKey, mapFitPadding, MAP_MIN_ZOOM, MAP_START_ZOOM, initialMapViewport, mapViewportKey, savedMapViewport } from '../src/lib/map-view.ts';

// React Flow rounds the fitted camera to whole screen pixels.
const pixelTolerance = 1.01;

test('existing projects get the vertical default while new explicit choices are remembered per project', () => {
 assert.notEqual(mapDirectionKey('one'), 'sprout:map-direction:one');
 assert.notEqual(mapDirectionKey('one'), mapDirectionKey('two'));
 for (const saved of [null, '', 'vertical', 'obsolete']) assert.equal(initialMapDirection(saved), 'vertical');
 assert.equal(initialMapDirection('horizontal'), 'horizontal');
});

test('wide and tall contexts fit below the toolbar and above bottom controls, including negative saved coordinates', () => {
 for (const bounds of [{ x: -1200, y: -200, width: 12000, height: 600 }, { x: -200, y: -1800, width: 320, height: 5000 }]) {
  const padding = mapFitPadding(1400, 70, 130);
  const view = getViewportForBounds(bounds, 1400, 700, MAP_MIN_ZOOM, 1, padding);
  assert.ok(view.zoom < .15, 'fit must be allowed beyond the old minimum zoom');
  assert.ok(bounds.x * view.zoom + view.x >= parseFloat(padding.left) - pixelTolerance);
  assert.ok((bounds.x + bounds.width) * view.zoom + view.x <= 1400 - parseFloat(padding.right) + pixelTolerance);
  assert.ok(bounds.y * view.zoom + view.y >= parseFloat(padding.top) - pixelTolerance);
  assert.ok((bounds.y + bounds.height) * view.zoom + view.y <= 700 - parseFloat(padding.bottom) + pixelTolerance);
 }
});

test('phone framing reserves the wrapped toolbar without wasting desktop side margins', () => {
 const padding = mapFitPadding(375, 120, 100);
 const bounds = { x: -800, y: -20, width: 24000, height: 1000 };
 const view = getViewportForBounds(bounds, 375, 420, MAP_MIN_ZOOM, 1, padding);
 assert.ok(bounds.x * view.zoom + view.x >= 16 - pixelTolerance);
 assert.ok((bounds.x + bounds.width) * view.zoom + view.x <= 359 + pixelTolerance);
 assert.ok(bounds.y * view.zoom + view.y >= 136 - pixelTolerance);
 assert.ok((bounds.y + bounds.height) * view.zoom + view.y <= 308 + pixelTolerance);
});

test('opening a wide tree keeps a readable scale at the left and permits overflow to the right', () => {
 const bounds = { x: -1200, y: -200, width: 12000, height: 600 };
 const view = initialMapViewport(bounds, 1800, 800, 60, 100);
 assert.equal(view.zoom, MAP_START_ZOOM);
 assert.equal(bounds.x * view.zoom + view.x, 96);
 assert.ok((bounds.x + bounds.width) * view.zoom + view.x > 1800);
 assert.ok(bounds.y * view.zoom + view.y >= 76);
 assert.ok((bounds.y + bounds.height) * view.zoom + view.y <= 688);
 const phone = initialMapViewport(bounds, 390, 620, 120, 100);
 assert.equal(phone.zoom, .62);
 assert.equal(bounds.x * phone.zoom + phone.x, 16);
 const tall = initialMapViewport({ ...bounds, height: 2000 }, 1800, 800, 60, 100);
 assert.ok(tall.zoom < MAP_START_ZOOM);
 assert.ok(bounds.y * tall.zoom + tall.y >= 76);
 assert.ok((bounds.y + 2000) * tall.zoom + tall.y <= 688 + pixelTolerance);
});

test('last pan and zoom are validated and kept separate for each project, branch and orientation', () => {
 const saved = { x: -890.25, y: 124.75, zoom: 1.15 };
 assert.deepEqual(savedMapViewport(JSON.stringify(saved)), saved);
 for (const invalid of [null, '', 'invalid', '{}', 'null', '[]', JSON.stringify({ ...saved, zoom: 0 }), JSON.stringify({ ...saved, zoom: 2 }), JSON.stringify({ ...saved, x: '42' }), JSON.stringify({ ...saved, y: null })]) assert.equal(savedMapViewport(invalid), null);
 assert.equal(new Set([
  mapViewportKey('one', null, 'vertical'), mapViewportKey('two', null, 'vertical'),
  mapViewportKey('one', 'branch', 'vertical'), mapViewportKey('one', null, 'horizontal'),
 ]).size, 4);
});
