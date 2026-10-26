import test from 'node:test';
import assert from 'node:assert/strict';
import { overviewTasks } from '../src/lib/overview.ts';

const project = { id: 'project', name: 'Project', color: '#477fa1' };
const statuses = [{ id: 'open', projectId: project.id, name: 'In review', isCompleted: false }, { id: 'complete', projectId: project.id, name: 'Archived', isCompleted: true }];
const item = (id, fields = {}) => ({ id, title: id, projectId: project.id, parentId: 'component', statusId: 'open', priority: 'none', dueDate: null, startDate: null, estimatedHours: null, assigneeIds: [], assigneeId: null, ...fields });
const workspace = items => ({ projects: [project], people: [], statuses, items: [item('component', { parentId: null }), ...items], dependencies: [] });

test('overview separates due-today, past, future and undated tasks using completion flags', () => {
 const data = workspace([
  item('today', { dueDate: '2026-10-26' }), item('past', { dueDate: '2026-10-24' }),
  item('future', { dueDate: '2026-10-27' }), item('start only', { startDate: '2026-10-20' }),
  item('done', { dueDate: '2026-10-22', statusId: 'complete' }),
 ]);
 data.items[0].dueDate = '2026-10-20'; data.items[0].statusId = 'complete';
 const before = structuredClone(data), result = overviewTasks(data, '2026-10-26');
 assert.deepEqual(result.dueToday.map(x => x.item.id), ['today']);
 assert.deepEqual(result.overdue.map(x => x.item.id), ['past']);
 assert.deepEqual(result.withoutDueDate.map(x => x.item.id), ['start only']);
 assert.equal(result.taskCount, 5); assert.equal(result.openCount, 4); assert.equal(result.completedCount, 1);
 assert.equal(result.overdue[0].daysOverdue, 2);
 assert.equal(result.dueToday[0].component.id, 'component', 'completed component does not complete its task');
 assert.deepEqual(data, before);
});

test('deadline order is oldest first, then priority, with all matching tasks retained', () => {
 const data = workspace([item('later urgent', { dueDate: '2026-10-25', priority: 'urgent' }), item('old low', { dueDate: '2026-10-24', priority: 'low' }), item('old urgent', { dueDate: '2026-10-24', priority: 'urgent' }), item('today low', { dueDate: '2026-10-26', priority: 'low' }), item('today high', { dueDate: '2026-10-26', priority: 'high' })]);
 const result = overviewTasks(data, '2026-10-26');
 assert.deepEqual(result.overdue.map(x => x.item.id), ['old urgent', 'old low', 'later urgent']);
 assert.deepEqual(result.dueToday.map(x => x.item.id), ['today high', 'today low']);
});

test('both assignees and deeper task component context are preserved; assignments are never inherited', () => {
 const data = workspace([item('parent'), item('nested', { parentId: 'parent', dueDate: '2026-10-26', assigneeIds: ['one', 'two'] }), item('unassigned', { dueDate: '2026-10-25', assigneeIds: [], assigneeId: 'one' })]);
 data.people = [{ id: 'one', name: 'One' }, { id: 'two', name: 'Two' }]; data.items[0].assigneeIds = ['one'];
 const result = overviewTasks(data, '2026-10-26');
 assert.deepEqual(result.dueToday[0].assignees.map(x => x.person.name), ['One', 'Two']);
 assert.equal(result.dueToday[0].component.id, 'component');
 assert.deepEqual(result.overdue[0].assignees, []); assert.equal(result.unassignedAttention, 1);
});

test('date-only overdue days stay correct across DST and completed dependencies do not block', () => {
 const data = workspace([item('past', { dueDate: '2026-03-28' }), item('done prerequisite', { statusId: 'complete' }), item('open prerequisite')]);
 data.dependencies = [{ itemId: 'past', dependsOnId: 'done prerequisite' }, { itemId: 'past', dependsOnId: 'open prerequisite' }];
 const result = overviewTasks(data, '2026-03-30');
 assert.equal(result.overdue[0].daysOverdue, 2); assert.equal(result.overdue[0].waitingOn, 1);
});

test('missing references are exposed instead of fabricating ownership, component or completion', () => {
 const data = workspace([item('incomplete record', { parentId: 'missing', statusId: 'missing', dueDate: '2026-10-26', assigneeIds: ['missing'] })]);
 const row = overviewTasks(data, '2026-10-26').dueToday[0];
 assert.equal(row.component, undefined); assert.equal(row.status, undefined);
 assert.deepEqual(row.assignees, [{ id: 'missing', person: undefined }]);
});

test('an empty workspace produces empty groups and no synthetic dates or people', () => {
 const result = overviewTasks({ projects: [], items: [], statuses: [], people: [], dependencies: [] }, '2026-10-26');
 assert.deepEqual(result, { dueToday: [], overdue: [], withoutDueDate: [], taskCount: 0, openCount: 0, completedCount: 0, unassignedAttention: 0 });
});

test('a new calendar day moves yesterday’s deadline into overdue without changing the task', () => {
 const data = workspace([item('deadline', { dueDate: '2026-10-26' })]);
 assert.equal(overviewTasks(data, '2026-10-26').dueToday.length, 1);
 const next = overviewTasks(data, '2026-10-27');
 assert.equal(next.dueToday.length, 0); assert.equal(next.overdue[0].daysOverdue, 1);
 assert.equal(data.items[1].dueDate, '2026-10-26');
});
