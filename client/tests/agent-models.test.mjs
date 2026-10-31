import test from 'node:test';
import assert from 'node:assert/strict';
import { catalogueNote, modelLabel, parseSelectValue, selectValue } from '../src/lib/agent-models.ts';

const models = [{ id: 'fixture-fast', name: 'Fixture Fast' }, { id: 'fixture-deep', name: 'Fixture Deep' }];

test('listed models, presets and free-text entries map to distinct select values', () => {
 assert.equal(selectValue({ preset: 'inherit' }, models, false), 'preset:inherit');
 assert.equal(selectValue({ model: 'fixture-deep' }, models, false), 'model:fixture-deep');
 assert.equal(selectValue({ model: 'unlisted-model' }, models, false), 'other');
 assert.equal(selectValue({ model: 'fixture-deep' }, models, true), 'other', 'typing a listed ID keeps the free-text field open');
 assert.equal(selectValue({ model: 'preset:inherit' }, models, false), 'other', 'a model ID cannot impersonate a preset');
});

test('select values round-trip back to the chosen preset or model', () => {
 assert.deepEqual(parseSelectValue('preset:default'), { preset: 'default' });
 assert.deepEqual(parseSelectValue('model:fixture-fast'), { model: 'fixture-fast' });
 assert.deepEqual(parseSelectValue('model:org/model:v2'), { model: 'org/model:v2' });
 assert.equal(parseSelectValue('other'), 'other');
});

test('the catalogue note explains where models came from and when to type one', () => {
 assert.equal(catalogueNote(null), 'Loading models…');
 assert.equal(catalogueNote({ provider: 'codex', source: 'cli', models, error: null }), '');
 assert.match(catalogueNote({ provider: 'claude', source: 'configuration', models, error: null }), /configuration/);
 assert.match(catalogueNote({ provider: 'gemini', source: 'none', models: [], error: 'The agent could not list models.' }), /could not list models\. Choose Other model/);
 assert.match(catalogueNote({ provider: 'gemini', source: 'none', models: [], error: null }), /does not list models/);
});

test('model labels show the ID behind a name and the exact model a previous run used', () => {
 assert.equal(modelLabel({ id: 'fixture-fast', name: 'fixture-fast' }), 'fixture-fast');
 assert.equal(modelLabel({ id: 'opus', name: 'Opus, latest' }), 'Opus, latest (opus)');
 assert.equal(modelLabel({ id: 'opus', name: 'Opus, latest', lastRan: 'fixture-opus-resolved' }), 'Opus, latest (opus) · last ran as fixture-opus-resolved');
});
