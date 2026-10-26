import { CalendarDays, GitBranch, Link2, FileText, ArrowUpRight, MoreHorizontal, Boxes, SignalHigh, SignalMedium, SignalLow, CircleAlert } from 'lucide-react';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import type { CSSProperties } from 'react';
import type { Item, Priority, Person } from '../lib/types';
import { descendants, initials, dateLabel, overdue, assignedPeople } from '../lib/types';
import { useWorkspace } from '../lib/workspace';
import { TaskEstimate } from './TaskEstimate';
import { componentForTask } from '../lib/board';

export function PriorityIcon({ priority, size = 13 }: { priority: Priority; size?: number }) {
 if (priority === 'urgent') return <CircleAlert size={size} className="priority-urgent"/>;
 if (priority === 'high') return <SignalHigh size={size} className="priority-high"/>;
 if (priority === 'medium') return <SignalMedium size={size} className="priority-medium"/>;
 return <SignalLow size={size} className={priority === 'none' ? 'muted' : 'priority-low'}/>;
}
export function Avatar({ name, small = false }: { name?: string; small?: boolean }) { return <span title={name || 'Unassigned'} className={`avatar ${small ? 'avatar-small' : ''}`}>{name ? initials(name) : '–'}</span>; }
export function AssigneeAvatars({ people }: { people: Person[] }) {
 const label = people.length ? people.map(person => person.name).join(', ') : 'Unassigned';
 return <span className="assignee-avatars" role="group" aria-label={`Assignees: ${label}`} title={label}>{people.length ? people.slice(0, 2).map(person => <Avatar key={person.id} small name={person.name}/>) : <Avatar small/>}{people.length > 2 && <span className="avatar avatar-small assignee-overflow">+{people.length - 2}</span>}</span>;
}
export function TaskCard({ item, onOpen, onFocus, overlay = false, disabled = false }: { item: Item; onOpen: (id: string) => void; onFocus: (id: string) => void; overlay?: boolean; disabled?: boolean }) {
 const { data } = useWorkspace();
 const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: overlay ? `overlay:${item.id}` : item.id, disabled: overlay || disabled, data: { type: 'task', item } });
 const style: CSSProperties = { transform: CSS.Transform.toString(transform), transition, opacity: isDragging ? 0.25 : 1 };
 const children = descendants(data.items, item.id); const complete = data.items.filter(i => children.has(i.id) && data.statuses.find(s => s.id === i.statusId)?.isCompleted).length;
 const deps = data.dependencies.filter(d => d.itemId === item.id); const blocked = deps.some(d => { const target = data.items.find(i => i.id === d.dependsOnId); return target && !data.statuses.find(s => s.id === target.statusId)?.isCompleted; });
 const people = assignedPeople(item, data.people);
 const component = componentForTask(data.items, item);
 const hasSpec = (() => { try { const s = JSON.parse(item.specJson); return JSON.stringify(s).includes('"text"'); } catch { return false; } })();
 return <article ref={setNodeRef} style={style} {...attributes} {...listeners} role="group" className={`task-card ${overlay ? 'task-card-overlay' : ''}`} data-task-id={item.id} aria-label={`Task: ${item.title}`} onClick={() => onOpen(item.id)} onKeyDown={e => { if (e.target !== e.currentTarget) return; if (e.key === 'Enter') { e.stopPropagation(); onOpen(item.id); } else listeners?.onKeyDown?.(e); }}>
  <div className="card-top"><span className={`priority-label priority-${item.priority}`}><PriorityIcon priority={item.priority}/>{item.priority === 'none' ? 'Task' : item.priority}</span><button className="card-open icon-button" onPointerDown={e => e.stopPropagation()} onClick={e => { e.stopPropagation(); onOpen(item.id); }} aria-label={`Open ${item.title}`}><MoreHorizontal size={17}/></button></div>
  <h3>{item.title}</h3>{item.parentId && <span className="card-parent" aria-label={`Component: ${component?.title ?? 'unavailable'}`} title={component?.title ?? 'Component unavailable'}><Boxes size={12}/><span>{component?.title ?? 'Component unavailable'}</span></span>}
  {blocked && <span className="blocked-badge"><Link2 size={11}/> Waiting on {deps.filter(d => { const t = data.items.find(i => i.id === d.dependsOnId); return t && !data.statuses.find(s => s.id === t.statusId)?.isCompleted; }).length} task{deps.length !== 1 ? 's' : ''}</span>}
  <div className="card-footer"><div className="card-metadata"><TaskEstimate hours={item.estimatedHours}/>{item.dueDate && <span className={overdue(item, data.statuses) ? 'overdue' : ''}><CalendarDays size={12}/>{dateLabel(item.dueDate)}</span>}{children.size > 0 && <button className="subtask-chip" onPointerDown={e => e.stopPropagation()} onClick={e => { e.stopPropagation(); onFocus(item.id); }} title="Explore subitems"><GitBranch size={12}/>{complete}/{children.size}<ArrowUpRight size={10}/></button>}{hasSpec && <FileText size={13} aria-label="Has specification"/>}</div><AssigneeAvatars people={people}/></div>
 </article>;
}
