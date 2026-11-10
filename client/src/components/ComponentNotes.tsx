import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { useEditor, EditorContent } from '@tiptap/react';
import Placeholder from '@tiptap/extension-placeholder';
import { Bold, Check, CircleAlert, Code2, Copy, Download, FileUp, Heading2, Italic, List, ListChecks, ListOrdered, Loader2, NotebookPen, Plus, Trash2, Undo2, X } from 'lucide-react';
import { toast } from 'sonner';
import type { Item } from '../lib/types';
import { dateLabel } from '../lib/types';
import { useWorkspace } from '../lib/workspace';
import { MAX_SPEC_BYTES, parseMarkdown, specificationExtensions, toMarkdown } from '../lib/markdown';
import { blankNote, noteExcerpt, noteFields, noteFileName, noteLabel, notesFor, readableMarkdown } from '../lib/notes';
import type { NoteFields } from '../lib/notes';
import { taskDateShortcut } from '../lib/task-dates';
import { Button } from './ui/button';
import { Dialog } from './ui/dialog';
import './notes.css';

type Mode = 'rich' | 'markdown';
type SaveState = 'saved' | 'saving' | 'unsaved' | 'error';
const modeKey = 'canopy:notes-mode';
const draftKey = (id: string) => `canopy:note-draft:${id}`;
function serialize(document: object): string {
 const markdown = toMarkdown(document); const readable = readableMarkdown(markdown);
 if (readable === markdown) return markdown;
 try { return JSON.stringify(parseMarkdown(readable)) === JSON.stringify(parseMarkdown(markdown)) ? readable : markdown; } catch { return markdown; }
}
function storedMode(): Mode { try { return localStorage.getItem(modeKey) === 'markdown' ? 'markdown' : 'rich'; } catch { return 'rich'; } }
function remember(key: string, value: string | null) { try { if (value === null) localStorage.removeItem(key); else localStorage.setItem(key, value); } catch { /* Drafts still autosave to the server. */ } }

