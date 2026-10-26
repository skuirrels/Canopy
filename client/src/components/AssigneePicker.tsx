import { useState } from 'react';
import { ChevronDown, Search, Users } from 'lucide-react';
import type { Person } from '../lib/types';
import { Avatar } from './TaskCard';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';

export function AssigneePicker({ people, value, onChange }: { people: Person[]; value: string[]; onChange: (ids: string[]) => void }) {
 const [open, setOpen] = useState(false);
 const [search, setSearch] = useState('');
 const matches = people.filter(person => person.name.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
 return <>
  <button className="assignee-picker-trigger" aria-label="Choose task assignees" aria-haspopup="dialog" aria-expanded={open} onClick={() => { setSearch(''); setOpen(true); }}>
   <span className="assignee-picker-selected">{value.length ? value.map(id => <span className="assignee-person" key={id}><Avatar small name={people.find(person => person.id === id)?.name}/>{people.find(person => person.id === id)?.name ?? 'Unavailable assignee'}</span>) : <span className="assignee-person"><Users size={16}/>Unassigned</span>}</span><ChevronDown size={14}/>
  </button>
  <Dialog open={open} onOpenChange={setOpen} title="Assign people" description="Select everyone responsible for this task. Changes save automatically." className="assignee-picker-dialog">
   <label className="assignee-picker-search"><Search size={16}/><input aria-label="Find an assignee" placeholder="Find a person…" value={search} onChange={event => setSearch(event.target.value)}/></label>
   <div className="assignee-picker-options">{matches.map(person => <label key={person.id}><input type="checkbox" aria-label={person.name} checked={value.includes(person.id)} onChange={event => onChange(event.target.checked ? [...value, person.id] : value.filter(id => id !== person.id))}/><Avatar small name={person.name}/><span>{person.name}</span></label>)}{!matches.length && <p>{people.length ? 'No people match your search.' : 'Add people in workspace Assignees first.'}</p>}</div>
   <div className="dialog-footer"><button className="text-button" disabled={!value.length} onClick={() => onChange([])}>Clear assignments</button><span className="assignee-picker-count">{value.length} selected</span><Button onClick={() => setOpen(false)}>Done</Button></div>
  </Dialog>
 </>;
}
