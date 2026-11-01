import { useState } from 'react';
import { Copy, Link2, RefreshCw, Save, Unlink, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import type { Item } from '../lib/types';
import { useWorkspace } from '../lib/workspace';
import { parseMarkdown, toMarkdown } from '../lib/markdown';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';
import './linked-spec-file.css';

type FilePreview = { path: string; markdown: string; hash: string };
async function readFile(path: string, method = 'GET', body?: unknown): Promise<FilePreview> {
 const response = await fetch('/api/spec-files' + path, { method, headers: { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
 const result = await response.json();
 if (!response.ok) throw new Error(result.title || 'Cannot read the Markdown file.');
 return result;
}

export function LinkedSpecFile({ item, beforeChange, getSpec }: { item: Item; beforeChange: () => Promise<void>; getSpec: () => string }) {
 const { data, mutate } = useWorkspace();
 const link = data.specFiles?.find(file => file.id === item.id);
 const [open, setOpen] = useState(false); const [path, setPath] = useState('');
 const [preview, setPreview] = useState<FilePreview | null>(null); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
 const [action, setAction] = useState<'write' | 'unlink' | null>(null);
 const [outgoing, setOutgoing] = useState('');
 async function run(operation: () => Promise<void>) {
  setBusy(true); setError('');
  try { await operation(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
 }
 async function previewFile(refresh = false) {
  await run(async () => {
   const file = refresh ? await readFile(`/${item.id}`) : await readFile('/preview', 'POST', { path });
   parseMarkdown(file.markdown); setPreview(file); setPath(file.path);
  });
 }
 function startLink() { setPath(link?.path ?? ''); setPreview(null); setError(''); setOpen(true); }
 return <div className="linked-spec-file">
  {link ? <>
   <div className="linked-spec-heading"><Link2 size={14}/><strong>Linked Markdown file</strong><button className="icon-button" aria-label="Copy linked Markdown path" data-tooltip="Copy path for Codex / Claude / Gemini" onClick={() => { void navigator.clipboard.writeText(link.path).then(() => toast.success('File path copied')).catch(() => toast.error('Could not copy the path. Select and copy it below.')); }}><Copy size={13}/></button></div>
   <code className="linked-spec-path" title={link.path}>{link.path}</code>
   <p>Share this path with Codex, Claude or Gemini. Refresh to load their edits; save to write yours.</p>
   <div className="linked-spec-actions"><button className="text-button" disabled={busy} onClick={() => { setPreview(null); setPath(link.path); setError(''); setOpen(true); void previewFile(true); }}><RefreshCw size={13}/>Refresh from file</button><button className="text-button" disabled={busy} onClick={() => { setError(''); void run(async () => { await beforeChange(); setOutgoing(toMarkdown(JSON.parse(getSpec()))); setAction('write'); }); }}><Save size={13}/>Save to file</button><button className="text-button" disabled={busy} onClick={() => { setError(''); setAction('unlink'); }}><Unlink size={13}/>Unlink</button></div>
  </> : <button className="text-button" onClick={startLink}><Link2 size={13}/>Link Markdown file</button>}
  {error && !open && !action && <p role="alert" className="linked-spec-error">{error}</p>}
  <Dialog open={open} onOpenChange={value => { if (!busy) { setOpen(value); setError(''); } }} title="Link a Markdown file" description="Use the same file on disk in Canopy, Codex, Claude and Gemini. Loading replaces this task’s specification; it does not change the file.">
   <label>File path<input autoFocus value={path} disabled={busy} placeholder="Absolute path to an existing .md file" onChange={event => { setPath(event.target.value); setPreview(null); setError(''); }}/></label>
   <p className="muted">Paste the full path on this machine. Files must be UTF-8 Markdown, up to 2 MB. Edits autosave in Canopy; writing to disk is explicit.</p>
   <Button variant="secondary" disabled={busy} blockedReason={!path.trim() && 'Enter the path to a Markdown file.'} onClick={() => { void previewFile(); }}>{busy && <Loader2 size={13} className="spin"/>}Preview file</Button>
   {preview && <label>File preview<textarea className="linked-spec-preview" readOnly rows={9} value={preview.markdown} aria-label="Linked Markdown preview"/></label>}
   {error && <p role="alert" className="linked-spec-error">{error}</p>}
   <div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setOpen(false)}>Cancel</Button><Button disabled={busy} blockedReason={!preview && 'Preview the file before linking it.'} onClick={() => { void run(async () => { if (!preview) return; await beforeChange(); await mutate(`/api/spec-files/${item.id}`, 'PUT', { path: preview.path, expectedHash: preview.hash, specJson: JSON.stringify(parseMarkdown(preview.markdown)) }, item.projectId); setOpen(false); toast.success('Linked file loaded into specification'); }); }}>Link and load</Button></div>
  </Dialog>
  <Dialog open={action !== null} onOpenChange={value => { if (!value && !busy) { setAction(null); setError(''); } }} title={action === 'write' ? 'Save specification to linked file?' : 'Unlink Markdown file?'} description={action === 'write' ? 'This replaces the file with the saved specification. Markdown formatting may be normalized. External changes must be refreshed first.' : 'The specification stays in Canopy and the file stays on disk.'}>
   <code className="linked-spec-path">{link?.path}</code>
   {action === 'write' && <textarea readOnly rows={8} value={outgoing} aria-label="Markdown to save"/>}
   {error && <p role="alert" className="linked-spec-error">{error}</p>}
   <div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setAction(null)}>Cancel</Button><Button disabled={busy} blockedReason={!link && 'This task no longer has a linked file.'} onClick={() => { void run(async () => { if (!link) return; await beforeChange(); if (action === 'write') {
    // Do not let task edits made after this preview silently change the outgoing content.
    if (toMarkdown(JSON.parse(getSpec())) !== outgoing) throw new Error('The specification changed. Cancel and preview Save to file again.');
    await mutate(`/api/spec-files/${item.id}/write`, 'POST', { expectedHash: link.hash, markdown: outgoing }, item.projectId);
   } else await mutate(`/api/spec-files/${item.id}`, 'DELETE', undefined, item.projectId); setAction(null); toast.success(action === 'write' ? 'Markdown file saved' : 'Markdown file unlinked'); }); }}>{busy && <Loader2 size={13} className="spin"/>}{action === 'write' ? 'Save to file' : 'Unlink file'}</Button></div>
  </Dialog>
 </div>;
}
