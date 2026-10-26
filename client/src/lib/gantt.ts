import type { Dependency, Item } from './types';

const DAY_MS = 86_400_000;
export type Schedule = { start: number; end: number; kind: 'range' | 'start' | 'due' };
export type DateChange = 'move' | 'start' | 'end';
export type GanttRow = { item: Item; depth: number; childCount: number; context: boolean };

// Calendar-day ordinals keep dragging independent of daylight saving and timezone offsets.
export function dateDay(date: string): number { return Date.parse(`${date}T00:00:00Z`) / DAY_MS; }
export function dayDate(day: number): string { return new Date(day * DAY_MS).toISOString().slice(0, 10); }
export function todayDay(now = new Date()): number { return dateDay(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`); }
export function calendarLabel(day: number, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' }): string { return new Intl.DateTimeFormat(undefined, { ...options, timeZone: 'UTC' }).format(new Date(day * DAY_MS)); }
export function schedule(item: Pick<Item, 'startDate' | 'dueDate'>): Schedule | null {
 const start = item.startDate ? dateDay(item.startDate) : null;
 const end = item.dueDate ? dateDay(item.dueDate) : null;
 if (start === null && end === null) return null;
 return { start: start ?? end!, end: end ?? start!, kind: start === null ? 'due' : end === null ? 'start' : 'range' };
}
export function changeDates(item: Pick<Item, 'startDate' | 'dueDate'>, mode: DateChange, requestedDays: number): Pick<Item, 'startDate' | 'dueDate'> {
 const value = schedule(item); if (!value) return { startDate: item.startDate, dueDate: item.dueDate };
 const days = Math.round(requestedDays);
 // Match the DateOnly range accepted by the public task contract.
 const firstDay = dateDay('0001-01-01'), lastDay = dateDay('9999-12-31');
 let start = value.start, end = value.end;
 if (mode === 'move') { const shift = Math.max(firstDay - start, Math.min(lastDay - end, days)); start += shift; end += shift; }
 else if (mode === 'start' && item.startDate) start = Math.max(firstDay, Math.min(end, start + days));
 else if (mode === 'end' && item.dueDate) end = Math.min(lastDay, Math.max(start, end + days));
 return { startDate: item.startDate ? dayDate(start) : null, dueDate: item.dueDate ? dayDate(end) : null };
}
export function timelineRange(items: Item[], today: number, anchor: number | null = null, visibleDays = 0): { start: number; end: number } {
 const dates = items.flatMap(item => { const value = schedule(item); return value ? [value.start, value.end] : []; });
 let start = dates.length ? Math.min(...dates) - 3 : today - 7;
 let end = dates.length ? Math.max(...dates) + 3 : today + 28;
 // Leave half a viewport on each side so the anchor can actually be centered,
 // even when every task is in the past/future or the month scale is very wide.
 if (anchor !== null) { const padding = Math.max(7, Math.ceil(visibleDays / 2)); start = Math.min(start, anchor - padding); end = Math.max(end, anchor + padding); }
 const weekday = (day: number) => (new Date(day * DAY_MS).getUTCDay() + 6) % 7;
 start -= weekday(start); end += 6 - weekday(end);
 return { start, end };
}
export function ganttRows(allItems: Item[], matchingItems: Item[], collapsed: Set<string>, focusId: string | null): GanttRow[] {
 const all = new Map(allItems.map(item => [item.id, item]));
 const matches = new Set(matchingItems.map(item => item.id)); const wanted = new Set(matches);
 for (const item of matchingItems) {
  let parentId = item.parentId; const seen = new Set<string>([item.id]);
  while (parentId && parentId !== focusId && !seen.has(parentId)) { seen.add(parentId); const parent = all.get(parentId); if (!parent) break; wanted.add(parentId); parentId = parent.parentId; }
 }
 const children = new Map<string | null, Item[]>();
 for (const item of allItems) { if (!wanted.has(item.id)) continue; const parent = item.parentId && wanted.has(item.parentId) ? item.parentId : null; const siblings = children.get(parent) ?? []; siblings.push(item); children.set(parent, siblings); }
 for (const siblings of children.values()) siblings.sort((a, b) => a.position - b.position || a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id));
 const rows: GanttRow[] = []; const visited = new Set<string>();
 function visit(parentId: string | null, depth: number) {
  for (const item of children.get(parentId) ?? []) { if (visited.has(item.id)) continue; visited.add(item.id); const childCount = children.get(item.id)?.length ?? 0; rows.push({ item, depth, childCount, context: !matches.has(item.id) }); if (!collapsed.has(item.id)) visit(item.id, depth + 1); }
 }
 visit(null, 0); return rows;
}
export function calendarSegments(start: number, end: number, unit: 'month' | 'week' | 'day'): { start: number; days: number; label: string }[] {
 const segments: { start: number; days: number; label: string }[] = [];
 for (let cursor = start; cursor <= end;) {
  const date = new Date(cursor * DAY_MS); let next = cursor + 1;
  if (unit === 'week') next = Math.min(end + 1, cursor + 7 - (date.getUTCDay() + 6) % 7);
  if (unit === 'month') { date.setUTCMonth(date.getUTCMonth() + 1, 1); next = Math.min(end + 1, date.getTime() / DAY_MS); }
  segments.push({ start: cursor, days: next - cursor, label: calendarLabel(cursor, unit === 'month' ? { month: 'long', year: 'numeric' } : unit === 'day' ? { weekday: 'narrow', day: 'numeric' } : { month: 'short', day: 'numeric' }) }); cursor = next;
 }
 return segments;
}

// Only direct dependency edges belong to the current selection, not its hierarchy
// or the rest of a dependency chain. Callers supply the currently drawable edges.
export function dependencyHighlights(dependencies: Dependency[], selectedId: string | null) {
 const links = new Set<string>(); const tasks = new Set<string>();
 if (selectedId) for (const dependency of dependencies) {
  if (dependency.itemId === selectedId || dependency.dependsOnId === selectedId) {
   links.add(dependency.id);
   tasks.add(dependency.itemId === selectedId ? dependency.dependsOnId : dependency.itemId);
  }
 }
 return { links, tasks };
}
