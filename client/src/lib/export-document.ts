export type ChartSnapshot = { interaction?: 'gantt'; markup: string; css: string; width: number; height: number; background: string; foreground: string; secondary: string; line: string };
export type ExportMetadata = { projectName: string; view: string; detail: string; generatedAt: string };
export function localizeCssUrls(value: string, baseUrl: string): string {
 const base = new URL(baseUrl);
 // Quoted SVG marker IDs can contain parentheses, including CSS variable names.
 return value.replace(/url\(\s*(?:"([^"]*)"|'([^']*)'|([^)]*))\s*\)/g, (_, double: string, single: string, unquoted: string) => {
  const reference = double ?? single ?? unquoted.trim();
  if (reference.startsWith('#') || reference.startsWith('data:')) return `url(${JSON.stringify(reference)})`;
  const url = new URL(reference, base);
  if (url.origin === base.origin && url.pathname === base.pathname && url.hash) return `url(${JSON.stringify(url.hash)})`;
  throw new Error('The chart contains an external visual resource that cannot be embedded.');
 });
}
export function escapeHtml(value: string): string { return value.replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]!)); }
export function exportFilename(projectName: string, view: string): string {
 const name = projectName.normalize('NFKC').replace(/[<>:"/\\|?*\u0000-\u001f]/g, '-').replace(/\s+/g, ' ').trim().replace(/[. ]+$/g, '');
 return `${name || 'Project'} - ${view}.html`;
}
export function printScale(width: number, height: number): number {
 if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) throw new Error('The chart has no printable dimensions.');
 // Landscape A3, with 10 mm margins. All content is retained on one PDF page.
 return Math.min(1, (420 - 20) * 96 / 25.4 / width, (297 - 20) * 96 / 25.4 / height);
}
// This controller is serialized into offline Gantt exports. Keep it self-contained:
// no application state, network, storage, or user-authored executable content.
export function initializeGanttExport() {
 const rows = [...document.querySelectorAll<HTMLElement>('.gantt-row')];
 const body = document.querySelector<HTMLElement>('.gantt-body')!;
 const grid = document.querySelector<HTMLElement>('.gantt-grid')!;
 const art = document.querySelector<HTMLElement>('.export-art')!;
 const sheet = document.querySelector<HTMLElement>('.export-sheet')!;
 const controls = document.querySelector<HTMLElement>('.export-controls')!;
 const buttons = rows.flatMap(row => { const button = row.querySelector<HTMLButtonElement>('.gantt-collapse[aria-expanded]'); return button ? [{ row, button }] : []; });
 const collapsed = new Set(rows.filter(row => row.dataset.exportCollapsed === 'true').map(row => row.dataset.taskId!));
 function printLayout() {
  const width = sheet.offsetWidth, height = sheet.scrollHeight;
  const scale = Math.min(1, 400 * 96 / 25.4 / width, 277 * 96 / 25.4 / height);
  const style = document.documentElement.style;
  style.setProperty('--export-print-width', `${width}px`); style.setProperty('--export-print-height', `${height}px`); style.setProperty('--export-print-scale', String(scale));
 }
 function update() {
  const ancestors: { depth: number; hidden: boolean }[] = [];
  const visible = new Map<string, { index: number; height: number; top: number }>();
  let height = 0;
  for (const row of rows) {
   const depth = Number(row.dataset.depth);
   while (ancestors.length && ancestors[ancestors.length - 1].depth >= depth) ancestors.pop();
   const hidden = ancestors.some(parent => parent.hidden);
   row.hidden = hidden;
   if (!hidden) { const rowHeight = row.offsetHeight; visible.set(row.dataset.taskId!, { index: visible.size, height: rowHeight, top: height }); height += rowHeight; }
   ancestors.push({ depth, hidden: hidden || collapsed.has(row.dataset.taskId!) });
  }
  for (const { row, button } of buttons) {
   const expanded = !collapsed.has(row.dataset.taskId!);
   button.setAttribute('aria-expanded', String(expanded));
   button.setAttribute('aria-label', `${expanded ? 'Collapse' : 'Expand'} ${row.querySelector('.gantt-task-title')?.getAttribute('title') ?? 'task'}`);
   button.textContent = expanded ? '⌄' : '›';
  }
  const allButton = controls.querySelector<HTMLButtonElement>('button')!;
  allButton.textContent = collapsed.size ? 'Expand all tasks' : 'Collapse all tasks';
  allButton.setAttribute('aria-label', allButton.textContent);
  document.querySelector('.gantt-list-heading > span:last-child')!.textContent = `${visible.size} shown`;
  controls.querySelector('[role="status"]')!.textContent = `${visible.size} of ${rows.length} tasks shown`;
  body.style.height = `${height}px`; grid.style.height = 'auto'; art.style.height = 'auto';
  const today = body.querySelector<HTMLElement>('.gantt-today-line'); if (today) today.style.height = `${height}px`;
  const svg = body.querySelector<SVGSVGElement>('.gantt-dependencies');
  if (svg) {
   svg.style.height = `${height}px`; svg.setAttribute('height', String(height));
   let count = 0;
   for (const path of svg.querySelectorAll<SVGPathElement>('path[data-from]')) {
    const from = visible.get(path.dataset.from!), to = visible.get(path.dataset.to!);
    path.style.display = from && to ? '' : 'none';
    if (!from || !to) continue;
    count++;
    const x1 = Number(path.dataset.x1), x2 = Number(path.dataset.x2);
    const y1 = from.top + from.height / 2, y2 = to.top + to.height / 2;
    const mid = x2 > x1 + 24 ? (x1 + x2) / 2 : x1 + 16;
    const between = from.index < to.index ? from.top + from.height : from.top;
    path.setAttribute('d', x2 > x1 + 24 ? `M${x1},${y1} H${mid} V${y2} H${x2 - 3}` : `M${x1},${y1} H${mid} V${between} H${x2 - 16} V${y2} H${x2 - 3}`);
   }
   svg.setAttribute('aria-label', `${count} visible task dependencies`);
  }
  printLayout();
 }
 for (const { row, button } of buttons) button.addEventListener('click', () => {
  const id = row.dataset.taskId!; if (collapsed.has(id)) collapsed.delete(id); else collapsed.add(id); update();
 });
 controls.querySelector('button')!.addEventListener('click', () => {
  if (collapsed.size) collapsed.clear(); else for (const { row } of buttons) collapsed.add(row.dataset.taskId!);
  update();
 });
 controls.hidden = false; update();
 document.fonts.ready.then(printLayout);
 window.addEventListener('beforeprint', printLayout);
}
export function buildExportDocument(snapshot: ChartSnapshot, metadata: ExportMetadata, fontCss: string): string {
 printScale(snapshot.width, snapshot.height);
 const interactive = snapshot.interaction === 'gantt';
 const nonce = interactive ? Array.from(crypto.getRandomValues(new Uint8Array(16)), byte => byte.toString(16).padStart(2, '0')).join('') : '';
 const scriptPolicy = interactive ? `'nonce-${nonce}'` : "'none'";
 const sheetWidth = Math.ceil(snapshot.width) + 48;
 const css = `${fontCss}\n${snapshot.css}`.replace(/</g, '\\3c ');
 return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src ${scriptPolicy}; connect-src 'none'; style-src 'unsafe-inline'; font-src data:; img-src data:; base-uri 'none'; form-action 'none'"><title>${escapeHtml(metadata.projectName)} - ${escapeHtml(metadata.view)}</title><style>
${css}
*{box-sizing:border-box}html,body{margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:${snapshot.background};color:${snapshot.foreground};-webkit-print-color-adjust:exact;print-color-adjust:exact}body{overflow:auto}.export-sheet{width:${sheetWidth}px;margin:24px;padding:24px;background:${snapshot.background}}.export-heading{margin:0 0 20px;border-bottom:1px solid ${snapshot.line};padding-bottom:18px}.export-heading h1{font-size:28px;font-weight:600;line-height:1.35;margin:0 0 8px;overflow-wrap:anywhere}.export-heading p{font-size:14px;line-height:1.6;margin:4px 0;color:${snapshot.secondary};overflow-wrap:anywhere}.export-label{font-size:12px;letter-spacing:.06em;text-transform:uppercase;font-weight:600;color:${snapshot.secondary};margin-bottom:8px}.export-art{position:relative;width:${snapshot.width}px;height:${snapshot.height}px;overflow:hidden;isolation:isolate}.export-art *{pointer-events:none!important;cursor:default!important;animation:none!important;transition:none!important}.export-art button{outline:none!important}
.export-controls{display:flex;align-items:center;gap:16px;margin:0 0 16px;font-size:14px}.export-controls[hidden]{display:none}.export-controls button{margin-left:auto;flex-shrink:0;white-space:nowrap;font:inherit;background:${snapshot.background};color:${snapshot.foreground};padding:8px 14px;border:1px solid ${snapshot.line};border-radius:7px;cursor:pointer}.export-controls [role=status]{color:${snapshot.secondary}}
${interactive ? '.export-art .gantt-collapse[aria-expanded]{pointer-events:auto!important;cursor:pointer!important;font-size:22px}.export-art .gantt-collapse[aria-expanded]:focus-visible,.export-controls button:focus-visible{outline:2px solid currentColor!important;outline-offset:2px}.export-art .gantt-row[hidden]{display:none!important}' : ''}.export-note{font-size:12px;color:${snapshot.secondary};margin-top:16px;line-height:1.5}
@page{size:A3 landscape;margin:10mm}/* EXPORT_PRINT_LAYOUT */
</style></head><body><main class="export-sheet"><header class="export-heading"><div class="export-label">${escapeHtml(metadata.view)} · Static snapshot</div><h1>${escapeHtml(metadata.projectName)}</h1><p>${escapeHtml(metadata.detail)}</p><p>Exported ${escapeHtml(metadata.generatedAt)}</p></header>${interactive ? '<div class="export-controls" hidden><span role="status" aria-live="polite"></span><span>Use the arrows beside tasks to expand or collapse a branch.</span><button type="button">Collapse all tasks</button></div>' : ''}<div class="export-art">${snapshot.markup}</div><footer class="export-note">${interactive ? 'Snapshot of the current filters. Expand or collapse tasks to explore the hierarchy.' : 'Snapshot of the current filters and expanded branches.'} This document works offline and does not update when the project changes.</footer></main>${interactive ? `<script nonce="${nonce}">(${initializeGanttExport.toString()})();</script>` : ''}</body></html>`;
}
export function addPrintLayout(html: string, sheetWidth: number, sheetHeight: number): string {
 const scale = printScale(sheetWidth, sheetHeight);
 return html.replace('/* EXPORT_PRINT_LAYOUT */', `@media print{html,body{width:calc(var(--export-print-width, ${sheetWidth}px) * var(--export-print-scale, ${scale}));height:calc(var(--export-print-height, ${sheetHeight}px) * var(--export-print-scale, ${scale}));overflow:visible}.export-sheet{position:absolute;left:0;top:0;margin:0;transform:scale(var(--export-print-scale, ${scale}));transform-origin:top left}}`);
}
