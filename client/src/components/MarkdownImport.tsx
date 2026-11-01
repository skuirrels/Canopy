import { useEffect, useMemo, useState } from 'react';
import { useEditor, EditorContent } from '@tiptap/react';
import { Loader2 } from 'lucide-react';
import { parseMarkdown, specificationExtensions } from '../lib/markdown';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';
import { blockedBy } from '../lib/blocked';
import './agents.css';

export function MarkdownImport({ initialMarkdown = '', onApply, onClose }: { initialMarkdown?: string; onApply: (markdown: string, mode: 'append' | 'replace') => Promise<void>; onClose: () => void }) {
 const [markdown, setMarkdown] = useState(initialMarkdown); const [mode, setMode] = useState<'append' | 'replace'>('append'); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
 const parsed = useMemo(() => { try { return { document: parseMarkdown(markdown), error: '' }; } catch (e) { return { document: null, error: (e as Error).message }; } }, [markdown]);
 const editor = useEditor({ editable: false, extensions: specificationExtensions, content: { type: 'doc', content: [] }, editorProps: { attributes: { class: 'spec-editor', 'aria-label': 'Specification import preview' } } });
 useEffect(() => { if (editor && !editor.isDestroyed && parsed.document) editor.commands.setContent(parsed.document, { emitUpdate: false }); }, [editor, parsed.document]);
 return <Dialog open wide onOpenChange={open => { if (!open && !busy) onClose(); }} title="Import specification" description="Preview Markdown, then append it or replace the existing specification." className="markdown-import-dialog">
  <label>Markdown<textarea rows={7} value={markdown} onChange={e => setMarkdown(e.target.value)} placeholder="Paste a specification or documentation…"/></label>
  <label>How to import<select value={mode} onChange={e => setMode(e.target.value as 'append' | 'replace')}><option value="append">Append to existing specification</option><option value="replace">Replace existing specification</option></select></label>
  {parsed.error ? <p role="alert" className="agent-error">{parsed.error}</p> : <div className="markdown-preview"><span className="eyebrow">PREVIEW</span><EditorContent editor={editor}/></div>}
  {error && <p role="alert" className="agent-error">{error}</p>}
  <div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={onClose}>Cancel</Button><Button disabled={busy} blockedReason={blockedBy([!markdown.trim(), 'Paste some Markdown to import.'], [!parsed.document, 'Fix the Markdown problem above first.'])} onClick={() => { setBusy(true); setError(''); void onApply(markdown, mode).then(onClose).catch(e => setError((e as Error).message)).finally(() => setBusy(false)); }}>{busy && <Loader2 size={14} className="spin"/>}{mode === 'append' ? 'Append specification' : 'Replace specification'}</Button></div>
 </Dialog>;
}
