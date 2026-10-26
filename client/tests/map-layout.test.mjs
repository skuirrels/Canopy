import test from 'node:test';
import assert from 'node:assert/strict';
import { displayMapPoint, storedMapPoint, MAP_NODE_WIDTH, MAP_NODE_HEIGHT } from '../src/lib/map-layout.ts';

const near = (actual, expected) => assert.ok(Math.abs(actual - expected) < 1e-8, `${actual} differs from ${expected}`);

test('horizontal view retains saved positions and neither orientation changes its input', () => {
 const point = { x: 410, y: -216 };
 assert.deepEqual(displayMapPoint(point, 'horizontal'), point);
 displayMapPoint(point, 'vertical');
 assert.deepEqual(point, { x: 410, y: -216 });
});

test('vertical projection puts descendants below their parent and siblings side by side without overlap', () => {
 const parent = displayMapPoint({ x: 0, y: 0 }, 'vertical');
 const child = displayMapPoint({ x: MAP_NODE_WIDTH + 90, y: 0 }, 'vertical');
 const sibling = displayMapPoint({ x: MAP_NODE_WIDTH + 90, y: MAP_NODE_HEIGHT + 40 }, 'vertical');
 assert.equal(parent.x, child.x);
 assert.ok(child.y > parent.y + MAP_NODE_HEIGHT);
 assert.equal(child.y, sibling.y);
 assert.ok(sibling.x > child.x + MAP_NODE_WIDTH);
});

test('vertical drag coordinates round-trip through the shared saved layout, including negative positions', () => {
 for (const point of [{ x: -425.5, y: 113.25 }, { x: 0, y: 0 }, { x: 1250, y: -982 }]) {
  const displayed = displayMapPoint(point, 'vertical');
  const dragged = { x: displayed.x + 120, y: displayed.y - 48 };
  const saved = storedMapPoint(dragged, 'vertical');
  const reloaded = displayMapPoint(saved, 'vertical');
  near(reloaded.x, dragged.x); near(reloaded.y, dragged.y);
  near(saved.x, point.x - 48 * MAP_NODE_WIDTH / MAP_NODE_HEIGHT);
  near(saved.y, point.y + 120 * MAP_NODE_HEIGHT / MAP_NODE_WIDTH);
 }
});
