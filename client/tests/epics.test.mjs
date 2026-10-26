import test from 'node:test';
import assert from 'node:assert/strict';
import { componentDetails, epicGroups, resequenceComponents, specificationText } from '../src/lib/epics.ts';

const item = (id, parentId = null, statusId = 'todo', position = 0) => ({ id, title: id, parentId, statusId, position, siblingPosition: position, createdAt: '2026-01-01T00:00:00Z' });
const statuses = [{ id: 'todo', isCompleted: false }, { id: 'finished', isCompleted: true }];

test('nested task matches retain their component and ancestor path without inflating filtered completion', () => {
 const component = item('component'), branch = item('branch', 'component'), nested = item('nested', 'branch', 'finished'), hidden = item('hidden', 'component');
 const unrelated = item('other-component'); const items = [component, branch, nested, hidden, unrelated];
 const [group] = epicGroups(items, [nested], statuses, null);
 assert.equal(group.item.id, 'component'); assert.equal(group.context, true);
 assert.equal(group.taskCount, 3); assert.equal(group.completedCount, 1); assert.equal(group.matchingTaskCount, 1);
 assert.equal(group.tasks[0].item.id, 'branch'); assert.equal(group.tasks[0].context, true);
 assert.equal(group.tasks[0].children[0].item.id, 'nested');
 assert.equal(epicGroups(items, [nested], statuses, null).length, 1);
});

test('component status does not count as a completed task; an empty component remains visible', () => {
 const component = item('component', null, 'finished'), task = item('task', 'component'), empty = item('empty');
 const groups = epicGroups([component, task, empty], [component, task, empty], statuses, null);
 assert.deepEqual(groups.map(group => [group.item.id, group.taskCount, group.completedCount]), [['component', 1, 0], ['empty', 0, 0]]);
 assert.equal(epicGroups([component, task], [component], statuses, null)[0].tasks.length, 0);
});

test('focusing a component displays its immediate children as components and keeps sibling order', () => {
 const root = item('root'), a = item('a', 'root', 'todo', 3), b = item('b', 'root', 'todo', 1), grandchild = item('child', 'a');
 const items = [root, a, b, grandchild];
 const groups = epicGroups(items, [a, b, grandchild], statuses, 'root');
 assert.deepEqual(groups.map(group => group.item.id), ['b', 'a']);
 assert.deepEqual(groups[1].tasks.map(node => node.item.id), ['child']);
});

test('moving a branch between components transfers all descendant progress once without changing statuses', () => {
 const a = item('a'), b = item('b'), branch = item('branch', 'a'), child = item('child', 'branch', 'finished'); const items = [a, b, branch, child];
 const moved = items.map(task => task.id === branch.id ? { ...task, parentId: b.id } : task);
 assert.deepEqual(epicGroups(moved, moved, statuses, null).map(group => [group.item.id, group.taskCount, group.completedCount]), [['a', 0, 0], ['b', 2, 1]]);
 assert.equal(branch.parentId, 'a'); assert.equal(child.statusId, 'finished');
});

test('no matches produces an empty view without creating placeholder tasks', () => {
 const component = item('component'); const items = [component];
 assert.deepEqual(epicGroups(items, [], statuses, null), []);
 assert.deepEqual(epicGroups([], [], statuses, null), []);
 assert.equal(items.length, 1);
});

test('specification preview reads stored text, handles empty or malformed specs, and never interprets HTML', () => {
 const spec = { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: '<script>plain text</script>' }] }, { type: 'bulletList', content: [{ type: 'listItem', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Acceptance criteria' }] }] }] }] };
 assert.equal(specificationText(JSON.stringify(spec)), '<script>plain text</script> Acceptance criteria');
 assert.equal(specificationText('{'), ''); assert.equal(specificationText('{"type":"doc","content":[]}'), '');
});

test('component sequencing ignores workflow positions and preserves hidden components when filtered', () => {
 const a = { ...item('a', null, 'todo', 8), siblingPosition: 0 };
 const hidden = { ...item('hidden', null, 'finished', 0), siblingPosition: 1 };
 const b = { ...item('b', null, 'finished', 0), siblingPosition: 2 };
 const child = item('child', 'a'); const items = [b, a, hidden, child];
 assert.deepEqual(epicGroups(items, items, statuses, null).map(g => g.item.id), ['a', 'hidden', 'b']);
 assert.deepEqual(resequenceComponents(items, null, ['a', 'b'], 'a', 'b'), ['b', 'hidden', 'a']);
 assert.deepEqual(resequenceComponents(items, null, ['a', 'hidden', 'b'], 'b', 'a'), ['b', 'a', 'hidden']);
 assert.equal(resequenceComponents(items, null, ['a', 'b'], 'a', 'a'), null);
 assert.equal(resequenceComponents(items, null, ['a', 'b'], 'a', 'child'), null);
 assert.equal(a.position, 8); assert.equal(child.parentId, 'a');
});

test('expanded components show the selected task hierarchy rather than promoting children to component cards', () => {
 const root = item('component'), sibling = item('other'), branch = item('branch', 'component'), child = item('matching task', 'branch', 'finished'), hidden = item('hidden', 'component');
 const items = [root, sibling, branch, child, hidden];
 const group = componentDetails(items, [child], statuses, root.id);
 assert.equal(group.item.id, root.id);
 assert.equal(group.taskCount, 3); assert.equal(group.completedCount, 1); assert.equal(group.matchingTaskCount, 1);
 assert.deepEqual(group.tasks.map(node => node.item.id), ['branch']);
 assert.equal(group.tasks[0].context, true);
 assert.equal(group.tasks[0].children[0].item.id, child.id);
 assert.equal(componentDetails(items, [child], statuses, branch.id).tasks[0].item.id, child.id);
});

test('an expanded empty component and a filtered-out task list retain their component header and true progress', () => {
 const root = item('component'), child = item('child', root.id, 'finished'), empty = item('empty'); const items = [root, child, empty];
 const filtered = componentDetails(items, [], statuses, root.id);
 assert.deepEqual(filtered.tasks, []); assert.equal(filtered.taskCount, 1); assert.equal(filtered.completedCount, 1);
 assert.equal(componentDetails(items, [], statuses, empty.id).taskCount, 0);
 assert.equal(componentDetails(items, items, statuses, 'missing'), undefined);
});
