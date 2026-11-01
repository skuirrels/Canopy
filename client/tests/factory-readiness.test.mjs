import test from 'node:test';
import assert from 'node:assert/strict';
import { taskReadiness, openPrerequisites, needsSpecification } from '../src/lib/factory-readiness.ts';

const statuses = [{ id: 'todo', isCompleted: false }, { id: 'done', isCompleted: true }];
const spec = JSON.stringify({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Build it' }] }] });
const item = (id, statusId = 'todo', specJson = spec) => ({ id, statusId, specJson, title: id });

test('a task waits on prerequisites that are not in a completed column', () => {
 const items = [item('a'), item('b', 'done'), item('c')];
 const graph = { items, statuses, dependencies: [{ itemId: 'c', dependsOnId: 'a' }, { itemId: 'c', dependsOnId: 'b' }] };
 assert.deepEqual(openPrerequisites(items[2], graph).map(x => x.id), ['a']);
 const readiness = taskReadiness(items[2], graph, [], true);
 assert.equal(readiness.state, 'waiting'); assert.equal(readiness.label, 'Waiting on 1 task');
});
test('done, building, specification and ready states', () => {
 const graph = { items: [], statuses, dependencies: [] };
 assert.equal(taskReadiness(item('x', 'done'), graph, [], true).state, 'done');
 assert.equal(taskReadiness(item('x'), graph, [{ itemId: 'x', state: 'prepared', createdAt: '2026-11-01' }], true).label, 'Prepared · ready to start');
 assert.equal(taskReadiness(item('x'), graph, [{ itemId: 'x', state: 'succeeded', createdAt: '2026-11-01' }], true).state, 'ready');
 assert.equal(taskReadiness(item('x', 'todo', '{}'), graph, [], true).state, 'needsSpecification');
 assert.equal(taskReadiness(item('x', 'todo', '{}'), graph, [], false).state, 'ready');
});
test('only implementation workflows need a specification', () => {
 assert.equal(needsSpecification({ stages: [{ steps: [{ kind: 'agent', role: 'implementation' }] }] }), true);
 assert.equal(needsSpecification({ stages: [{ steps: [{ kind: 'command' }] }] }), false);
});
