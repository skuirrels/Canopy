import test from 'node:test';
import assert from 'node:assert/strict';
import { assignedIds, assignedPeople, fields, restoreDraft } from '../src/lib/types.ts';

test('assignment lists are authoritative and the legacy single field still resolves', () => {
 assert.deepEqual(assignedIds({assigneeIds:[],assigneeId:'legacy'}),[]);
 assert.deepEqual(assignedIds({assigneeId:'legacy'}),['legacy']);
 assert.deepEqual(assignedIds({assigneeIds:['one','two'],assigneeId:'one'}),['one','two']);
 assert.deepEqual(assignedPeople({assigneeIds:['two','one']},[{id:'one',name:'First'},{id:'two',name:'Second'}]).map(p=>p.name),['Second','First']);
});
test('date and spec saves send both assignments and retain the dependency contract', () => {
 const item = {id:'task',title:'Plan',specJson:'{}',statusId:'status',priority:'high',assigneeId:'one',assigneeIds:['one','two'],estimatedHours:2.5,startDate:null,dueDate:null};
 const edit=fields(item,[{itemId:'task',dependsOnId:'before'}]);
 assert.deepEqual(edit.assigneeIds,['one','two']);
 assert.equal(edit.estimatedHours,2.5);
 assert.equal(fields({...item,estimatedHours:0},[]).estimatedHours,0);
 assert.equal(fields({...item,estimatedHours:null},[]).estimatedHours,null);
 assert.equal(Object.hasOwn(edit,'assigneeId'),false);
 assert.deepEqual(edit.dependencyIds,['before']);
});
test('recovering a legacy spec draft retains newly added co-assignees', () => {
 const initial={assigneeIds:['one','two'],title:'Current',specJson:'current'};
 const recovered=restoreDraft(initial,{assigneeId:'one',title:'Draft',specJson:'unsaved'});
 assert.deepEqual(recovered.assigneeIds,['one','two']);
 assert.equal(recovered.specJson,'unsaved');
 assert.equal(Object.hasOwn(recovered,'assigneeId'),false);
 assert.deepEqual(restoreDraft(initial,{assigneeId:null}).assigneeIds,[]);
 assert.deepEqual(restoreDraft(initial,{assigneeIds:[]}).assigneeIds,[]);
});
