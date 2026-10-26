import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { initializeGanttExport } from '../src/lib/export-document.ts';

// A small DOM adapter lets the exact serialized offline script run without
// importing the application, or giving an exported page network/storage access.
function exportedTimeline() {
 function element(attributes = {}) {
  const listeners = new Map();
  return { attributes, style: { setProperty(key, value) { this[key] = value; } }, dataset: {}, hidden: false, textContent: '',
   setAttribute(key, value) { attributes[key] = value; }, getAttribute(key) { return attributes[key] ?? null; },
   addEventListener(key, callback) { listeners.set(key, callback); }, click() { listeners.get('click')(); },
   querySelector() { return null; } };
 }
 const rows = [ ['component', 0, true], ['branch', 1, true], ['leaf', 2, false], ['sibling', 1, false], ['other', 0, false] ].map(([id, depth, parent]) => {
  const row = element(), title = element({ title: id });
  row.dataset = { taskId: id, depth: String(depth), exportCollapsed: String(id === 'component') };
  Object.defineProperty(row, 'offsetHeight', { get: () => row.hidden ? 0 : 60 });
  row.button = parent ? element({ 'aria-expanded': 'true' }) : null;
  row.querySelector = selector => selector === '.gantt-task-title' ? title : row.button;
  return row;
 });
 const paths = [ ['leaf', 'other', 200, 400], ['component', 'other', 500, 100] ].map(([from, to, x1, x2]) => {
  const path = element(); path.dataset = { from, to, x1, x2 }; return path;
 });
 const svg = element(); svg.querySelectorAll = () => paths;
 const today = element(), body = element(); body.querySelector = selector => selector === '.gantt-dependencies' ? svg : today;
 const all = element(), status = element(), controls = element(); controls.querySelector = selector => selector === 'button' ? all : status;
 const sheet = element(); sheet.offsetWidth = 2448;
 Object.defineProperty(sheet, 'scrollHeight', { get: () => 200 + rows.filter(row => !row.hidden).length * 60 });
 const heading = element(), grid = element(), art = element(), root = element();
 const elements = { '.gantt-body': body, '.gantt-grid': grid, '.export-art': art, '.export-sheet': sheet, '.export-controls': controls, '.gantt-list-heading > span:last-child': heading };
 const beforePrint = new Map();
 const document = { querySelectorAll: () => rows, querySelector: selector => elements[selector], documentElement: root, fonts: { ready: Promise.resolve() } };
 vm.runInNewContext(`(${initializeGanttExport.toString()})();`, { document, window: { addEventListener: (key, callback) => beforePrint.set(key, callback) } });
 return { rows, paths, svg, all, status, controls, body, today, grid, art, root, sheet, beforePrint, visible: () => rows.filter(row => !row.hidden).map(row => row.dataset.taskId) };
}

test('offline expansion restores hidden descendants and preserves nested collapse choices', () => {
 const view = exportedTimeline();
 assert.deepEqual(view.visible(), ['component', 'other']);
 assert.equal(view.rows[0].button.getAttribute('aria-expanded'), 'false');
 assert.equal(view.all.textContent, 'Expand all tasks');
 view.all.click();
 assert.deepEqual(view.visible(), ['component', 'branch', 'leaf', 'sibling', 'other']);
 view.rows[1].button.click();
 assert.deepEqual(view.visible(), ['component', 'branch', 'sibling', 'other']);
 view.rows[0].button.click(); view.rows[0].button.click();
 assert.deepEqual(view.visible(), ['component', 'branch', 'sibling', 'other']);
 assert.equal(view.rows[1].button.getAttribute('aria-expanded'), 'false');
 view.all.click(); view.all.click();
 assert.deepEqual(view.visible(), ['component', 'other']);
 assert.equal(view.status.textContent, '2 of 5 tasks shown');
});

test('offline dependency arrows follow visible rows, hide missing endpoints and resize the chart', () => {
 const view = exportedTimeline();
 assert.equal(view.paths[0].style.display, 'none');
 assert.equal(view.paths[1].getAttribute('d'), 'M500,30 H516 V60 H84 V90 H97');
 assert.equal(view.svg.getAttribute('aria-label'), '1 visible task dependencies');
 view.all.click();
 assert.equal(view.paths[0].style.display, '');
 assert.equal(view.paths[0].getAttribute('d'), 'M200,150 H300 V270 H397');
 assert.equal(view.paths[1].getAttribute('d'), 'M500,30 H516 V60 H84 V270 H97');
 assert.equal(view.body.style.height, '300px');
 assert.equal(view.today.style.height, '300px');
 assert.equal(view.svg.style.height, '300px');
 assert.equal(view.grid.style.height, 'auto'); assert.equal(view.art.style.height, 'auto');
});

test('offline printing recalculates the whole visible sheet after expansion', () => {
 const view = exportedTimeline();
 assert.equal(view.root.style['--export-print-height'], '320px');
 view.all.click();
 assert.equal(view.root.style['--export-print-height'], '500px');
 view.sheet.offsetWidth = 3000;
 view.beforePrint.get('beforeprint')();
 const scale = Number(view.root.style['--export-print-scale']);
 assert.ok(3000 * scale <= 400 * 96 / 25.4);
 assert.ok(500 * scale <= 277 * 96 / 25.4);
 assert.equal(view.root.style['--export-print-width'], '3000px');
});
