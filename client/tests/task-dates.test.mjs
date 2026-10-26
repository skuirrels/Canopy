import test from 'node:test';
import assert from 'node:assert/strict';
import { taskDateShortcut, dateWithinBounds } from '../src/lib/task-dates.ts';

test('Today and Tomorrow use the browser local calendar, including dates different from UTC', () => {
 const previous = process.env.TZ;
 try {
  process.env.TZ = 'America/Los_Angeles';
  const west = new Date('2026-10-27T01:30:00Z');
  assert.equal(taskDateShortcut(0, west), '2026-10-26');
  assert.equal(taskDateShortcut(1, west), '2026-10-27');
  process.env.TZ = 'Pacific/Auckland';
  const east = new Date('2026-12-31T12:30:00Z');
  assert.equal(taskDateShortcut(0, east), '2027-01-01');
  assert.equal(taskDateShortcut(1, east), '2027-01-02');
 } finally { if (previous === undefined) delete process.env.TZ; else process.env.TZ = previous; }
});
test('Tomorrow advances one calendar date across daylight saving, leap day and year boundaries', () => {
 const previous = process.env.TZ;
 try {
  process.env.TZ = 'Europe/London';
  for (const [date, expected] of [['2026-03-28T23:30:00', '2026-03-29'], ['2026-10-24T23:30:00', '2026-10-25'], ['2028-02-28T23:30:00', '2028-02-29'], ['2026-12-31T23:30:00', '2027-01-01']]) assert.equal(taskDateShortcut(1, new Date(date)), expected);
 } finally { if (previous === undefined) delete process.env.TZ; else process.env.TZ = previous; }
});
test('quick options respect inclusive start/due constraints without changing the other date', () => {
 assert.equal(dateWithinBounds('2026-10-26'), true);
 assert.equal(dateWithinBounds('2026-10-26', '2026-10-26', '2026-10-26'), true);
 assert.equal(dateWithinBounds('2026-10-26', '2026-10-27'), false);
 assert.equal(dateWithinBounds('2026-10-27', undefined, '2026-10-26'), false);
 assert.equal(dateWithinBounds('2026-10-27', '2026-10-26'), true);
});
