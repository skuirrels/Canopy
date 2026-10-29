import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { stageWouldCycle, setStageDependency, removeFactoryStage, moveFactoryStep, recoveredDraft, patchOverride, resolveFactoryOverrides, taskFactorySignature, configurationFingerprint, pipelineDraftState, identifiers } from '../src/lib/factory-definition.ts';

const templates = JSON.parse(readFileSync(new URL('../../server/Planner.Api/factory-templates.json', import.meta.url), 'utf8')).FactoryTemplates;
const implementation = () => structuredClone(templates.find(template => template.id === 'standard-implementation').definition);

test('dependency editing rejects a cycle regardless of display order and retains valid cross-group links', () => {
 const definition = implementation(); const before = structuredClone(definition);
 assert.equal(stageWouldCycle(definition, 'handoff', 'prepare'), true);
 assert.equal(stageWouldCycle({ ...definition, stages: [...definition.stages].reverse() }, 'handoff', 'prepare'), true);
 assert.throws(() => setStageDependency(definition, 'handoff', 'prepare', true), /cycle/);
 assert.equal(stageWouldCycle(definition, 'prepare', 'review'), false);
 const changed = setStageDependency(definition, 'prepare', 'review', true);
 assert.deepEqual(changed.stages.find(stage => stage.id === 'review').dependsOn, ['verify', 'prepare']);
 assert.deepEqual(definition, before);
 assert.equal(stageWouldCycle(definition, 'missing', 'prepare'), true);
});

test('removing a stage clears dependency connections without inventing a replacement artifact producer', () => {
 const definition = implementation(), original = structuredClone(definition);
 const changed = removeFactoryStage(definition, 'verify');
 assert.equal(changed.stages.some(stage => stage.id === 'verify'), false);
 assert.deepEqual(changed.stages.find(stage => stage.id === 'review').dependsOn, []);
 assert.deepEqual(changed.stages.find(stage => stage.id === 'review').steps[0].consumes, ['changes', 'verification']);
 assert.deepEqual(definition, original);
});

test('step reordering preserves all configured settings and never reorders stage dependencies', () => {
 const definition = implementation(); const stage = definition.stages[1];
 const second = { ...structuredClone(stage.steps[0]), id: 'second', name: 'Second implementation', skills: [{ id: 'project:review', source: 'fixture caller input' }] };
 stage.steps.push(second); const before = structuredClone(stage);
 const changed = moveFactoryStep(stage, second.id, -1);
 assert.deepEqual(changed.steps, [second, before.steps[0]]);
 assert.deepEqual(changed.dependsOn, before.dependsOn); assert.deepEqual(stage, before);
 assert.deepEqual(moveFactoryStep(stage, second.id, 1), stage);
});

test('recovery retains its original revision and rejects malformed browser documents', () => {
 const value = { name: 'Unsaved', description: '', definition: implementation(), baseRevision: 7 };
 assert.deepEqual(recoveredDraft(JSON.stringify(value)), value);
 for (const malformed of [null, 'broken', '{}', JSON.stringify({ ...value, baseRevision: '7' }), JSON.stringify({ ...value, definition: { stages: [null] } })]) assert.equal(recoveredDraft(malformed), null);
});

test('task overrides preserve inheritance, explicit defaults and cleared skills without modifying a published definition', () => {
 const definition = implementation(), original = structuredClone(definition);
 const configured = definition.stages[1].steps[0]; configured.model = 'project-model'; configured.skills = [{ id: 'project:build', source: 'fixture caller input' }];
 const published = structuredClone(definition);
 let overrides = patchOverride({ steps: [] }, 'build', 'implement', { model: '', skills: [] });
 const resolved = resolveFactoryOverrides(definition, overrides).stages[1].steps[0];
 assert.equal(resolved.model, ''); assert.deepEqual(resolved.skills, []);
 overrides = patchOverride(overrides, 'build', 'implement', { model: null, skills: null });
 assert.deepEqual(overrides.steps, []);
 assert.deepEqual(resolveFactoryOverrides(definition, overrides), published);
 assert.deepEqual(definition.stages[0], original.stages[0]); assert.deepEqual(definition, published);
});

test('new published versions do not make a pinned task dirty, while changed task overrides invalidate its baseline', () => {
 const value = { configuration: { pipelineVersionId: 'pinned-version', overrides: { steps: [] } }, updateAvailable: false };
 assert.equal(taskFactorySignature(value), taskFactorySignature({ ...value, updateAvailable: true }));
 assert.notEqual(taskFactorySignature(value), taskFactorySignature({ ...value, configuration: { ...value.configuration, overrides: { steps: [{ stageId: 'build', stepId: 'implement', instructions: 'External edit' }] } } }));
});

test('artifact editing retains incomplete separators so missing IDs reach validation instead of being hidden', () => {
 assert.deepEqual(identifiers('changes, '), ['changes', '']);
 assert.deepEqual(identifiers('changes, verification'), ['changes', 'verification']);
 assert.deepEqual(identifiers(''), []);
});

test('saving preserves validation and clears dirty state when the server adds optional nulls or changes property order', () => {
 const edited = { stages: [{ id: 'write', steps: [{ id: 'step', instructions: 'Caller input' }] }], schemaVersion: 1 };
 const saved = { schemaVersion: 1, stages: [{ steps: [{ command: null, instructions: 'Caller input', id: 'step', model: null }], id: 'write' }] };
 assert.equal(configurationFingerprint(edited), configurationFingerprint(saved));
 assert.notEqual(configurationFingerprint(edited), configurationFingerprint({ ...edited, schemaVersion: 2 }));
 assert.notEqual(configurationFingerprint({ model: '' }), configurationFingerprint({ model: null }));
 assert.notEqual(configurationFingerprint({ skills: [] }), configurationFingerprint({ skills: null }));
});

test('a recovery copy that already reached the server adopts the saved revision while genuinely stale edits retain their original revision', () => {
 const pipeline = { name: 'Saved', description: '', definition: implementation(), draftRevision: 5 };
 const recovery = { name: pipeline.name, description: pipeline.description, definition: structuredClone(pipeline.definition), baseRevision: 4 };
 assert.equal(pipelineDraftState(pipeline, recovery).baseRevision, 5);
 const stale = { ...recovery, description: 'Still unsaved' };
 assert.equal(pipelineDraftState(pipeline, stale).baseRevision, 4);
 assert.equal(pipelineDraftState(pipeline, stale).draft.description, stale.description);
});