export function ComponentNotes({ item, initialNoteId, onClose }: { item: Item; initialNoteId?: string | null; onClose: () => void }) {
 const { data, mutate } = useWorkspace();
 const notes = useMemo(() => notesFor(data.notes, item.id), [data.notes, item.id]);
 const [selectedId, setSelectedId] = useState<string | null>(initialNoteId ?? notes[0]?.id ?? null);
 const selected = notes.find(note => note.id === selectedId) ?? null;
 const [draft, setDraft] = useState<NoteFields | null>(null);
 const latest = useRef(draft); latest.current = draft;
 const saved = useRef(''); const inFlight = useRef<Promise<void> | null>(null);
 const [saveState, setSaveState] = useState<SaveState>('saved');
 const [mode, setMode] = useState<Mode>(storedMode);
 const [richBlocked, setRichBlocked] = useState('');
 const [deleting, setDeleting] = useState(false); const [busy, setBusy] = useState(false);
 const createdHere = useRef(new Set<string>());
 const titleInput = useRef<HTMLInputElement>(null); const markdownInput = useRef<HTMLTextAreaElement>(null); const importFile = useRef<HTMLInputElement>(null);
 const loadedId = useRef<string | null>(null); const focusOnLoad = useRef<{ id: string; target: 'title' | 'text' } | null>(null);

 const save = useCallback(async (): Promise<void> => {
  if (inFlight.current) { await inFlight.current; if (latest.current && JSON.stringify(latest.current) !== saved.current) await save(); return; }
  const id = loadedId.current; const current = latest.current;
  if (!id || !current) return;
  const serial = JSON.stringify(current); if (serial === saved.current) return;
  setSaveState('saving');
  const operation = (async () => {
   try {
    await mutate(`/api/notes/${id}`, 'PATCH', current, item.projectId);
    if (loadedId.current === id) { saved.current = serial; const clean = JSON.stringify(latest.current) === serial; setSaveState(clean ? 'saved' : 'unsaved'); if (clean) remember(draftKey(id), null); }
   } catch { setSaveState('error'); throw new Error('This note has unsaved changes.'); }
   finally { inFlight.current = null; }
  })();
  inFlight.current = operation; await operation;
 }, [item.projectId, mutate]);

 const editor = useEditor({ shouldRerenderOnTransaction: true, extensions: [...specificationExtensions, Placeholder.configure({ placeholder: 'Who was on the call, what was agreed, and what happens next…' })], editorProps: { attributes: { class: 'spec-editor note-editor', 'aria-label': 'Note text', role: 'textbox' } }, onUpdate: ({ editor: current }) => { if (loadedId.current) setDraft(value => value && ({ ...value, markdown: serialize(current.getJSON()) })); } });
 // Rich text is a view of the stored Markdown. Content the editor cannot represent stays in Markdown mode.
 const showRich = useCallback((markdown: string) => {
  if (!editor || editor.isDestroyed) return;
  try { editor.commands.setContent(markdown.trim() ? parseMarkdown(markdown) : { type: 'doc', content: [{ type: 'paragraph' }] }, { emitUpdate: false }); setRichBlocked(''); }
  catch (error) { setRichBlocked((error as Error).message.replace('task specs', 'rich text').replace(/ No changes were saved\.$/, '')); setMode('markdown'); }
 }, [editor]);

 // Load the selected note, restoring a browser draft that never reached the server.
 useEffect(() => {
  if (!selected) { if (!selectedId || !notes.length) { loadedId.current = null; setDraft(null); } if (selectedId && !notes.some(note => note.id === selectedId)) setSelectedId(notes[0]?.id ?? null); return; }
  if (loadedId.current === selected.id) {
   const incoming = JSON.stringify(noteFields(selected));
   if (incoming !== saved.current && JSON.stringify(latest.current) === saved.current) { saved.current = incoming; setDraft(noteFields(selected)); if (mode === 'rich') showRich(selected.markdown); }
   return;
  }
  loadedId.current = selected.id; saved.current = JSON.stringify(noteFields(selected));
  let next = noteFields(selected);
  try { const stored = localStorage.getItem(draftKey(selected.id)); if (stored) next = { ...next, ...JSON.parse(stored) as Partial<NoteFields> }; } catch { /* Ignore unreadable drafts. */ }
  setDraft(next); setSaveState(JSON.stringify(next) === saved.current ? 'saved' : 'unsaved'); setRichBlocked('');
  if (mode === 'rich') showRich(next.markdown);
 }, [selected, selectedId, notes, mode, showRich]);
 useEffect(() => { if (editor && mode === 'rich' && latest.current) showRich(latest.current.markdown); }, [editor, mode, showRich]);
 useEffect(() => {
  const pending = focusOnLoad.current;
  if (!pending || !draft || loadedId.current !== pending.id) return;
  focusOnLoad.current = null;
  requestAnimationFrame(() => { if (pending.target === 'title') titleInput.current?.focus(); else if (mode === 'markdown') markdownInput.current?.focus(); else editor?.commands.focus('end'); });
 }, [draft, editor, mode]);

 useEffect(() => {
  const id = loadedId.current;
  if (!draft || !id || JSON.stringify(draft) === saved.current) return;
  remember(draftKey(id), JSON.stringify(draft)); setSaveState('unsaved');
  const timer = setTimeout(() => { void save().catch(() => {}); }, 700); return () => clearTimeout(timer);
 }, [draft, save]);
 useEffect(() => {
  const beforeUnload = (event: BeforeUnloadEvent) => { if (latest.current && JSON.stringify(latest.current) !== saved.current) event.preventDefault(); };
  window.addEventListener('beforeunload', beforeUnload); return () => window.removeEventListener('beforeunload', beforeUnload);
 }, []);

 function patch(change: Partial<NoteFields>) { setDraft(value => value && ({ ...value, ...change })); }
 function changeMode(next: Mode) { setMode(next); remember(modeKey, next); }
 async function select(id: string) { if (id === selectedId) return; try { await save(); setSelectedId(id); } catch { toast.error('Retry saving this note before opening another.'); } }
 async function create(fields: NoteFields) {
  setBusy(true);
  try {
   await save();
   const before = new Set((data.notes ?? []).map(note => note.id));
   const next = await mutate(`/api/items/${item.id}/notes`, 'POST', fields, item.projectId);
   const created = next.notes?.find(note => !before.has(note.id));
   if (created) { createdHere.current.add(created.id); focusOnLoad.current = { id: created.id, target: fields.title ? 'text' : 'title' }; setSelectedId(created.id); }
  } catch { /* The workspace reports failures. */ } finally { setBusy(false); }
 }
 function importMarkdown(file: File) {
  if (file.size > MAX_SPEC_BYTES) { toast.error('Choose a Markdown file smaller than 2 MB.'); return; }
  void file.arrayBuffer().then(buffer => new TextDecoder('utf-8', { fatal: true }).decode(buffer))
   .then(markdown => create({ title: file.name.replace(/\.(md|markdown|txt)$/i, '').slice(0, 200), heldOn: new Date(file.lastModified).toLocaleDateString('en-CA'), markdown }))
   .catch(() => toast.error('Choose a valid UTF-8 Markdown or text file.'));
 }
 async function remove() {
  if (!selected) return;
  const id = selected.id; const previous = saved.current;
  // Stop autosave from writing to a note that is being deleted.
  saved.current = JSON.stringify(latest.current); setBusy(true);
  try { await inFlight.current; await mutate(`/api/notes/${id}`, 'DELETE', undefined, item.projectId); remember(draftKey(id), null); loadedId.current = null; setDeleting(false); setSelectedId(notes.find(note => note.id !== id)?.id ?? null); toast.success('Note deleted'); }
  catch { saved.current = previous; setDraft(value => value && { ...value }); } finally { setBusy(false); }
 }
 async function close() {
  try { await save(); } catch { toast.error('This note could not be saved. Your draft is kept in this browser; retry before closing.'); return; }
  // A note opened and left blank was never written, so it should not stay behind as clutter.
  const blank = notes.filter(note => createdHere.current.has(note.id) && blankNote(note.id === loadedId.current && latest.current ? latest.current : noteFields(note)));
  for (const note of blank) { try { await mutate(`/api/notes/${note.id}`, 'DELETE', undefined, item.projectId); remember(draftKey(note.id), null); } catch { /* reported */ } }
  onClose();
 }
 const today = taskDateShortcut(0);
 const tools = editor ? [
  { label: 'Bold', icon: Bold, active: editor.isActive('bold'), action: () => editor.chain().focus().toggleBold().run() },
  { label: 'Italic', icon: Italic, active: editor.isActive('italic'), action: () => editor.chain().focus().toggleItalic().run() },
  { label: 'Heading', icon: Heading2, active: editor.isActive('heading'), action: () => editor.chain().focus().toggleHeading({ level: 2 }).run() },
  { label: 'Bullet list', icon: List, active: editor.isActive('bulletList'), action: () => editor.chain().focus().toggleBulletList().run() },
  { label: 'Action items', icon: ListChecks, active: editor.isActive('taskList'), action: () => editor.chain().focus().toggleTaskList().run() },
  { label: 'Numbered list', icon: ListOrdered, active: editor.isActive('orderedList'), action: () => editor.chain().focus().toggleOrderedList().run() },
  { label: 'Code block', icon: Code2, active: editor.isActive('codeBlock'), action: () => editor.chain().focus().toggleCodeBlock().run() },
  { label: 'Undo typing', icon: Undo2, active: false, action: () => editor.chain().focus().undo().run() },
 ] : [];
 return <DialogPrimitive.Root open onOpenChange={open => { if (!open) void close(); }}><DialogPrimitive.Portal><DialogPrimitive.Overlay className="dialog-overlay"/>
  <DialogPrimitive.Content className="notes-dialog" onOpenAutoFocus={event => { event.preventDefault(); requestAnimationFrame(() => { if (!latest.current) return; if (mode === 'markdown') markdownInput.current?.focus(); else editor?.commands.focus('end'); }); }} onEscapeKeyDown={event => { if (deleting) event.preventDefault(); }}>
   <header className="notes-header">
    <div><span className="eyebrow"><NotebookPen size={12}/>Component notes</span><DialogPrimitive.Title>{item.title}</DialogPrimitive.Title><DialogPrimitive.Description className="sr-only">Write and edit Markdown notes for this component, such as notes from a call. Notes autosave and never change the specification.</DialogPrimitive.Description></div>
    <div className="notes-header-actions">{draft && <span className={`save-indicator ${saveState}`} aria-live="polite">{saveState === 'saving' ? <Loader2 size={11} className="spin"/> : saveState === 'saved' ? <Check size={11}/> : <CircleAlert size={11}/>}{{ saved: 'All changes saved', saving: 'Saving…', unsaved: 'Unsaved changes', error: 'Save failed' }[saveState]}</span>}{saveState === 'error' && <Button size="sm" variant="secondary" onClick={() => { void save().catch(() => {}); }}>Retry save</Button>}<button className="icon-button" aria-label="Close notes" onClick={() => { void close(); }}><X size={18}/></button></div>
   </header>
   <div className="notes-body">
    <nav className="notes-list" aria-label={`Notes for ${item.title}`}>
     <div className="notes-list-actions">{notes.length > 0 && <Button size="sm" disabled={busy} onClick={() => { void create({ title: '', heldOn: today, markdown: '' }); }}><Plus size={14}/>New note</Button>}<button className="text-button" disabled={busy} onClick={() => importFile.current?.click()} data-tooltip="Create a note from a .md or .txt file"><FileUp size={13}/>Import</button><input ref={importFile} type="file" hidden accept=".md,.markdown,.txt,text/markdown,text/plain" aria-label="Import note file" onChange={event => { const file = event.target.files?.[0]; event.target.value = ''; if (file) importMarkdown(file); }}/></div>
     {notes.length > 0 && <ul>{notes.map(note => { const fields = note.id === loadedId.current && draft ? draft : noteFields(note); return <li key={note.id}><button className={note.id === selectedId ? 'active' : ''} aria-current={note.id === selectedId ? 'true' : undefined} onClick={() => { void select(note.id); }}><strong>{noteLabel(fields)}</strong><span>{fields.heldOn ? dateLabel(fields.heldOn) : 'No date'}{noteExcerpt(fields.markdown) && <> · {noteExcerpt(fields.markdown, 70)}</>}</span></button></li>; })}</ul>}
    </nav>
    <section className="notes-editor" aria-label="Note">
     {draft && selected ? <>
      <div className="notes-fields">
       <label className="notes-title"><span className="sr-only">Note title</span><input ref={titleInput} value={draft.title} maxLength={200} placeholder={draft.heldOn ? `Notes from ${dateLabel(draft.heldOn)}` : 'Untitled note'} onChange={event => patch({ title: event.target.value })}/></label>
       <label className="notes-date"><span>Call date</span><input type="date" value={draft.heldOn ?? ''} onChange={event => patch({ heldOn: event.target.value || null })}/></label>
      </div>
      <div className="notes-toolbar">
       <div className="notes-mode" role="group" aria-label="Editing mode"><button aria-pressed={mode === 'rich'} disabled={!!richBlocked} data-tooltip={richBlocked || 'Format as you type'} onClick={() => changeMode('rich')}>Rich text</button><button aria-pressed={mode === 'markdown'} data-tooltip="Edit the Markdown source" onClick={() => changeMode('markdown')}>Markdown</button></div>
       {mode === 'rich' && <div className="notes-format">{tools.map(tool => <button key={tool.label} className={`icon-button ${tool.active ? 'active' : ''}`} data-tooltip={tool.label} aria-label={tool.label} aria-pressed={tool.active} onClick={tool.action}><tool.icon size={15}/></button>)}</div>}
       <div className="notes-file-actions"><button className="icon-button" aria-label="Copy note as Markdown" data-tooltip="Copy Markdown" onClick={() => { void navigator.clipboard.writeText(draft.markdown).then(() => toast.success('Markdown copied')).catch(() => toast.error('Could not copy. Switch to Markdown and copy the text.')); }}><Copy size={15}/></button><button className="icon-button" aria-label="Download note as Markdown" data-tooltip="Download .md" onClick={() => { const url = URL.createObjectURL(new Blob([draft.markdown], { type: 'text/markdown;charset=utf-8' })); const link = document.createElement('a'); link.href = url; link.download = noteFileName(draft); link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); }}><Download size={15}/></button><button className="icon-button" aria-label="Delete note" data-tooltip="Delete note" onClick={() => setDeleting(true)}><Trash2 size={15}/></button></div>
      </div>
      {richBlocked && mode === 'markdown' && <p className="notes-mode-note">Rich text is unavailable for this note: {richBlocked}</p>}
      <div className="notes-content" hidden={mode !== 'rich'} onMouseDown={event => { if (event.target === event.currentTarget) { event.preventDefault(); editor?.commands.focus('end'); } }}><EditorContent editor={editor}/></div>
      {mode === 'markdown' && <textarea ref={markdownInput} className="notes-markdown" aria-label="Note Markdown" spellCheck value={draft.markdown} placeholder={'## Attendees\n\n## Decisions\n\n## Action items\n- [ ] '} onChange={event => { patch({ markdown: event.target.value }); if (richBlocked) setRichBlocked(''); }}/>}
     </> : <div className="notes-empty"><NotebookPen size={30}/><h3>No notes yet</h3><p>Keep what was said on a call with this component: who attended, what was agreed and what happens next. Write in rich text or Markdown, or import a .md file.</p><Button disabled={busy} onClick={() => { void create({ title: '', heldOn: today, markdown: '' }); }}><Plus size={15}/>New note</Button></div>}
    </section>
   </div>
   <Dialog open={deleting} onOpenChange={value => { if (!busy) setDeleting(value); }} title="Delete this note?" description={`“${draft ? noteLabel(draft) : ''}” will be permanently removed. This cannot be undone.`}><div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setDeleting(false)}>Keep note</Button><Button variant="danger" disabled={busy} onClick={() => { void remove(); }}>Delete note</Button></div></Dialog>
  </DialogPrimitive.Content>
 </DialogPrimitive.Portal></DialogPrimitive.Root>;
}
