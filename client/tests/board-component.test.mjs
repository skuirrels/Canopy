import test from 'node:test';
import assert from 'node:assert/strict';
import { componentForTask } from '../src/lib/board.ts';
const item = (id, parentId = null, projectId = 'project') => ({ id, title: id, parentId, projectId });

test('tasks and deeper subtasks retain their top-level component label', () => {
 const component = item('Component'); const task = item('Task', component.id); const subtask = item('Subtask', task.id);
 const items = [component, task, subtask], before = structuredClone(items);
 assert.equal(componentForTask(items, task), component);
 assert.equal(componentForTask(items, subtask), component);
 assert.equal(componentForTask(items, component), undefined);
 assert.deepEqual(items, before);
});

test('component labels follow hierarchy when tasks from multiple components share a board column', () => {
 const first = item('First component'), second = item('Second component');
 const firstTask = item('First task', first.id), secondTask = item('Second task', second.id);
 const items = [secondTask, first, firstTask, second];
 assert.equal(componentForTask(items, firstTask), first);
 assert.equal(componentForTask(items, secondTask), second);
});

test('missing, foreign and cyclic parent paths expose an unavailable component', () => {
 const orphan = item('Orphan', 'missing'), foreign = item('Foreign', null, 'other'), wrong = item('Wrong project', foreign.id);
 const a = item('A', 'B'), b = item('B', 'A');
 assert.equal(componentForTask([orphan], orphan), undefined);
 assert.equal(componentForTask([foreign, wrong], wrong), undefined);
 assert.equal(componentForTask([a, b], a), undefined);
});
