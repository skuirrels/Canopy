export type Project = { id: string; name: string; description: string; color: string; revision: number; createdAt: string };
export type Status = { id: string; projectId: string; name: string; color: string; position: number; isCompleted: boolean };
export type Person = { id: string; name: string };
export type Priority = 'none' | 'low' | 'medium' | 'high' | 'urgent';
export type Item = { id: string; projectId: string; parentId: string | null; statusId: string; title: string; specJson: string; priority: Priority; assigneeId: string | null; assigneeIds: string[]; estimatedHours: number | null; startDate: string | null; dueDate: string | null; position: number; siblingPosition: number; x: number | null; y: number | null; createdAt: string; updatedAt: string };
export type Dependency = { id: string; projectId: string; itemId: string; dependsOnId: string };
export type Workspace = { projects: Project[]; items: Item[]; statuses: Status[]; dependencies: Dependency[]; people: Person[] };
export type EditFields = Pick<Item, 'title' | 'specJson' | 'statusId' | 'priority' | 'assigneeIds' | 'estimatedHours' | 'startDate' | 'dueDate'> & { dependencyIds: string[] };
export const emptyWorkspace: Workspace = { projects: [], items: [], statuses: [], dependencies: [], people: [] };
export const priorities: Priority[] = ['none', 'low', 'medium', 'high', 'urgent'];
export function descendants(items: Item[], id: string): Set<string> {
  const result = new Set<string>(); const queue = [id];
  while (queue.length) { const current = queue.shift()!; for (const item of items) if (item.parentId === current && !result.has(item.id)) { result.add(item.id); queue.push(item.id); } }
  return result;
}
export function fields(item: Item, dependencies: Dependency[]): EditFields {
  const { title, specJson, statusId, priority, startDate, dueDate } = item;
  return { title, specJson, statusId, priority, assigneeIds: assignedIds(item), estimatedHours: item.estimatedHours ?? null, startDate, dueDate, dependencyIds: dependencies.filter(d => d.itemId === item.id).map(d => d.dependsOnId) };
}
export function initials(name: string) { return name.trim().split(/\s+/).slice(0, 2).map(s => s[0]).join('').toUpperCase(); }
export function dateLabel(date: string) { return new Date(date + 'T12:00:00').toLocaleDateString(undefined, { month: 'short', day: 'numeric' }); }
export function overdue(item: Item, statuses: Status[]) { return !!item.dueDate && item.dueDate < new Date().toLocaleDateString('en-CA') && !statuses.find(s => s.id === item.statusId)?.isCompleted; }

// Older clients/backends expose the first assignee only. New arrays are authoritative,
// including an explicit empty array. This also allows existing browser drafts to migrate.
export function assignedIds(item: { assigneeIds?: string[]; assigneeId?: string | null }): string[] {
 return item.assigneeIds ?? (item.assigneeId ? [item.assigneeId] : []);
}
export function assignedPeople(item: { assigneeIds?: string[]; assigneeId?: string | null }, people: Person[]): Person[] {
 return assignedIds(item).map(id => people.find(person => person.id === id)).filter((person): person is Person => !!person);
}
export function restoreDraft(initial: EditFields, stored: Partial<EditFields> & { assigneeId?: string | null }): EditFields {
 const { assigneeId, ...values } = stored;
 const ids = values.assigneeIds ?? (assigneeId !== undefined && assigneeId !== (initial.assigneeIds[0] ?? null) ? (assigneeId ? [assigneeId] : []) : initial.assigneeIds);
 return { ...initial, ...values, assigneeIds: ids };
}
