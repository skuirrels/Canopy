import type { Item, Person, Project, Status, Workspace } from './types.ts';
import { assignedIds, priorities } from './types.ts';
import { componentForTask } from './board.ts';
import { dateDay } from './gantt.ts';

export type AttentionTask = {
 item: Item; project?: Project; component?: Item; status?: Status;
 assignees: { id: string; person?: Person }[]; daysOverdue: number; waitingOn: number;
};
const priorityOrder = new Map([...priorities].reverse().map((priority, index) => [priority, index]));

// Due dates and configured completion flags drive this projection. Component
// status, start dates and estimated hours never substitute for a task's due date.
export function overviewTasks(workspace: Workspace, today: string) {
 const projects = new Map(workspace.projects.map(project => [project.id, project]));
 const statuses = new Map(workspace.statuses.map(status => [status.id, status]));
 const people = new Map(workspace.people.map(person => [person.id, person]));
 const items = new Map(workspace.items.map(item => [item.id, item]));
 const statusFor = (item: Item) => { const status = statuses.get(item.statusId); return status?.projectId === item.projectId ? status : undefined; };
 const tasks = workspace.items.filter(item => item.parentId !== null);
 const outstanding = tasks.filter(item => !statusFor(item)?.isCompleted);
 function row(item: Item): AttentionTask {
  return { item, project: projects.get(item.projectId), component: componentForTask(workspace.items, item), status: statusFor(item),
   assignees: assignedIds(item).map(id => ({ id, person: people.get(id) })),
   daysOverdue: item.dueDate ? Math.max(0, dateDay(today) - dateDay(item.dueDate)) : 0,
   waitingOn: workspace.dependencies.filter(link => link.itemId === item.id && (!items.has(link.dependsOnId) || !statusFor(items.get(link.dependsOnId)!)?.isCompleted)).length };
 }
 const compare = (a: AttentionTask, b: AttentionTask) => (a.item.dueDate ?? '').localeCompare(b.item.dueDate ?? '') || priorityOrder.get(a.item.priority)! - priorityOrder.get(b.item.priority)! || a.item.title.localeCompare(b.item.title) || a.item.id.localeCompare(b.item.id);
 const dueToday = outstanding.filter(item => item.dueDate === today).map(row).sort(compare);
 const overdue = outstanding.filter(item => item.dueDate && item.dueDate < today).map(row).sort(compare);
 const withoutDueDate = outstanding.filter(item => !item.dueDate).map(row).sort(compare);
 return { dueToday, overdue, withoutDueDate, taskCount: tasks.length, openCount: outstanding.length, completedCount: tasks.length - outstanding.length,
  unassignedAttention: [...dueToday, ...overdue].filter(task => task.assignees.length === 0).length };
}
