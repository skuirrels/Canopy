import { useEffect, useId, useRef, useState } from 'react';
import type { FormEvent, FormEventHandler } from 'react';
import { Check, Pencil, Plus, Trash2, Users } from 'lucide-react';
import type { Person } from '../lib/types';
import { Avatar } from './TaskCard';
import { BlockedNote, Button } from './ui/button';
import { blockedBy } from '../lib/blocked';
import './assignees.css';

type Props = {
 people: Person[]; name: string; busy: boolean;
 onNameChange: (name: string) => void;
 onAdd: FormEventHandler<HTMLFormElement>;
 onRemove: (person: Person) => void;
 onEdit: (person: Person, name: string) => Promise<void>;
};

export function Assignees({ people, name, busy, onNameChange, onAdd, onRemove, onEdit }: Props) {
 const [editing, setEditing] = useState<Person | null>(null), [draft, setDraft] = useState('');
 const [saving, setSaving] = useState(false), [error, setError] = useState<string | null>(null);
 const input = useRef<HTMLInputElement>(null), returnFocus = useRef<string | null>(null);
 useEffect(() => {
  if (editing) { input.current?.focus(); input.current?.select(); }
  else if (returnFocus.current) { document.getElementById(`edit-assignee-${returnFocus.current}`)?.focus(); returnFocus.current = null; }
 }, [editing]);
 useEffect(() => { if (editing && error && !saving && !busy) input.current?.focus(); }, [editing, error, saving, busy]);
 function closeEdit() { if (editing) returnFocus.current = editing.id; setEditing(null); setError(null); }
 async function save(event: FormEvent) {
  event.preventDefault(); if (!editing || saving || busy || !draft.trim()) return;
  setSaving(true); setError(null);
  try { await onEdit(editing, draft); closeEdit(); }
  catch (cause) { setError(cause instanceof Error ? cause.message : String(cause)); }
  finally { setSaving(false); }
 }
 const locked = busy || saving;
 const editingNote = useId(); const editingReason = editing && 'Save or cancel the open edit to change other assignees.';

 return <section className="assignees-page" aria-labelledby="assignees-heading">
  <header className="assignees-heading"><span className="assignees-page-icon" aria-hidden="true"><Users size={24}/></span><div><h1 id="assignees-heading">Assignees</h1><p>People you can assign to work across your projects.</p></div></header>
  <form className="assignees-add-form" onSubmit={onAdd}>
   <label htmlFor="assignee-name">Name<input id="assignee-name" aria-label="Assignee name" value={name} onChange={event => onNameChange(event.target.value)} placeholder="Enter a name" required maxLength={200}/></label>
   <Button disabled={locked} blockedReason={blockedBy([editing !== null, 'Save or cancel the open edit first.'], [!name.trim(), 'Enter a name first.'])}><Plus size={16}/>{busy ? 'Adding…' : 'Add assignee'}</Button>
  </form>
  <div className="assignees-list-heading"><h2>People</h2><span>{people.length} {people.length === 1 ? 'assignee' : 'assignees'}</span></div>
  <BlockedNote id={editingNote} reason={editingReason}/>
  {people.length ? <ul className="assignees-list" aria-label="Workspace assignees">{[...people].sort((a, b) => a.name.localeCompare(b.name)).map(person => <li className={`assignees-list-row ${editing?.id === person.id ? 'assignees-editing' : ''}`} key={person.id}><Avatar name={person.name}/>{editing?.id === person.id ? <form className="assignees-edit-form" aria-label={`Edit ${person.name}`} onSubmit={event => { void save(event); }}>
   <label>Name<input ref={input} aria-label={`Edit name for ${person.name}`} value={draft} onChange={event => { setDraft(event.target.value); setError(null); }} onKeyDown={event => { if (event.key === 'Escape' && !locked) { event.preventDefault(); event.stopPropagation(); closeEdit(); } }} disabled={locked} required maxLength={200} aria-invalid={!!error} aria-describedby={error ? 'assignee-edit-error' : undefined}/></label>
   <div className="assignees-edit-actions"><Button size="sm" disabled={locked} blockedReason={!draft.trim() && 'Enter a name first.'}><Check size={15}/>{locked ? 'Saving…' : 'Save'}</Button><Button type="button" size="sm" variant="secondary" disabled={locked} onClick={closeEdit}>Cancel</Button></div>
   {error && <p className="assignees-edit-error danger" id="assignee-edit-error" role="alert">{error}</p>}
  </form> : <><span className="assignees-person-name">{person.name}</span><div className="assignees-row-actions"><button id={`edit-assignee-${person.id}`} className="assignees-edit-button" aria-label={`Edit ${person.name}`} data-tooltip="Edit assignee" disabled={locked || editing !== null} aria-describedby={editingReason ? editingNote : undefined} onClick={() => { setDraft(person.name); setError(null); setEditing(person); }}><Pencil size={16}/>Edit</button><button className="icon-button" aria-label={`Remove ${person.name}`} data-tooltip="Remove assignee" disabled={locked || editing !== null} aria-describedby={editingReason ? editingNote : undefined} onClick={() => onRemove(person)}><Trash2 size={17}/></button></div></>}</li>)}</ul> : <div className="assignees-empty"><Users size={30}/><h2>No assignees yet</h2><p>Add yourself or someone working on your projects.</p></div>}

 </section>;
}
