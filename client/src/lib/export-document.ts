export type ChartSnapshot = { markup: string; css: string; width: number; height: number; background: string; foreground: string; secondary: string; line: string };
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
export function buildExportDocument(snapshot: ChartSnapshot, metadata: ExportMetadata, fontCss: string): string {
 printScale(snapshot.width, snapshot.height);
 const sheetWidth = Math.ceil(snapshot.width) + 48;
 const css = `${fontCss}\n${snapshot.css}`.replace(/</g, '\\3c ');
 return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'none'; connect-src 'none'; style-src 'unsafe-inline'; font-src data:; img-src data:; base-uri 'none'; form-action 'none'"><title>${escapeHtml(metadata.projectName)} - ${escapeHtml(metadata.view)}</title><style>
${css}
*{box-sizing:border-box}html,body{margin:0;padding:0;font-family:Inter,Arial,sans-serif;background:${snapshot.background};color:${snapshot.foreground};-webkit-print-color-adjust:exact;print-color-adjust:exact}body{overflow:auto}.export-sheet{width:${sheetWidth}px;margin:24px;padding:24px;background:${snapshot.background}}.export-heading{margin:0 0 20px;border-bottom:1px solid ${snapshot.line};padding-bottom:18px}.export-heading h1{font-size:28px;font-weight:600;line-height:1.35;margin:0 0 8px;overflow-wrap:anywhere}.export-heading p{font-size:14px;line-height:1.6;margin:4px 0;color:${snapshot.secondary};overflow-wrap:anywhere}.export-label{font-size:12px;letter-spacing:.06em;text-transform:uppercase;font-weight:600;color:${snapshot.secondary};margin-bottom:8px}.export-art{position:relative;width:${snapshot.width}px;height:${snapshot.height}px;overflow:hidden;isolation:isolate}.export-art *{pointer-events:none!important;cursor:default!important;animation:none!important;transition:none!important}.export-art button{outline:none!important}.export-note{font-size:12px;color:${snapshot.secondary};margin-top:16px;line-height:1.5}
@page{size:A3 landscape;margin:10mm}/* EXPORT_PRINT_LAYOUT */
</style></head><body><main class="export-sheet"><header class="export-heading"><div class="export-label">${escapeHtml(metadata.view)} · Static snapshot</div><h1>${escapeHtml(metadata.projectName)}</h1><p>${escapeHtml(metadata.detail)}</p><p>Exported ${escapeHtml(metadata.generatedAt)}</p></header><div class="export-art">${snapshot.markup}</div><footer class="export-note">Snapshot of the current filters and expanded branches. This document works offline and does not update when the project changes.</footer></main></body></html>`;
}
export function addPrintLayout(html: string, sheetWidth: number, sheetHeight: number): string {
 const scale = printScale(sheetWidth, sheetHeight);
 return html.replace('/* EXPORT_PRINT_LAYOUT */', `@media print{html,body{width:${sheetWidth * scale}px;height:${sheetHeight * scale}px;overflow:visible}.export-sheet{position:absolute;left:0;top:0;margin:0;transform:scale(${scale});transform-origin:top left}}`);
}
