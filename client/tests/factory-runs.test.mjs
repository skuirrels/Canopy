import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mergeFactoryRunHistory, requireFactoryRunDetail } from '../src/lib/factory-runs.ts';

test('a delayed history response cannot replace a newer execution state or reuse another run state', () => {
 const id = randomUUID(), second = randomUUID(), itemId = randomUUID();
 const saved = { id, itemId, revision: 9, state: 'waiting', title: 'Captured test input', pipelineName: 'Test pipeline', versionNumber: 1, createdAt: new Date().toISOString() };
 const stale = { ...saved, revision: 2, state: 'queued' }, other = { ...saved, id: second, revision: 1, state: 'prepared' };
 assert.deepEqual(mergeFactoryRunHistory([stale, other], [saved]), [saved, other]);
 assert.deepEqual(mergeFactoryRunHistory([{ ...saved, revision: 12, state: 'succeeded' }], [saved]), [{ ...saved, revision: 12, state: 'succeeded' }]);
 assert.deepEqual(mergeFactoryRunHistory([], [saved]), [], 'deleted history is not reintroduced from the cache');
 assert.equal(stale.state, 'queued', 'server response objects stay unchanged');
});

test('a preparation-only backend is reported as an explicit runtime protocol gap', () => {
 const prepared = { run: { id: randomUUID(), state: 'prepared', snapshot: { environment: { definition: { stages: [] } } } }, stages: [], steps: [] };
 assert.throws(() => requireFactoryRunDetail(prepared), /Restart the updated Canopy backend/);
 assert.throws(() => requireFactoryRunDetail({ ...prepared, run: { ...prepared.run, revision: 1, active: false } }), /cannot supply factory runtime data/);
});
