import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties, KeyboardEvent, PointerEvent } from 'react';
import { CalendarDays, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, Link2, Plus, CalendarClock, GripVertical } from 'lucide-react';
import { toast } from 'sonner';
import { useWorkspace } from '../lib/workspace';
import { fields, assignedPeople } from '../lib/types';
import type { Item, Project } from '../lib/types';
import { calendarLabel, calendarSegments, changeDates, ganttRows, schedule, timelineRange, todayDay } from '../lib/gantt';
import type { DateChange } from '../lib/gantt';
import { AssigneeAvatars } from './TaskCard';
import { Button } from './ui/button';
import './gantt.css';
import { TaskEstimate } from './TaskEstimate';
import { ChartExport } from './ChartExport';
import { captureGantt } from '../lib/chart-export';

type Scale = 'days' | 'weeks' | 'months';
type Gesture = { id: string; mode: DateChange; x: number; pointerId: number; startDate: string | null; dueDate: string | null; delta: number };
const ROW_HEIGHT = 60;
const DAY_WIDTH: Record<Scale, number> = { days: 40, weeks: 22, months: 8 };

export default function Gantt({ project, items, focusId, onOpen, onAdd }: { project: Project; items: Item[]; focusId: string | null; onOpen: (id: string) => void; onAdd: () => void }) {
 const { data, pending, mutate } = useWorkspace(); const liveData = useRef(data); liveData.current = data;
 const [scale, setScale] = useState<Scale>('weeks'); const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
 const [viewportWidth, setViewportWidth] = useState(0);
 const [showLinks, setShowLinks] = useState(true);
 const [preview, setPreview] = useState<{ id: string; mode: DateChange; delta: number } | null>(null);
 const scroll = useRef<HTMLDivElement>(null); const gesture = useRef<Gesture | null>(null); const suppressClick = useRef(false); const initialPositioned = useRef(false);
 const today = todayDay(); const dayWidth = DAY_WIDTH[scale];
 const allItems = useMemo(() => data.items.filter(item => item.projectId === project.id), [data.items, project.id]);
 const rows = useMemo(() => ganttRows(allItems, items, collapsed, focusId), [allItems, items, collapsed, focusId]);
 const rangeItems = useMemo(() => ganttRows(allItems, items, new Set(), focusId).map(row => row.item), [allItems, items, focusId]);
 const baseRange = useMemo(() => timelineRange(rangeItems, today, today, viewportWidth / dayWidth), [rangeItems, today, viewportWidth, dayWidth]);
 const range = { start: baseRange.start, end: Math.max(baseRange.end, baseRange.start + Math.ceil(viewportWidth / dayWidth) - 1) };
 const timelineWidth = (range.end - range.start + 1) * dayWidth;
 const months = useMemo(() => calendarSegments(range.start, range.end, 'month'), [range.start, range.end]);
 const ticks = useMemo(() => calendarSegments(range.start, range.end, scale === 'days' ? 'day' : 'week'), [range.start, range.end, scale]);
 const statuses = new Map(data.statuses.map(status => [status.id, status]));
 const scheduledCount = items.filter(item => schedule(item)).length;
 const displayItem = (item: Item) => preview?.id === item.id ? { ...item, ...changeDates(item, preview.mode, preview.delta) } : item;
 const rowIndex = new Map(rows.map((row, index) => [row.item.id, index]));
 const visibleLinks = showLinks ? data.dependencies.filter(link => rowIndex.has(link.itemId) && rowIndex.has(link.dependsOnId)) : [];
 const visibleLinkCount = visibleLinks.filter(link => schedule(rows[rowIndex.get(link.itemId)!].item) && schedule(rows[rowIndex.get(link.dependsOnId)!].item)).length;
 useLayoutEffect(() => { const viewport = scroll.current; if (!viewport) return; const update = () => { const labelWidth = parseFloat(getComputedStyle(viewport.parentElement!).getPropertyValue('--task-column-width')); setViewportWidth(Math.max(0, viewport.clientWidth - labelWidth)); }; update(); const observer = new ResizeObserver(update); observer.observe(viewport); return () => observer.disconnect(); }, [items.length === 0]);
 useLayoutEffect(() => {
  if (!items.length) { initialPositioned.current = false; return; }
  if (initialPositioned.current || !scroll.current || viewportWidth <= 0) return;
  scroll.current.scrollLeft = Math.max(0, (today - range.start + .5) * dayWidth - viewportWidth / 2);
  initialPositioned.current = true;
 }, [items.length, range.start, range.end, dayWidth, viewportWidth, today]);
 useEffect(() => { const cancel = (event: globalThis.KeyboardEvent) => { if (event.key === 'Escape' && gesture.current) { gesture.current = null; setPreview(null); suppressClick.current = true; } }; window.addEventListener('keydown', cancel); return () => window.removeEventListener('keydown', cancel); }, []);
 function toggle(id: string) { setCollapsed(old => { const next = new Set(old); if (next.has(id)) next.delete(id); else next.add(id); return next; }); }
 async function saveDates(id: string, mode: DateChange, delta: number, original: Pick<Item, 'startDate' | 'dueDate'>) {
  const current = liveData.current.items.find(item => item.id === id); if (!current) return;
  if (current.startDate !== original.startDate || current.dueDate !== original.dueDate) { toast.error('These dates changed while you were dragging. Try again.'); return; }
  const nextDates = changeDates(current, mode, delta); if (nextDates.startDate === current.startDate && nextDates.dueDate === current.dueDate) return;
  try {
   await mutate(`/api/items/${id}`, 'PATCH', { ...fields(current, liveData.current.dependencies), ...nextDates }, project.id);
   toast.success('Task dates updated', { duration: 10000, action: { label: 'Undo', onClick: () => {
    const latest = liveData.current.items.find(item => item.id === id); if (!latest) return;
    if (latest.startDate !== nextDates.startDate || latest.dueDate !== nextDates.dueDate) { toast.error('Dates have changed again. Undo the latest date change first.'); return; }
    void mutate(`/api/items/${id}`, 'PATCH', { ...fields(latest, liveData.current.dependencies), startDate: original.startDate, dueDate: original.dueDate }, project.id).catch(() => {});
   } } });
  } catch { /* The workspace reports persistence and revision errors. */ }
 }
 function begin(event: PointerEvent<HTMLButtonElement>, item: Item, mode: DateChange) {
  if (pending || event.button !== 0) return;
  event.stopPropagation(); suppressClick.current = false; event.currentTarget.setPointerCapture(event.pointerId);
  gesture.current = { id: item.id, mode, x: event.clientX + (scroll.current?.scrollLeft ?? 0), pointerId: event.pointerId, startDate: item.startDate, dueDate: item.dueDate, delta: 0 };
 }
 function move(event: PointerEvent<HTMLDivElement>) {
  const active = gesture.current; if (!active || active.pointerId !== event.pointerId) return;
  const viewport = scroll.current; if (viewport) { const bounds = viewport.getBoundingClientRect(); if (event.clientX > bounds.right - 36) viewport.scrollLeft += 12; else if (event.clientX < bounds.left + 36) viewport.scrollLeft -= 12; }
  const pixels = event.clientX + (viewport?.scrollLeft ?? 0) - active.x; if (Math.abs(pixels) > 4) suppressClick.current = true;
  const delta = Math.round(pixels / dayWidth); if (delta === active.delta) return;
  active.delta = delta; setPreview({ id: active.id, mode: active.mode, delta });
 }
 function finish(event: PointerEvent<HTMLDivElement>) { const active = gesture.current; if (!active || active.pointerId !== event.pointerId) return; gesture.current = null; setPreview(null); if (active.delta) void saveDates(active.id, active.mode, active.delta, active); }
 function cancel() { gesture.current = null; setPreview(null); }
 function keyboardDate(event: KeyboardEvent<HTMLButtonElement>, item: Item, mode: DateChange) {
  if (pending || (mode === 'move' && !event.altKey) || !['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
  event.preventDefault(); event.stopPropagation(); void saveDates(item.id, mode, (event.key === 'ArrowRight' ? 1 : -1) * (event.shiftKey ? 7 : 1), item);
 }
 function showToday() { if (scroll.current) scroll.current.scrollLeft = Math.max(0, (today - range.start + .5) * dayWidth - viewportWidth / 2); }
 if (!items.length) return <div className="full-state gantt-empty"><CalendarClock size={32}/><h2>No tasks on this timeline yet</h2><p>Add a task, or clear your filters to see the project schedule.</p><Button onClick={onAdd}><Plus size={16}/>Add task</Button></div>;
 return <section className="gantt-view" aria-label="Project schedule">
  <div className="gantt-toolbar"><div className="gantt-summary"><CalendarDays size={17}/><strong>Timeline</strong><span>{scheduledCount} scheduled · {items.length - scheduledCount} unscheduled</span></div><div className="gantt-actions"><ChartExport projectName={project.name} view="Gantt chart" detail={`${scale[0].toUpperCase() + scale.slice(1)} · ${rows.length} shown rows · ${focusId ? allItems.find(item => item.id === focusId)?.title : 'Project root'} · Dependency arrows ${showLinks ? 'on' : 'off'}`} disabled={pending > 0 || preview !== null} capture={() => { const grid = scroll.current?.querySelector<HTMLElement>('.gantt-grid'); if (!grid) throw new Error('The timeline is still loading.'); return captureGantt(grid); }}/><div className="gantt-scale" role="group" aria-label="Timeline zoom">{(['days', 'weeks', 'months'] as Scale[]).map(value => <button key={value} aria-pressed={scale === value} onClick={() => setScale(value)}>{value[0].toUpperCase() + value.slice(1)}</button>)}</div><button className="gantt-today" onClick={showToday}>Today</button><button className={`icon-button ${showLinks ? 'active' : ''}`} onClick={() => setShowLinks(value => !value)} aria-label="Show Gantt dependencies" aria-pressed={showLinks}><Link2 size={16}/></button><button className="icon-button" onClick={() => setCollapsed(collapsed.size ? new Set() : new Set(allItems.filter(item => allItems.some(child => child.parentId === item.id)).map(item => item.id)))} aria-label={collapsed.size ? 'Expand all Gantt tasks' : 'Collapse all Gantt tasks'}>{collapsed.size ? <ChevronsUpDown size={17}/> : <ChevronsDownUp size={17}/>}</button></div></div>
  <div className="gantt-scroll" ref={scroll} tabIndex={0} role="region" aria-label="Scrollable project timeline">
   <div className="gantt-grid" style={{ '--timeline-width': `${timelineWidth}px`, '--day-width': `${dayWidth}px`, '--row-height': `${ROW_HEIGHT}px` } as CSSProperties}>
    <div className="gantt-axis"><div className="gantt-list-heading"><span>Task / assignees</span><span>{rows.length} shown</span></div><div className="gantt-calendar"><div className="gantt-months">{months.map(month => <span key={month.start} style={{ left: (month.start - range.start) * dayWidth, width: month.days * dayWidth }}>{month.label}</span>)}</div><div className="gantt-ticks">{ticks.map(tick => <span key={tick.start} style={{ left: (tick.start - range.start) * dayWidth, width: tick.days * dayWidth }}>{scale === 'months' ? calendarLabel(tick.start, { day: 'numeric' }) : tick.label}</span>)}</div></div></div>
    <div className="gantt-body">
     {rows.map(({ item: original, depth, childCount, context }) => {
      const item = displayItem(original); const value = schedule(item); const status = statuses.get(item.statusId); const people = assignedPeople(item, data.people);
      const label = value ? value.kind === 'range' ? `${calendarLabel(value.start)} – ${calendarLabel(value.end)} (${value.end - value.start + 1} days)` : `${value.kind === 'due' ? 'Due' : 'Starts'} ${calendarLabel(value.start)}` : 'Unscheduled';
      return <div className={`gantt-row ${context ? 'gantt-context' : ''}`} key={item.id} data-task-id={item.id}>
       <div className="gantt-task" style={{ paddingLeft: 12 + Math.min(depth, 8) * 18 }}><button className="gantt-collapse" disabled={!childCount} onClick={() => toggle(item.id)} aria-label={`${collapsed.has(item.id) ? 'Expand' : 'Collapse'} ${item.title}`}>{childCount ? collapsed.has(item.id) ? <ChevronRight size={15}/> : <ChevronDown size={15}/> : <span className="status-dot" style={{ background: status?.color }}/>}</button><button className="gantt-task-title" onClick={() => onOpen(item.id)} title={item.title}><span>{item.title}</span><small>{status?.name}{context ? ' · parent context' : ''}{item.estimatedHours != null && <TaskEstimate hours={item.estimatedHours}/>}</small></button><AssigneeAvatars people={people}/></div>
       <div className="gantt-track" onPointerMove={move} onPointerUp={finish} onPointerCancel={cancel}>
        {value ? <div className={`gantt-bar ${value.kind !== 'range' ? 'gantt-milestone' : ''} ${childCount ? 'gantt-parent-bar' : ''} ${preview?.id === item.id ? 'gantt-bar-dragging' : ''}`} style={{ left: (value.start - range.start) * dayWidth + (value.kind === 'range' ? 2 : dayWidth / 2 - 14), width: value.kind === 'range' ? Math.max(4, (value.end - value.start + 1) * dayWidth - 4) : 28, '--task-color': status?.color ?? project.color } as CSSProperties} title={`${item.title}\n${label}${people.length ? `\n${people.map(person => person.name).join(', ')}` : ''}`}>
         <button className="gantt-bar-body" aria-label={`Open ${item.title}: ${label}`} aria-describedby="gantt-instructions" onPointerDown={event => begin(event, original, 'move')} onLostPointerCapture={cancel} onKeyDown={event => keyboardDate(event, original, 'move')} onClick={() => { if (suppressClick.current) { suppressClick.current = false; return; } onOpen(item.id); }} disabled={pending > 0}>{value.kind === 'range' ? <span>{item.title}</span> : <span className={`gantt-point gantt-point-${value.kind}`}/>}</button>
         {value.kind === 'range' && (value.end - value.start + 1) * dayWidth >= 40 && <><button className="gantt-resize gantt-resize-start" aria-label={`Resize start of ${item.title}`} aria-describedby="gantt-instructions" onPointerDown={event => begin(event, original, 'start')} onLostPointerCapture={cancel} onKeyDown={event => keyboardDate(event, original, 'start')} disabled={pending > 0}><GripVertical size={12}/></button><button className="gantt-resize gantt-resize-end" aria-label={`Resize end of ${item.title}`} aria-describedby="gantt-instructions" onPointerDown={event => begin(event, original, 'end')} onLostPointerCapture={cancel} onKeyDown={event => keyboardDate(event, original, 'end')} disabled={pending > 0}><GripVertical size={12}/></button></>}
         {preview?.id === item.id && <span className="gantt-date-preview">{label}</span>}
        </div> : <button className="gantt-unscheduled" onClick={() => onOpen(item.id)}><CalendarClock size={14}/>Set dates</button>}
       </div>
      </div>;
     })}
     <svg className="gantt-dependencies" width={timelineWidth} height={rows.length * ROW_HEIGHT} role="img" aria-label={`${visibleLinkCount} visible task dependencies`}><defs><marker id="gantt-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto"><path d="M 0 0 L 7 3.5 L 0 7 z" fill="var(--gantt-link)"/></marker></defs>{visibleLinks.map(link => {
      const fromIndex = rowIndex.get(link.dependsOnId)!, toIndex = rowIndex.get(link.itemId)!; const from = schedule(displayItem(rows[fromIndex].item)), to = schedule(displayItem(rows[toIndex].item)); if (!from || !to) return null;
      const x1 = (from.end - range.start + (from.kind === 'range' ? 1 : .5)) * dayWidth; const x2 = (to.start - range.start + (to.kind === 'range' ? 0 : .5)) * dayWidth; const y1 = fromIndex * ROW_HEIGHT + ROW_HEIGHT / 2, y2 = toIndex * ROW_HEIGHT + ROW_HEIGHT / 2;
      const mid = x2 > x1 + 24 ? (x1 + x2) / 2 : x1 + 16; const between = fromIndex < toIndex ? (fromIndex + 1) * ROW_HEIGHT : fromIndex * ROW_HEIGHT;
      const path = x2 > x1 + 24 ? `M${x1},${y1} H${mid} V${y2} H${x2 - 3}` : `M${x1},${y1} H${mid} V${between} H${x2 - 16} V${y2} H${x2 - 3}`;
      return <path key={link.id} d={path} markerEnd="url(#gantt-arrow)"><title>{rows[toIndex].item.title} depends on {rows[fromIndex].item.title}</title></path>;
     })}</svg>
     {today >= range.start && today <= range.end && <div className="gantt-today-line" style={{ left: `calc(var(--task-column-width) + ${(today - range.start + .5) * dayWidth}px)` }}><span>Today</span></div>}
    </div>
   </div>
  </div>
  <div className="gantt-help" id="gantt-instructions"><span>Drag bars to move dates · Drag either edge to resize · Alt + ←/→ moves a focused bar; arrows resize an edge (Shift = 7 days)</span><span>◇ Due date only · ○ Start date only · Unscheduled tasks need dates</span></div>
 </section>;
}
