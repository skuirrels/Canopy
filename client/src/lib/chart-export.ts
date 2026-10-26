import font400 from '@fontsource/inter/files/inter-latin-400-normal.woff2?url';
import font500 from '@fontsource/inter/files/inter-latin-500-normal.woff2?url';
import font600 from '@fontsource/inter/files/inter-latin-600-normal.woff2?url';
import font700 from '@fontsource/inter/files/inter-latin-700-normal.woff2?url';
import { addPrintLayout, buildExportDocument, localizeCssUrls } from './export-document';
import type { ChartSnapshot, ExportMetadata } from './export-document';

// Freeze visual styles rather than copying application CSS or scripts. Repeated
// styles share a rule, keeping emailed documents small and independent of assets.
const properties = ['display','position','box-sizing','overflow','overflow-x','overflow-y','isolation','width','height','min-width','min-height','max-width','max-height','top','right','bottom','left','padding','margin','gap','row-gap','column-gap','grid-template-columns','grid-template-rows','grid-column','grid-row','grid-auto-flow','flex','flex-direction','flex-wrap','align-items','align-self','justify-content','justify-self','order','font-family','font-size','font-weight','font-style','line-height','letter-spacing','word-spacing','text-align','text-decoration','text-transform','white-space','text-overflow','overflow-wrap','word-break','vertical-align','font-variant-numeric','color','background-color','background-image','background-size','background-position','background-repeat','border-top','border-right','border-bottom','border-left','border-radius','box-shadow','opacity','visibility','z-index','transform','transform-origin','fill','fill-opacity','stroke','stroke-width','stroke-opacity','stroke-dasharray','stroke-linecap','stroke-linejoin','marker-end','marker-start','clip-path','filter','list-style','-webkit-line-clamp','-webkit-box-orient'];
function freezeElement(source: HTMLElement): { clone: HTMLElement; css: string } {
 const clone = source.cloneNode(true) as HTMLElement; const rules = new Map<string, string>();
 const freeze = (original: Element, copy: Element) => {
  const computed = getComputedStyle(original);
  const style = properties.map(property => {
   let value = computed.getPropertyValue(property); if (!value) return '';
   if (property === 'position' && value === 'sticky') value = 'relative';
   return `${property}:${localizeCssUrls(value, document.baseURI)};`;
  }).join('');
  let className = rules.get(style); if (!className) { className = `snapshot-${rules.size}`; rules.set(style, className); }
  copy.removeAttribute('style'); copy.setAttribute('class', `${copy.getAttribute('class') ?? ''} ${className}`.trim());
  for (const attribute of [...copy.attributes]) {
   if (attribute.name.startsWith('on') || ['srcset','autofocus','contenteditable','formaction'].includes(attribute.name)) copy.removeAttribute(attribute.name);
   if (['href','xlink:href','src'].includes(attribute.name) && !attribute.value.startsWith('#') && !attribute.value.startsWith('data:')) copy.removeAttribute(attribute.name);
  }
  if (copy.tagName.toLowerCase() === 'button') { copy.setAttribute('type', 'button'); copy.setAttribute('tabindex', '-1'); copy.setAttribute('aria-disabled', 'true'); }
  const originals = [...original.children], copies = [...copy.children];
  originals.forEach((child, index) => freeze(child, copies[index]));
 };
 freeze(source, clone);
 clone.querySelectorAll('script,iframe,object,embed,link,style').forEach(element => element.remove());
 return { clone, css: [...rules].map(([style, className]) => `.${className}{${style}}`).join('\n') };
}
function colors(): Pick<ChartSnapshot, 'background' | 'foreground' | 'secondary' | 'line'> {
 const style = getComputedStyle(document.documentElement);
 return { background: style.getPropertyValue('--surface-subtle').trim(), foreground: style.getPropertyValue('--heading').trim(), secondary: style.getPropertyValue('--secondary-text').trim(), line: style.getPropertyValue('--line').trim() };
}
export function captureGantt(grid: HTMLElement, collapsed?: ReadonlySet<string>): ChartSnapshot {
 const width = Math.ceil(grid.getBoundingClientRect().width), height = Math.ceil(grid.scrollHeight);
 const { clone, css } = freezeElement(grid);
 clone.style.width = `${width}px`; clone.style.minWidth = '0'; clone.style.height = `${height}px`;
 clone.querySelectorAll('.gantt-resize,.gantt-date-preview').forEach(element => element.remove());
 clone.querySelectorAll('.gantt-unscheduled').forEach(element => { element.textContent = 'Unscheduled'; });
 if (collapsed) {
  clone.querySelectorAll<HTMLElement>('.gantt-row').forEach(row => {
   row.dataset.exportCollapsed = String(collapsed.has(row.dataset.taskId!));
   const button = row.querySelector<HTMLButtonElement>('.gantt-collapse[aria-expanded]');
   if (button) { button.removeAttribute('tabindex'); button.removeAttribute('aria-disabled'); }
  });
 }
 return { markup: clone.outerHTML, css, width, height, ...(collapsed ? { interaction: 'gantt' as const } : {}), ...colors() };
}
export function captureMindMap(root: HTMLElement, nodeBounds: { x: number; y: number; width: number; height: number }): ChartSnapshot {
 let left = nodeBounds.x, top = nodeBounds.y, right = left + nodeBounds.width, bottom = top + nodeBounds.height;
 root.querySelectorAll<SVGGraphicsElement>('.react-flow__edge-path,.react-flow__edge-textwrapper').forEach(element => {
  const box = element.getBBox(); if (!box.width && !box.height) return;
  left = Math.min(left, box.x); top = Math.min(top, box.y); right = Math.max(right, box.x + box.width); bottom = Math.max(bottom, box.y + box.height);
 });
 const padding = 40; const width = Math.ceil(right - left + padding * 2), height = Math.ceil(bottom - top + padding * 2);
 const { clone, css } = freezeElement(root);
 clone.style.width = `${width}px`; clone.style.height = `${height}px`; clone.style.minHeight = '0'; clone.style.position = 'relative';
 clone.style.backgroundImage = `radial-gradient(${getComputedStyle(document.documentElement).getPropertyValue('--map-grid').trim()} 1px, transparent 1px)`;
 clone.style.backgroundSize = '24px 24px';
 clone.querySelectorAll('.react-flow__controls,.react-flow__minimap,.react-flow__attribution,.react-flow__background,.react-flow__panel,[id^="react-flow__node-desc-"],[id^="react-flow__edge-desc-"],[id^="react-flow__aria-live-"]').forEach(element => element.remove());
 clone.querySelectorAll<HTMLElement>('.map-node-buttons>button,.map-root-footer>button,.dependency-handle').forEach(element => { element.style.visibility = 'hidden'; element.replaceChildren(); });
 clone.querySelectorAll<HTMLElement>('.react-flow__renderer,.react-flow__pane,.react-flow__nodes,.react-flow__edges').forEach(element => { element.style.width = `${width}px`; element.style.height = `${height}px`; element.style.overflow = 'visible'; });
 const viewport = clone.querySelector<HTMLElement>('.react-flow__viewport');
 if (!viewport) throw new Error('The mind map is still loading. Try exporting again.');
 viewport.style.transform = `translate(${padding - left}px,${padding - top}px)`; viewport.style.transformOrigin = '0 0'; viewport.style.width = `${width}px`; viewport.style.height = `${height}px`;
 return { markup: clone.outerHTML, css, width, height, ...colors() };
}
let fonts: Promise<string> | undefined;
function embeddedFonts(): Promise<string> {
 if (!fonts) fonts = Promise.all(([font400,font500,font600,font700]).map(async (url, index) => {
  const response = await fetch(url); if (!response.ok) throw new Error('The export font could not be loaded. Try again.');
  const bytes = new Uint8Array(await response.arrayBuffer()); let binary = '';
  for (let start = 0; start < bytes.length; start += 8192) binary += String.fromCharCode(...bytes.subarray(start, start + 8192));
  return `@font-face{font-family:Inter;font-style:normal;font-weight:${[400,500,600,700][index]};src:url(data:font/woff2;base64,${btoa(binary)}) format('woff2');font-display:block}`;
 })).then(values => values.join('\n')).catch(error => { fonts = undefined; throw error; });
 return fonts;
}
export async function prepareChartDocument(snapshot: ChartSnapshot, metadata: ExportMetadata): Promise<string> {
 const html = buildExportDocument(snapshot, metadata, await embeddedFonts());
 const frame = document.createElement('iframe'); frame.title = 'Preparing chart export'; frame.setAttribute('aria-hidden','true'); frame.tabIndex = -1; frame.sandbox.add('allow-same-origin');
 frame.style.cssText = `position:fixed;left:-100000px;top:0;width:${snapshot.width + 96}px;height:100px;border:0;visibility:hidden;pointer-events:none`;
 try {
  await new Promise<void>((resolve, reject) => { frame.onload = () => resolve(); frame.onerror = () => reject(new Error('The export preview could not be prepared.')); frame.srcdoc = html; document.body.append(frame); });
  const doc = frame.contentDocument; if (!doc) throw new Error('The export preview could not be measured.');
  await doc.fonts.ready;
  const sheet = doc.querySelector<HTMLElement>('.export-sheet'); if (!sheet) throw new Error('The export preview is incomplete.');
  return addPrintLayout(html, Math.ceil(sheet.getBoundingClientRect().width), Math.ceil(sheet.getBoundingClientRect().height));
 } finally { frame.remove(); }
}
export function downloadChartDocument(html: string, filename: string): void {
 const url = URL.createObjectURL(new Blob([html], { type: 'text/html;charset=utf-8' }));
 const link = document.createElement('a'); link.href = url; link.download = filename; document.body.append(link); link.click(); link.remove();
 setTimeout(() => URL.revokeObjectURL(url), 30000);
}
