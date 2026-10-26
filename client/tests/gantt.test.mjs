import test from 'node:test';
import assert from 'node:assert/strict';
import { calendarSegments, changeDates, dateDay, dayDate, dependencyHighlights, ganttRows, schedule, timelineRange } from '../src/lib/gantt.ts';
const task = (id, parentId = null, position = 0, dates = {}) => ({ id, parentId, position, title: id, createdAt: '2026-01-01T00:00:00Z', startDate: null, dueDate: null, ...dates });

test('moving a task across the London DST boundary preserves inclusive calendar duration', () => {
 const original = { startDate: '2026-10-24', dueDate: '2026-10-26' };
 const moved = changeDates(original, 'move', 2);
 assert.deepEqual(moved, { startDate: '2026-10-26', dueDate: '2026-10-28' });
 assert.equal(schedule(moved).end - schedule(moved).start + 1, 3);
 assert.deepEqual(original, { startDate: '2026-10-24', dueDate: '2026-10-26' });
});
test('date changes retain missing dates and never schedule an undated task', () => {
 assert.deepEqual(changeDates({ startDate: null, dueDate: '2026-11-01' }, 'move', -2), { startDate: null, dueDate: '2026-10-30' });
 assert.deepEqual(changeDates({ startDate: '2026-11-01', dueDate: null }, 'move', 2), { startDate: '2026-11-03', dueDate: null });
 assert.deepEqual(changeDates({ startDate: null, dueDate: null }, 'move', 7), { startDate: null, dueDate: null });
 assert.equal(schedule({ startDate: null, dueDate: null }), null);
 assert.equal(schedule({ startDate: null, dueDate: '2026-11-01' }).kind, 'due');
 assert.equal(schedule({ startDate: '2026-11-01', dueDate: null }).kind, 'start');
});
test('resize cannot reverse a task interval; a single calendar day remains valid', () => {
 const original = { startDate: '2026-11-01', dueDate: '2026-11-03' };
 assert.deepEqual(changeDates(original, 'start', 10), { startDate: '2026-11-03', dueDate: '2026-11-03' });
 assert.deepEqual(changeDates(original, 'end', -10), { startDate: '2026-11-01', dueDate: '2026-11-01' });
});
test('calendar arithmetic handles leap days, year boundaries, and negative movement', () => {
 assert.equal(dayDate(dateDay('2028-02-28') + 1), '2028-02-29');
 assert.equal(dayDate(dateDay('2028-02-28') + 2), '2028-03-01');
 assert.equal(dayDate(dateDay('2027-01-01') - 1), '2026-12-31');
});
test('filtered nested tasks retain their ancestor path and stored sibling ordering', () => {
 const parent = task('parent', null, 2), child = task('child', 'parent', 1), grandchild = task('grandchild', 'child');
 const other = task('other', null, 1);
 const rows = ganttRows([parent, child, grandchild, other], [grandchild], new Set(), null);
 assert.deepEqual(rows.map(r => [r.item.id, r.depth, r.context]), [['parent', 0, true], ['child', 1, true], ['grandchild', 2, false]]);
 assert.deepEqual(ganttRows([parent, child, grandchild, other], [parent, child, grandchild, other], new Set(), null).map(r => r.item.id), ['other', 'parent', 'child', 'grandchild']);
});
test('collapsing a branch hides its descendants without losing data; focusing excludes the outer branch', () => {
 const parent = task('parent'), child = task('child', 'parent'), grandchild = task('grandchild', 'child'); const items = [parent, child, grandchild];
 assert.deepEqual(ganttRows(items, items, new Set(['parent']), null).map(r => r.item.id), ['parent']);
 assert.deepEqual(ganttRows(items, [grandchild], new Set(), 'parent').map(r => [r.item.id, r.depth]), [['child', 0], ['grandchild', 1]]);
 assert.equal(items.length, 3);
});
test('an undated project gets a calendar viewport without adding dates to its tasks', () => {
 const items = [task('undated')], today = dateDay('2026-10-25'); const range = timelineRange(items, today);
 assert.ok(range.start <= today && range.end >= today);
 assert.deepEqual(items[0].startDate, null); assert.deepEqual(items[0].dueDate, null);
 const future = timelineRange([task('future', null, 0, { dueDate: '2028-01-01' })], today, today);
 assert.ok(future.start <= today && future.end >= dateDay('2028-01-01'));
});
test('calendar headers cover every day exactly once across a year boundary', () => {
 const start = dateDay('2026-12-28'), end = dateDay('2027-01-10');
 const months = calendarSegments(start, end, 'month');
 assert.deepEqual(months.map(m => m.days), [4, 10]);
 assert.equal(calendarSegments(start, end, 'week').reduce((sum, part) => sum + part.days, 0), 14);
 assert.equal(calendarSegments(start, end, 'day').length, 14);
});
test('dragging stays inside the public DateOnly calendar range without distorting duration', () => {
 assert.deepEqual(changeDates({ startDate: '9999-12-30', dueDate: '9999-12-31' }, 'move', 2), { startDate: '9999-12-30', dueDate: '9999-12-31' });
 assert.deepEqual(changeDates({ startDate: '0001-01-01', dueDate: '0001-01-02' }, 'move', -7), { startDate: '0001-01-01', dueDate: '0001-01-02' });
});

test('today can be centered in a wide timeline with past, future or unscheduled tasks', () => {
 const today = dateDay('2026-10-26');
 const visibleDays = 180;
 for (const items of [
  [task('past', null, 0, { startDate: '2026-01-01', dueDate: '2026-01-10' })],
  [task('future', null, 0, { startDate: '2027-02-01', dueDate: '2027-02-10' })],
  [task('unscheduled')],
 ]) {
  const range = timelineRange(items, today, today, visibleDays);
  assert.ok(range.start <= today - visibleDays / 2, 'enough history to center today');
  assert.ok(range.end >= today + visibleDays / 2, 'enough future to center today');
  for (const item of items) {
   const dates = schedule(item);
   if (dates) assert.ok(range.start <= dates.start && range.end >= dates.end, 'all scheduled dates remain reachable');
  }
 }
});

test('task selection highlights direct incoming and outgoing dependencies, not a transitive chain or hierarchy', () => {
 const links = [
  { id: 'incoming', itemId: 'selected', dependsOnId: 'prerequisite' },
  { id: 'outgoing', itemId: 'dependent', dependsOnId: 'selected' },
  { id: 'transitive', itemId: 'later', dependsOnId: 'dependent' },
  { id: 'unrelated', itemId: 'other', dependsOnId: 'third' },
 ];
 const before = structuredClone(links);
 const highlight = dependencyHighlights(links, 'selected');
 assert.deepEqual([...highlight.links], ['incoming', 'outgoing']);
 assert.deepEqual([...highlight.tasks], ['prerequisite', 'dependent']);
 assert.deepEqual(links, before);
 for (const selected of [null, 'unconnected']) {
  assert.equal(dependencyHighlights(links, selected).links.size, 0);
  assert.equal(dependencyHighlights(links, selected).tasks.size, 0);
 }
 assert.deepEqual([...dependencyHighlights([links[1]], 'selected').tasks], ['dependent'], 'hidden or unscheduled edges are omitted by the visible-edge projection');
 assert.deepEqual([...dependencyHighlights(links, 'prerequisite').links], ['incoming'], 'changing selection replaces the previous highlight');
});
