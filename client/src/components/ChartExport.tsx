import { useRef, useState } from 'react';
import * as Dropdown from '@radix-ui/react-dropdown-menu';
import { ChevronDown, Download, Printer, FileCode2, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import type { ChartSnapshot } from '../lib/export-document';
import { exportFilename } from '../lib/export-document';
import { downloadChartDocument, prepareChartDocument } from '../lib/chart-export';
import { Button } from './ui/button';
import { Dialog } from './ui/dialog';

export function ChartExport({ projectName, view, detail, capture, disabled = false }: { projectName: string; view: string; detail: string; capture: (mode: 'print' | 'html') => ChartSnapshot; disabled?: boolean }) {
 const [busy, setBusy] = useState(false); const [printHtml, setPrintHtml] = useState<string | null>(null); const [printReady, setPrintReady] = useState(false); const printFrame = useRef<HTMLIFrameElement>(null);
 async function run(mode: 'print' | 'html') {
  setBusy(true);
  try {
   await document.fonts.ready;
   const html = await prepareChartDocument(capture(mode), { projectName, view, detail, generatedAt: new Date().toLocaleString() });
   if (mode === 'print') { setPrintReady(false); setPrintHtml(html); }
   else { downloadChartDocument(html, exportFilename(projectName, view)); toast.success('Static HTML downloaded. Attach the file to your email.'); }
  } catch (error) { toast.error((error as Error).message || 'The chart could not be exported.'); }
  finally { setBusy(false); }
 }
 return <><Dropdown.Root><Dropdown.Trigger asChild><Button variant="secondary" size="sm" disabled={disabled || busy} aria-label={`Export ${view}`} title="Export the full chart with the current filters and expanded branches">{busy ? <Loader2 size={14} className="spin"/> : <Download size={14}/>}Export<ChevronDown size={13}/></Button></Dropdown.Trigger><Dropdown.Portal><Dropdown.Content className="dropdown chart-export-menu" align="end"><Dropdown.Item onSelect={() => { void run('print'); }}><Printer size={15}/><span>Print / Save PDF<small>Whole chart · landscape A3</small></span></Dropdown.Item><Dropdown.Item onSelect={() => { void run('html'); }}><FileCode2 size={15}/><span>Download static HTML<small>{view === 'Gantt chart' ? 'Offline · expandable tasks' : 'Self-contained email attachment'}</small></span></Dropdown.Item></Dropdown.Content></Dropdown.Portal></Dropdown.Root><Dialog open={printHtml !== null} onOpenChange={open => { if (!open) setPrintHtml(null); }} title={`Print ${view}`} description="The complete snapshot is fitted to landscape A3. Choose Print / Save PDF, then select Save as PDF in your browser's print dialog." className="chart-export-dialog"><iframe ref={printFrame} className="chart-export-frame" title={`${view} print preview`} srcDoc={printHtml ?? undefined} sandbox="allow-same-origin allow-modals" onLoad={() => { const doc = printFrame.current?.contentDocument; if (doc) void doc.fonts.ready.then(() => setPrintReady(true)); }}/><div className="dialog-footer"><Button variant="secondary" disabled={busy} onClick={() => { void run('html'); }}><FileCode2 size={15}/>Download HTML</Button><Button disabled={!printReady} onClick={() => { const frame = printFrame.current?.contentWindow; if (!frame) return; frame.focus(); frame.print(); }}><Printer size={15}/>Print / Save PDF</Button></div></Dialog></>;
}
