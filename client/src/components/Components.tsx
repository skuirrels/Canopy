import { useMemo, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import { DndContext, DragOverlay, PointerSensor, KeyboardSensor, useSensor, useSensors, useDraggable, pointerWithin, rectIntersection, closestCenter } from '@dnd-kit/core';
import { SortableContext, horizontalListSortingStrategy, sortableKeyboardCoordinates, useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import type { DragEndEvent } from '@dnd-kit/core';
import { Boxes, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, Plus, GripVertical, FileText, Link2, CalendarDays, ArrowUpRight, ArrowLeft, Check } from 'lucide-react';
import { toast } from 'sonner';
import type { Item, Project } from '../lib/types';
import { dateLabel, descendants, overdue, assignedPeople } from '../lib/types';
import { compareSiblings, componentDetails, epicGroups, resequenceComponents, specificationText } from '../lib/epics';
import type { EpicGroup, EpicTask } from '../lib/epics';
import { useWorkspace } from '../lib/workspace';
import { Avatar, PriorityIcon } from './TaskCard';
import { Button } from './ui/button';
import './components.css';
import { TaskEstimate } from './TaskEstimate';

type Actions = { onOpen: (id: string) => void; onFocus: (id: string | null) => void; onAdd: (parentId: string | null, component?: boolean) => void };

function TaskMetadata({ item, listView = false }: { item: Item; listView?: boolean }) {
 const { data } = useWorkspace();
 const status = data.statuses.find(value => value.id === item.statusId);
 const people = assignedPeople(item, data.people);
 const prerequisites = data.dependencies.filter(link => link.itemId === item.id).map(link => data.items.find(task => task.id === link.dependsOnId)).filter((task): task is Item => !!task);
 const complete = data.statuses.find(value => value.id === item.statusId)?.isCompleted;
 const blockers = complete ? [] : prerequisites.filter(task => !data.statuses.find(value => value.id === task.statusId)?.isCompleted);
 const workflow = <><TaskEstimate hours={item.estimatedHours}/>{status && <span className="epic-status"><span className="status-dot" style={{ background: status.color }}/>{status.name}</span>}{item.priority !== 'none' && <span title={`${item.priority} priority`}><PriorityIcon priority={item.priority}/>{item.priority}</span>}{prerequisites.length > 0 && <span className={blockers.length ? 'epic-blocked' : ''} title={`Depends on: ${prerequisites.map(task => task.title).join(', ')}`}><Link2 size={13}/>{blockers.length ? `${blockers.length} blocking` : `${prerequisites.length} ${prerequisites.length === 1 ? 'dependency' : 'dependencies'}`}</span>}</>;
 const dates = (item.startDate || item.dueDate) && <span className={overdue(item, data.statuses) ? 'overdue' : ''} title={item.startDate && item.dueDate ? `${item.startDate} to ${item.dueDate}` : item.dueDate ? `Due ${item.dueDate}` : `Starts ${item.startDate}`}><CalendarDays size={13}/>{item.startDate && item.dueDate ? `${dateLabel(item.startDate)} – ${dateLabel(item.dueDate)}` : item.dueDate ? `Due ${dateLabel(item.dueDate)}` : `Starts ${dateLabel(item.startDate!)}`}</span>;
 const assignee = people.length ? people.map(person => <span className="epic-assignee" key={person.id}><Avatar small name={person.name}/>{person.name}</span>) : <span className="epic-assignee"><Avatar small/>Unassigned</span>;
 return listView ? <><div className="component-list-status epic-task-metadata">{workflow}</div><div className="component-list-dates epic-task-metadata">{dates || <span>No dates</span>}</div><div className="component-list-assignee epic-task-metadata">{assignee}</div></> : <div className="epic-task-metadata">{workflow}{dates}{assignee}</div>;
}

function TaskRow({ node, depth, collapsed, toggle, onOpen, onAdd, listView = false }: { node: EpicTask; depth: number; collapsed: Set<string>; toggle: (id: string) => void; listView?: boolean } & Pick<Actions, 'onOpen' | 'onAdd'>) {
 const { data, pending } = useWorkspace(); const item = node.item;
 const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: item.id, disabled: pending > 0 || listView, data: { item } });
 const hasChildren = node.children.length > 0; const open = !collapsed.has(item.id);
 const complete = data.statuses.find(status => status.id === item.statusId)?.isCompleted;
 const spec = specificationText(item.specJson);
 return <li className={`epic-task ${node.context ? 'epic-context' : ''}`}>
  <div ref={setNodeRef} className={`epic-task-row ${isDragging ? 'epic-task-dragging' : ''}`} style={{ '--task-depth': Math.min(depth, 4) } as CSSProperties}>
   {!listView && <button className="epic-drag icon-button" {...attributes} {...listeners} aria-label={`Move ${item.title} to a component`} title="Drag to a component" disabled={pending > 0}><GripVertical size={15}/></button>}
   {hasChildren ? <button className="epic-branch icon-button" aria-label={`${open ? 'Collapse' : 'Expand'} ${item.title}`} aria-expanded={open} onClick={() => toggle(item.id)}>{open ? <ChevronDown size={15}/> : <ChevronRight size={15}/>}</button> : <span className={`epic-task-mark ${complete ? 'epic-task-complete' : ''}`} aria-hidden="true">{complete && <Check size={11}/>}</span>}
   <div className="epic-task-main"><div className="epic-task-title-cell"><button className="epic-task-title" onClick={() => onOpen(item.id)}>{item.title}</button>{node.context && <span className="epic-context-label">Parent context</span>}</div><TaskMetadata item={item} listView={listView}/></div>
   <div className="epic-task-actions">{spec && <button className="icon-button" aria-label={`Open specification for ${item.title}`} title="Open specification" onClick={() => onOpen(item.id)}><FileText size={15}/></button>}<button className="icon-button" aria-label={`Add subtask to ${item.title}`} title="Add subtask" onClick={() => onAdd(item.id)}><Plus size={15}/></button></div>
  </div>
  {hasChildren && open && <ul className="epic-task-tree">{node.children.map(child => <TaskRow key={child.item.id} node={child} depth={depth + 1} collapsed={collapsed} toggle={toggle} onOpen={onOpen} onAdd={onAdd} listView={listView}/>)}</ul>}
 </li>;
}

