import { useEffect, useId, useRef, useState } from 'react';
import { Check, ChevronDown, Loader2, Plus, X } from 'lucide-react';
import { toast } from 'sonner';
import type { Person } from '../lib/types';
import { assigneeSuggestions, toggleAssignee } from '../lib/assignee-picker';
import { Avatar } from './TaskCard';

export function AssigneePicker({ people, value, onChange, onCreate }: { people: Person[]; value: string[]; onChange: (ids: string[]) => void; onCreate: (name: string) => Promise<Person> }) {
 const [open, setOpen] = useState(false); const [search, setSearch] = useState('');
 const [active, setActive] = useState(0); const [adding, setAdding] = useState(false); const [error, setError] = useState('');
 const root = useRef<HTMLDivElement>(null); const input = useRef<HTMLInputElement>(null);
 const mounted = useRef(true); const inFlight = useRef(false); const current = useRef({ value, open }); current.current = { value, open };
 const id = useId(); const suggestions = assigneeSuggestions(people, search);
 const count = suggestions.matches.length + (suggestions.canCreate ? 1 : 0); const highlighted = Math.min(active, count - 1);
 useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
 useEffect(() => {
  if (!open) return;
  const outside = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) closeSuggestions(); };
  document.addEventListener('pointerdown', outside);
  return () => document.removeEventListener('pointerdown', outside);
 }, [open]);
 useEffect(() => { if (open) document.getElementById(`${id}-option-${highlighted}`)?.scrollIntoView({ block: 'nearest' }); }, [id, highlighted, open, search]);
 function closeSuggestions() { setOpen(false); if (!inFlight.current) setSearch(''); }
 function select(person: Person) {
  if (inFlight.current) return;
  onChange(toggleAssignee(current.current.value, person.id)); setError(''); setSearch(''); setActive(0); input.current?.focus();
 }
 async function create() {
  if (inFlight.current || !suggestions.canCreate) return;
  inFlight.current = true; setAdding(true); setError('');
  try {
   const person = await onCreate(suggestions.name);
   if (!mounted.current) { toast.info(`${person.name} was added to workspace Assignees.`); return; }
   onChange([...new Set([...current.current.value, person.id])]); setSearch(''); setActive(0);
   if (current.current.open) input.current?.focus();
  } catch (cause) { if (mounted.current) setError((cause as Error).message); }
  finally { inFlight.current = false; if (mounted.current) setAdding(false); }
 }
 return <div ref={root} className="assignee-picker" data-assignee-picker-open={open ? 'true' : undefined} onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget)) closeSuggestions(); }} onKeyDown={event => {
  if (event.key === 'Escape' && open) { event.preventDefault(); event.stopPropagation(); closeSuggestions(); return; }
  if (event.target !== input.current || adding) return;
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); setOpen(true); setActive(index => !open ? (event.key === 'ArrowDown' ? 0 : count - 1) : (index + (event.key === 'ArrowDown' ? 1 : -1) + Math.max(count, 1)) % Math.max(count, 1)); }
  if (event.key === 'Enter' && open && count) { event.preventDefault(); if (highlighted < suggestions.matches.length) select(suggestions.matches[highlighted]); else void create(); }
 }}>
  <div className="assignee-picker-control" onClick={event => { if (event.target === event.currentTarget) input.current?.focus(); }}>
   {value.map(personId => { const person = people.find(candidate => candidate.id === personId); const name = person?.name ?? 'Unavailable assignee'; return <span className="assignee-picker-chip" key={personId}><Avatar small name={person?.name}/><span>{name}</span><button type="button" aria-label={`Remove ${name} from task`} disabled={adding} onClick={() => { onChange(value.filter(selected => selected !== personId)); input.current?.focus(); }}><X size={12}/></button></span>; })}
   <div className="assignee-picker-input"><input ref={input} role="combobox" aria-label="Task assignees" aria-describedby={error ? `${id}-error` : undefined} aria-invalid={!!error} aria-autocomplete="list" aria-haspopup="listbox" aria-expanded={open} aria-controls={`${id}-options`} aria-activedescendant={open && count ? `${id}-option-${highlighted}` : undefined} autoComplete="off" placeholder={value.length ? 'Add assignees…' : 'Type a name…'} value={search} maxLength={200} readOnly={adding} onFocus={() => setOpen(true)} onChange={event => { setSearch(event.target.value); setActive(0); setError(''); setOpen(true); }}/><button type="button" aria-label={open ? 'Close assignee suggestions' : 'Show assignee suggestions'} onClick={() => { if (open) closeSuggestions(); else input.current?.focus(); }}><ChevronDown size={14}/></button></div>
  </div>
  {open && <div className="assignee-picker-panel" aria-busy={adding}>
   <div id={`${id}-options`} role="listbox" aria-label="Assignee suggestions" aria-multiselectable="true" className="assignee-picker-options">
    {suggestions.matches.map((person, index) => <button type="button" role="option" aria-selected={value.includes(person.id)} id={`${id}-option-${index}`} key={person.id} className={highlighted === index ? 'highlighted' : ''} disabled={adding} onMouseDown={event => event.preventDefault()} onMouseEnter={() => setActive(index)} onClick={() => select(person)}><Avatar small name={person.name}/><span>{person.name}</span>{value.includes(person.id) && <Check size={15} aria-hidden="true"/>}</button>)}
    {suggestions.canCreate && <button type="button" role="option" aria-selected="false" id={`${id}-option-${suggestions.matches.length}`} className={`assignee-picker-create ${highlighted === suggestions.matches.length ? 'highlighted' : ''}`} disabled={adding} onMouseDown={event => event.preventDefault()} onMouseEnter={() => setActive(suggestions.matches.length)} onClick={() => { void create(); }}>{adding ? <Loader2 size={16} className="spin"/> : <Plus size={16}/>}<span>{adding ? 'Adding…' : `Add and assign “${suggestions.name}”`}</span></button>}
    {!count && <p>Type a name to add an assignee.</p>}
   </div>
   <div className="assignee-picker-footer"><span>{value.length} selected</span><button type="button" onClick={() => { input.current?.focus(); closeSuggestions(); }}>Done</button></div>
  </div>}
  {error && <p id={`${id}-error`} role="alert" className="assignee-picker-error">{error}</p>}
 </div>;
}
