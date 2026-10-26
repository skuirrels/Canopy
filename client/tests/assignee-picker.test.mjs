import test from 'node:test';
import assert from 'node:assert/strict';
import { assigneeSuggestions, toggleAssignee, createdAssignee } from '../src/lib/assignee-picker.ts';

const people = [{ id: 'a', name: 'Alex Green' }, { id: 'b', name: 'Sam Lee' }];
test('inline suggestions search case-insensitively and trim input without altering workspace names', () => {
 const before = structuredClone(people);
 assert.deepEqual(assigneeSuggestions(people, '  ALEX  ').matches, [people[0]]);
 assert.deepEqual(people, before);
 assert.equal(assigneeSuggestions(people, '  Alex Green ').canCreate, false);
 assert.equal(assigneeSuggestions(people, 'alex green').canCreate, false);
 assert.equal(assigneeSuggestions(people, '   ').canCreate, false);
 assert.equal(assigneeSuggestions([], '').canCreate, false);
 assert.equal(assigneeSuggestions(people, ' Alex ').canCreate, true);
 assert.equal(assigneeSuggestions(people, ' Jo ').name, 'Jo');
});
test('selecting and removing co-assignees preserves other assignments and the original array', () => {
 const original = ['a', 'b'];
 assert.deepEqual(toggleAssignee(original, 'c'), ['a', 'b', 'c']);
 assert.deepEqual(toggleAssignee(original, 'a'), ['b']);
 assert.deepEqual(original, ['a', 'b']);
});
test('new assignment uses the actual server-created person ID, even with concurrent unrelated additions', () => {
 const added = { id: 'new-server-id', name: 'Jo' };
 assert.equal(createdAssignee(people, [...people, { id: 'other', name: 'Unrelated' }, added], ' Jo '), added);
 assert.throws(() => createdAssignee(people, people, 'Alex Green'), /could not be identified/);
 assert.throws(() => createdAssignee(people, [...people, added, { id: 'duplicate', name: 'Jo' }], 'Jo'), /could not be identified/);
});