function ComponentCard({ group, collapsed, toggle, ...actions }: { group: EpicGroup; collapsed: Set<string>; toggle: (id: string) => void } & Actions) {
 const { data, pending } = useWorkspace(); const item = group.item;
 const { setNodeRef, setActivatorNodeRef, isOver, isDragging, attributes, listeners, transform, transition } = useSortable({ id: `component:${item.id}`, data: { type: 'component', parentId: item.id, item }, disabled: pending > 0 });
 const open = !collapsed.has(item.id); const percent = group.taskCount ? Math.round(group.completedCount / group.taskCount * 100) : 0;
 const spec = specificationText(item.specJson); const status = data.statuses.find(value => value.id === item.statusId);
 return <article ref={setNodeRef} className={`epic-component ${isOver ? 'epic-component-over' : ''} ${isDragging ? 'epic-component-dragging' : ''}`} style={{ transform: CSS.Translate.toString(transform), transition }} aria-label={`Component: ${item.title}`}>
  <header className="epic-component-header"><div className="epic-component-top"><span className="epic-component-label"><button ref={setActivatorNodeRef} className="epic-component-grip icon-button" {...attributes} {...listeners} aria-label={`Reorder component ${item.title}`} title="Drag left or right to reorder · Space, arrow keys, Space to move by keyboard" disabled={pending > 0}><GripVertical size={16}/></button><Boxes size={15}/>Component</span><div className="epic-component-actions"><button className="icon-button" aria-label={`Open task list for component ${item.title}`} title="Open component task list" onClick={() => actions.onFocus(item.id)}><ArrowUpRight size={17}/></button><button className="icon-button" aria-label={`Expand component ${item.title}`} title="Expand into task list" onClick={() => actions.onFocus(item.id)}><ChevronDown size={17}/></button></div></div>
   <h2><button onClick={() => actions.onOpen(item.id)}>{item.title}</button></h2>
   {spec && <button className="epic-spec-preview" aria-label={`Open specification for component ${item.title}`} onClick={() => actions.onOpen(item.id)} title="Open component specification">{spec}</button>}
   <TaskMetadata item={item}/>
   <div className="epic-component-progress"><span>{group.completedCount} / {group.taskCount} tasks complete</span>{group.taskCount > 0 && <strong>{percent}%</strong>}</div>
   {group.taskCount > 0 && <div className="epic-progress-track" role="progressbar" aria-label={`${item.title} task completion`} aria-valuemin={0} aria-valuemax={group.taskCount} aria-valuenow={group.completedCount}><span style={{ width: `${percent}%`, background: status?.color }}/></div>}
   {group.matchingTaskCount !== group.taskCount && <span className="epic-filter-count">{group.matchingTaskCount} matching tasks · progress includes all tasks</span>}
   {group.context && <span className="epic-context-label">Contains matching tasks</span>}
  </header>
  {open && <><ul className="epic-task-tree">{group.tasks.map(node => <TaskRow key={node.item.id} node={node} depth={0} collapsed={collapsed} toggle={toggle} onOpen={actions.onOpen} onAdd={actions.onAdd}/>)}</ul>{!group.tasks.length && <p className="epic-component-empty">{group.taskCount ? 'No tasks match the current filters.' : 'Break this component into the tasks needed to build it.'}</p>}<button className="epic-add-task" onClick={() => actions.onAdd(item.id)}><Plus size={15}/>Add task</button></>}
 </article>;
}

