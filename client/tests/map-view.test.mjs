import test from 'node:test';
import assert from 'node:assert/strict';
import { getViewportForBounds } from '@xyflow/react';
import { initialMapDirection, mapDirectionKey, mapFitPadding, MAP_MIN_ZOOM } from '../src/lib/map-view.ts';

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
