import test from 'node:test';
import assert from 'node:assert/strict';
import { addPrintLayout, buildExportDocument, escapeHtml, exportFilename, localizeCssUrls, printScale } from '../src/lib/export-document.ts';
const snapshot = { markup: '<div>Stored task title</div>', css: '.snapshot-0{color:#fff}', width: 2400, height: 1500, background: '#111114', foreground: '#fafafa', secondary: '#aaaab7', line: '#303036' };
const metadata = { projectName: '<img src=x onerror="alert(1)"> & plan', view: 'Gantt chart', detail: 'Weeks · 23 shown rows', generatedAt: '26 October 2026' };

test('SVG marker URLs preserve parentheses and become offline fragment references', () => {
 const base = 'http://127.0.0.1:5274/';
 const id = '#1__color=var(--map-dependency)&type=arrowclosed';
 assert.equal(localizeCssUrls(`url("${base}${id}")`, base), `url("${id}")`);
 assert.equal(localizeCssUrls(`url('${id}')`, base), `url("${id}")`);
 assert.equal(localizeCssUrls('url(#arrow)', base), 'url("#arrow")');
 assert.throws(() => localizeCssUrls('url("https://external.example/image.png")', base));
});

test('project names and snapshot metadata cannot create active HTML in emailed exports', () => {
 const html = buildExportDocument(snapshot, metadata, '');
 assert.ok(html.includes('&lt;img src=x onerror=&quot;alert(1)&quot;&gt; &amp; plan'));
 assert.ok(!html.includes('<img'));
 assert.ok(html.includes("script-src 'none'")); assert.ok(html.includes("connect-src 'none'"));
 assert.ok(!html.includes('<script')); assert.ok(!html.includes('<link'));
 assert.ok(html.includes('Stored task title'));
 assert.equal(escapeHtml("<>&\"'"), '&lt;&gt;&amp;&quot;&#39;');
});

test('both wide and tall charts fit entirely inside landscape A3 without enlarging small charts', () => {
 for (const [width, height] of [[2400,1500],[600,12000],[25000,600]]) {
  const scale = printScale(width,height);
  assert.ok(width * scale <= 400 * 96 / 25.4 + 1e-8);
  assert.ok(height * scale <= 277 * 96 / 25.4 + 1e-8);
 }
 assert.equal(printScale(500,400),1);
 assert.throws(() => printScale(Infinity,100)); assert.throws(() => printScale(100,0));
});

test('print sizing uses the measured entire sheet, while the offline screen snapshot stays full size', () => {
 const html = addPrintLayout(buildExportDocument(snapshot,metadata,''),2448,1700);
 assert.ok(html.includes('width:2400px;height:1500px'));
 assert.ok(html.includes('@media print{html,body'));
 assert.ok(html.includes('position:absolute;left:0;top:0;margin:0;transform:scale('));
 assert.ok(!html.includes('/* EXPORT_PRINT_LAYOUT */'));
});

test('email attachment filenames retain Unicode and cannot introduce directories or control characters', () => {
 assert.equal(exportFilename('Plan / Phase: 2\nTeam','Mind map'),'Plan - Phase- 2-Team - Mind map.html');
 assert.equal(exportFilename('開発 計画','Gantt chart'),'開発 計画 - Gantt chart.html');
});

test('expandable Gantt exports allow only their own offline controller and retain escaped metadata', () => {
 const html = buildExportDocument({ ...snapshot, interaction: 'gantt' }, { ...metadata, projectName: '</script><script>untrusted()</script>' }, '');
 const nonce = html.match(/<script nonce="([a-f0-9]+)">/)[1];
 assert.ok(html.includes(`script-src 'nonce-${nonce}'`));
 assert.equal((html.match(/<script /g) ?? []).length, 1);
 assert.ok(html.includes('&lt;/script&gt;&lt;script&gt;untrusted()&lt;/script&gt;'));
 assert.ok(html.includes("connect-src 'none'"));
 assert.ok(html.includes('class="export-controls" hidden'));
 assert.ok(html.includes('.gantt-row[hidden]{display:none!important}'));
 assert.ok(!html.includes("script-src 'unsafe-inline'"));
});
