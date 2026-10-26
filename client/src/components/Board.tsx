import { useState } from 'react';
import type { CSSProperties } from 'react';
import { DndContext, DragOverlay, PointerSensor, KeyboardSensor, useSensor, useSensors, closestCorners, pointerWithin, rectIntersection } from '@dnd-kit/core';
import type { DragEndEvent, DragStartEvent, CollisionDetection } from '@dnd-kit/core';
import { SortableContext, verticalListSortingStrategy, horizontalListSortingStrategy, sortableKeyboardCoordinates, useSortable, arrayMove } from '@dnd-kit/sortable';
import { GripVertical, Plus, MoreHorizontal, Check, Pencil, Trash2 } from 'lucide-react';
import * as Dropdown from '@radix-ui/react-dropdown-menu';
import { CSS } from '@dnd-kit/utilities';
import { toast } from 'sonner';
import { TaskCard } from './TaskCard';
import type { Item, Project, Status } from '../lib/types';
import { useWorkspace } from '../lib/workspace';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';

function Column({ status, items, onOpen, onFocus, onAdd, onEdit, onDelete, disabled }: { status: Status; items: Item[]; onOpen: (id: string) => void; onFocus: (id: string) => void; onAdd: (status: string) => void; onEdit: () => void; onDelete: () => void; disabled: boolean }) {
 const { setNodeRef, isOver, transform, transition, attributes, listeners, isDragging } = useSortable({ id: status.id, data: { type: 'column', status } });
 return <section className={`kanban-column ${isOver ? 'column-over' : ''}`} ref={setNodeRef} style={{ transform: CSS.Transform.toString(transform), transition, opacity: isDragging ? .3 : 1 }} aria-label={`${status.name} column`} data-column-id={status.id}>
  <div className="column-heading"><button className="column-grip" {...attributes} {...listeners} aria-label={`Move ${status.name} column`}><GripVertical size={12}/></button><span className="status-dot" style={{ background: status.color }}>{status.isCompleted && <Check size={9}/>}</span><h2>{status.name}</h2><span className="column-count">{items.length}</span><div className="column-actions"><Dropdown.Root><Dropdown.Trigger className="icon-button" aria-label={`${status.name} column options`}><MoreHorizontal size={16}/></Dropdown.Trigger><Dropdown.Portal><Dropdown.Content className="dropdown" align="end"><Dropdown.Item onSelect={onEdit}><Pencil size={14}/>Edit column</Dropdown.Item><Dropdown.Item className="danger" onSelect={onDelete}><Trash2 size={14}/>Delete empty column</Dropdown.Item></Dropdown.Content></Dropdown.Portal></Dropdown.Root><button className="icon-button" onClick={() => onAdd(status.id)} aria-label={`Add task to ${status.name}`}><Plus size={16}/></button></div></div>
  <SortableContext items={items.map(i => i.id)} strategy={verticalListSortingStrategy}><div className="column-cards">{items.map(item => <TaskCard key={item.id} item={item} onOpen={onOpen} onFocus={onFocus} disabled={disabled}/>)}</div></SortableContext>
  {items.length === 0 && <div className="column-empty"><span className="empty-dash"/>{isOver ? 'Drop it here' : 'A little room for what’s next'}</div>}
  <button className="add-card" onClick={() => onAdd(status.id)}><Plus size={15}/> Add task</button>
 </section>;
}
export function Board({ project, items, onOpen, onFocus, onAdd }: { project: Project; items: Item[]; onOpen: (id: string) => void; onFocus: (id: string) => void; onAdd: (status?: string) => void }) {
 const { data, mutate } = useWorkspace(); const statuses = data.statuses.filter(s => s.projectId === project.id).sort((a, b) => a.position - b.position);
 const [active, setActive] = useState<Item | null>(null); const [activeColumn, setActiveColumn] = useState<Status | null>(null); const [column, setColumn] = useState<Status | 'new' | null>(null); const [name, setName] = useState(''); const [color, setColor] = useState('#94a3b8'); const [completed, setCompleted] = useState(false); const [saving, setSaving] = useState(false);
 const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }), useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }));
 function edit(status: Status | 'new') { setColumn(status); setName(status === 'new' ? '' : status.name); setColor(status === 'new' ? '#94a3b8' : status.color); setCompleted(status !== 'new' && status.isCompleted); }
 function start(event: DragStartEvent) { setActive(data.items.find(i => i.id === event.active.id) || null); setActiveColumn(statuses.find(s => s.id === event.active.id) || null); }
 async function end({ active: drag, over }: DragEndEvent) {
  setActive(null); setActiveColumn(null); if (!over || drag.id === over.id) return;
  if (drag.data.current?.type === 'column') {
   const targetColumnId = over.data.current?.type === 'task' ? over.data.current.item.statusId : over.id;
   const from = statuses.findIndex(s => s.id === drag.id), to = statuses.findIndex(s => s.id === targetColumnId);
   if (from < 0 || to < 0 || from === to) return;
   const previous = statuses.map(s => s.id);
   try { await mutate(`/api/projects/${project.id}/status-order`, 'POST', { statusIds: arrayMove(previous, from, to) }, project.id); toast.success('Workflow reordered', { action: { label: 'Undo', onClick: () => { void mutate(`/api/projects/${project.id}/status-order`, 'POST', { statusIds: previous }, project.id).catch(() => {}); } } }); } catch { /* reported */ }
   return;
  }
  const item = data.items.find(i => i.id === drag.id); if (!item) return;
  const targetItem = data.items.find(i => i.id === over.id); const statusId = targetItem?.statusId || String(over.id);
  const oldSiblings = data.items.filter(i => i.parentId === item.parentId && i.statusId === item.statusId && i.id !== item.id).sort((a, b) => a.position - b.position);
  const beforeOld = oldSiblings.find(i => i.position > item.position)?.id ?? null;
  const destination = data.items.filter(i => i.parentId === item.parentId && i.statusId === statusId && i.id !== item.id).sort((a, b) => a.position - b.position);
  const droppingDown = targetItem && item.parentId === targetItem.parentId && item.statusId === targetItem.statusId && item.position < targetItem.position;
  const beforeId = droppingDown ? destination[destination.findIndex(i => i.id === targetItem.id) + 1]?.id ?? null : targetItem?.parentId === item.parentId ? targetItem?.id ?? null : null;
  try {
   await mutate(`/api/items/${item.id}/move`, 'POST', { parentId: item.parentId, statusId, beforeId }, project.id);
   toast.success('Task moved', { action: { label: 'Undo', onClick: () => { void mutate(`/api/items/${item.id}/move`, 'POST', { parentId: item.parentId, statusId: item.statusId, beforeId: beforeOld }, project.id).catch(() => {}); } } });
  } catch { /* mutation reports error */ }
 }
 async function saveColumn(e: React.FormEvent) {
  e.preventDefault(); if (!column) return; setSaving(true);
  try { await mutate(column === 'new' ? `/api/projects/${project.id}/statuses` : `/api/statuses/${column.id}`, column === 'new' ? 'POST' : 'PATCH', { name, color, isCompleted: completed }, project.id); setColumn(null); } catch { /* reported */ } finally { setSaving(false); }
 }
 const collisions: CollisionDetection = args => {
  if (args.active.data.current?.type === 'column') return closestCorners({ ...args, droppableContainers: args.droppableContainers.filter(c => c.data.current?.type === 'column') });
  const hits = args.pointerCoordinates ? pointerWithin(args) : rectIntersection(args);
  const tasks = hits.filter(hit => args.droppableContainers.find(c => c.id === hit.id)?.data.current?.type === 'task' && hit.id !== args.active.id);
  return tasks.length ? tasks : hits.length ? hits : closestCorners(args);
 };
 return <><DndContext sensors={sensors} collisionDetection={collisions} onDragStart={start} onDragCancel={() => { setActive(null); setActiveColumn(null); }} onDragEnd={event => { void end(event); }}><div className="kanban-board" style={{ '--column-count': statuses.length } as CSSProperties}><SortableContext items={statuses.map(s => s.id)} strategy={horizontalListSortingStrategy}>{statuses.map(status => <Column key={status.id} status={status} items={items.filter(i => i.statusId === status.id).sort((a, b) => a.position - b.position)} onOpen={onOpen} onFocus={onFocus} onAdd={onAdd} onEdit={() => edit(status)} onDelete={() => { void mutate(`/api/statuses/${status.id}`, 'DELETE', undefined, project.id).catch(() => {}); }} disabled={false}/>)}</SortableContext><button className="add-column" onClick={() => edit('new')}><Plus size={16}/> Add column</button></div><DragOverlay>{activeColumn && <div className="column-drag-preview"><GripVertical size={14}/>{activeColumn.name}</div>}{active && <TaskCard item={active} onOpen={() => {}} onFocus={() => {}} overlay/>}</DragOverlay></DndContext>
 <Dialog open={column !== null} onOpenChange={open => { if (!open) setColumn(null); }} title={column === 'new' ? 'A new step in your workflow' : 'Edit workflow column'} description="Make your workflow fit the way you work."><form onSubmit={e => { void saveColumn(e); }}><label>Column name<input autoFocus value={name} onChange={e => setName(e.target.value)} required maxLength={200} placeholder="e.g. In review"/></label><label className="color-field">Colour<input type="color" value={color} onChange={e => setColor(e.target.value)}/></label>{column !== 'new' && <label className="check-field"><input type="checkbox" checked={completed} onChange={e => setCompleted(e.target.checked)}/>Tasks in this column are complete</label>}<div className="dialog-footer"><Button type="button" variant="ghost" onClick={() => setColumn(null)}>Cancel</Button><Button disabled={saving}>{saving ? 'Saving…' : 'Save column'}</Button></div></form></Dialog></>;
}