export default function Components({ project, items, focusId, onOpen, onFocus, onAdd }: { project: Project; items: Item[]; focusId: string | null } & Actions) {
 const { data, mutate } = useWorkspace(); const latest = useRef(data); latest.current = data;
 const [draggingComponent, setDraggingComponent] = useState(false);
 const [collapsed, setCollapsed] = useState<Set<string>>(new Set()); const [dragging, setDragging] = useState<Item | null>(null);
 const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }), useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }));
 const allItems = useMemo(() => data.items.filter(item => item.projectId === project.id), [data.items, project.id]);
 const groups = useMemo(() => epicGroups(allItems, items, data.statuses, focusId), [allItems, items, data.statuses, focusId]);
 const detail = useMemo(() => focusId ? componentDetails(allItems, items, data.statuses, focusId) : undefined, [allItems, items, data.statuses, focusId]);
 const previewsHidden = groups.length > 0 && groups.every(group => collapsed.has(group.item.id));
 const taskCount = groups.reduce((sum, group) => sum + group.taskCount, 0); const complete = groups.reduce((sum, group) => sum + group.completedCount, 0);
 const blockedCount = groups.reduce((sum, group) => {
  const ids = descendants(allItems, group.item.id);
  return sum + allItems.filter(item => ids.has(item.id) && !data.statuses.find(status => status.id === item.statusId)?.isCompleted && data.dependencies.some(link => link.itemId === item.id && allItems.some(target => target.id === link.dependsOnId && !data.statuses.find(status => status.id === target.statusId)?.isCompleted))).length;
 }, 0);
 function toggle(id: string) { setCollapsed(previous => { const next = new Set(previous); if (next.has(id)) next.delete(id); else next.add(id); return next; }); }
 async function moveTask(event: DragEndEvent) {
  setDragging(null); setDraggingComponent(false);
  if (event.active.data.current?.type === 'component') {
   const activeId = event.active.data.current.item.id as string;
   const targetId = event.over?.data.current?.item?.id as string | undefined;
   if (!targetId) return;
   const projectItems = latest.current.items.filter(item => item.projectId === project.id);
   const previous = projectItems.filter(item => item.parentId === focusId).sort(compareSiblings).map(item => item.id);
   const itemIds = resequenceComponents(projectItems, focusId, groups.map(group => group.item.id), activeId, targetId);
   if (!itemIds) return;
   try {
    await mutate(`/api/projects/${project.id}/sibling-order`, 'POST', { parentId: focusId, itemIds }, project.id);
    toast.success('Components reordered', { duration: 10000, action: { label: 'Undo', onClick: () => {
     const current = latest.current.items.filter(item => item.projectId === project.id && item.parentId === focusId).sort(compareSiblings).map(item => item.id);
     if (JSON.stringify(current) !== JSON.stringify(itemIds)) { toast.error('The component order changed again. Undo its latest change first.'); return; }
     void mutate(`/api/projects/${project.id}/sibling-order`, 'POST', { parentId: focusId, itemIds: previous }, project.id).catch(() => {});
    } } });
   } catch { /* The workspace reports validation, revision, and persistence failures. */ }
   return;
  }
  const parentId = event.over?.data.current?.parentId as string | undefined;
  const item = latest.current.items.find(value => value.id === event.active.id);
  if (!item || !parentId || item.parentId === parentId) return;
  if (item.id === parentId || descendants(latest.current.items, item.id).has(parentId)) { toast.error('A task cannot be moved into its own subitems.'); return; }
  const beforeId = latest.current.items.filter(value => value.parentId === item.parentId && value.statusId === item.statusId && value.position > item.position).sort((a, b) => a.position - b.position)[0]?.id ?? null;
  try {
   await mutate(`/api/items/${item.id}/move`, 'POST', { parentId, statusId: item.statusId, beforeId: null }, project.id);
   toast.success('Task moved to component', { duration: 10000, action: { label: 'Undo', onClick: () => {
    const current = latest.current.items.find(value => value.id === item.id);
    if (current?.parentId !== parentId) { toast.error('This task moved again. Undo its latest move first.'); return; }
    void mutate(`/api/items/${item.id}/move`, 'POST', { parentId: item.parentId, statusId: current.statusId, beforeId: current.statusId === item.statusId ? beforeId : null }, project.id).catch(() => {});
   } } });
  } catch { /* The workspace reports validation, revision, and persistence failures. */ }
 }
 if (detail) {
  const branchScope = descendants(allItems, detail.item.id);
  const branchIds = allItems.filter(item => branchScope.has(item.id) && allItems.some(child => child.parentId === item.id)).map(item => item.id);
  const branchesCollapsed = branchIds.length > 0 && branchIds.every(id => collapsed.has(id));
  return <section className="epics-view component-detail-view" aria-label={`${detail.item.title} task list`}>
   <div className="epics-toolbar"><div className="component-detail-heading"><button className="text-button component-back" onClick={() => onFocus(detail.item.parentId)}><ArrowLeft size={16}/>{detail.item.parentId ? 'Back to parent tasks' : 'All components'}</button><h2><button onClick={() => onOpen(detail.item.id)} title="Open component specification">{detail.item.title}</button></h2><span className="component-detail-count">{detail.completedCount} / {detail.taskCount} tasks complete</span></div><div className="epics-toolbar-actions">{branchIds.length > 0 && <button className="icon-button" aria-label={branchesCollapsed ? 'Expand all task branches' : 'Collapse all task branches'} title={branchesCollapsed ? 'Expand all task branches' : 'Collapse all task branches'} onClick={() => setCollapsed(branchesCollapsed ? new Set() : new Set(branchIds))}>{branchesCollapsed ? <ChevronsUpDown size={17}/> : <ChevronsDownUp size={17}/>}</button>}<Button size="sm" variant="secondary" onClick={() => onOpen(detail.item.id)}><FileText size={15}/>Component spec</Button><Button size="sm" onClick={() => onAdd(detail.item.id)}><Plus size={15}/>Add task</Button></div></div>
   <div className="component-list-scroll"><div className="component-detail-summary"><TaskMetadata item={detail.item}/>{detail.matchingTaskCount !== detail.taskCount && <span className="epic-filter-count">{detail.matchingTaskCount} matching tasks · progress includes all tasks</span>}</div>
    <DndContext><div className="component-task-list"><div className="component-list-columns" aria-hidden="true"><span/><span>Task</span><span>Status / priority</span><span>Dates</span><span>Assignees</span><span/></div><ul className="epic-task-tree">{detail.tasks.map(node => <TaskRow key={node.item.id} node={node} depth={0} collapsed={collapsed} toggle={toggle} onOpen={onOpen} onAdd={onAdd} listView/>)}</ul>{!detail.tasks.length && <div className="epics-empty"><FileText size={30}/><h2>{detail.taskCount ? 'No tasks match your filters' : 'No tasks in this component yet'}</h2><p>{detail.taskCount ? 'Clear search or filters to see the tasks in this component.' : 'Add a task to start breaking down this component.'}</p>{!detail.taskCount && <Button onClick={() => onAdd(detail.item.id)}><Plus size={16}/>Add task</Button>}</div>}</div></DndContext>
   </div><div className="epics-help">Open a task for its specification. Expand task branches to see nested work. Use All components or the breadcrumbs to go back.</div>
  </section>;
 }
 return <section className="epics-view" aria-label="Project components">
  <div className="epics-toolbar"><div className="epics-summary"><span><Boxes size={17}/><strong>{groups.length}</strong> {groups.length === 1 ? 'component' : 'components'}</span><span><strong>{complete}/{taskCount}</strong> tasks complete</span>{blockedCount > 0 && <span className="epic-blocked"><Link2 size={14}/>{blockedCount} blocked</span>}</div><div className="epics-toolbar-actions"><button className="icon-button" aria-label={previewsHidden ? 'Show task previews' : 'Hide task previews'} title={previewsHidden ? 'Show task previews' : 'Hide task previews'} onClick={() => setCollapsed(previous => { const next = new Set(previous); groups.forEach(group => { if (previewsHidden) next.delete(group.item.id); else next.add(group.item.id); }); return next; })}>{previewsHidden ? <ChevronsUpDown size={17}/> : <ChevronsDownUp size={17}/>}</button><Button size="sm" variant="secondary" onClick={() => onAdd(focusId, true)}><Plus size={15}/>New component</Button></div></div>
  <DndContext sensors={sensors} collisionDetection={args => args.active.data.current?.type === 'component' ? closestCenter({ ...args, droppableContainers: args.droppableContainers.filter(target => target.data.current?.type === 'component') }) : args.pointerCoordinates ? pointerWithin(args) : rectIntersection(args)} onDragStart={event => { setDraggingComponent(event.active.data.current?.type === 'component'); setDragging(event.active.data.current?.item ?? allItems.find(item => item.id === event.active.id) ?? null); }} onDragCancel={() => { setDragging(null); setDraggingComponent(false); }} onDragEnd={event => { void moveTask(event); }}>
   <div className="epics-scroll" tabIndex={0} aria-label="Scrollable component plan"><p className="epics-explanation"><span className="components-desktop-hint">Top-level tasks are your components. Expand a component for its task list, or open a title for its specification.</span><span className="components-phone-hint">Swipe for components · Tap the chevron for tasks</span></p>
    {groups.length ? <SortableContext items={groups.map(group => `component:${group.item.id}`)} strategy={horizontalListSortingStrategy}><div className="epics-grid">{groups.map(group => <ComponentCard key={group.item.id} group={group} collapsed={collapsed} toggle={toggle} onOpen={onOpen} onFocus={onFocus} onAdd={onAdd}/>)}</div></SortableContext> : <div className="epics-empty"><Boxes size={34}/><h2>{allItems.some(item => item.parentId === focusId) ? 'No components match your filters' : 'Plan the components you need to build'}</h2><p>{allItems.some(item => item.parentId === focusId) ? 'Clear search or filters to see the component plan.' : 'Add a component, then break it down into tasks and specifications.'}</p><Button onClick={() => onAdd(focusId, true)}><Plus size={16}/>New component</Button></div>}
   </div>
   <DragOverlay>{dragging && <div className="epic-drag-preview">{draggingComponent ? <Boxes size={17}/> : <GripVertical size={17}/>}{dragging.title}</div>}</DragOverlay>
  </DndContext>
  <div className="epics-help">Drag a component’s top handle left or right to reorder. Drag a task’s handle onto a component to move it with its subitems. Status, specifications, dates, and dependencies stay attached.</div>
 </section>;
}
