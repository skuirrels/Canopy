import test from 'node:test';
import assert from 'node:assert/strict';
import { changeAgent, executionOverrides, inheritedSettings } from '../src/lib/agent-settings.ts';

const draft = { provider: 'codex', purpose: 'spec', instructions: 'Refine the brief', previousId: null, modelMode: 'inherit', model: '', effort: null };
const connection = { provider: 'codex', model: 'fixture-codex', settings: { codex: { model: 'fixture-codex', effort: 'high' }, claude: { model: 'fixture-claude', effort: 'low' }, gemini: { model: 'fixture-gemini', effort: 'medium' } } };

test('project defaults belong to the selected agent and a follow-up inherits its captured settings', () => {
 assert.deepEqual(inheritedSettings(connection, 'gemini'), { model: 'fixture-gemini', effort: 'medium' });
 assert.deepEqual(inheritedSettings(connection, 'claude'), { model: 'fixture-claude', effort: 'low' });
 assert.deepEqual(inheritedSettings(connection, 'codex', { model: 'previous-model', effort: 'medium' }), { model: 'previous-model', effort: 'medium' });
 assert.deepEqual(inheritedSettings(connection, 'claude', { model: null, effort: null }), { model: null, effort: null });
});

test('inheritance, explicit agent defaults and custom settings remain distinct in dispatch requests', () => {
 assert.deepEqual(executionOverrides(draft), { model: null, effort: null });
 assert.deepEqual(executionOverrides({ ...draft, modelMode: 'default', effort: '' }), { model: '', effort: '' });
 assert.deepEqual(executionOverrides({ ...draft, modelMode: 'custom', model: '  fixture-override  ', effort: 'low' }), { model: 'fixture-override', effort: 'low' });
 assert.throws(() => executionOverrides({ ...draft, modelMode: 'custom', model: '  ' }), /Enter a model/);
});

test('switching agents clears incompatible overrides while retaining the task instructions', () => {
 const next = changeAgent({ ...draft, modelMode: 'custom', model: 'fixture-codex', effort: 'ultra' }, 'claude');
 assert.equal(next.provider, 'claude'); assert.equal(next.modelMode, 'inherit'); assert.equal(next.model, ''); assert.equal(next.effort, null);
 assert.equal(next.instructions, draft.instructions); assert.equal(next.purpose, draft.purpose);
});
