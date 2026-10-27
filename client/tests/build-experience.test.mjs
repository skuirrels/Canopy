import test from 'node:test';
import assert from 'node:assert/strict';
import { BUILD_SOUND_KEY, buildActivityKey, describeBuild, readBuildPreference, writeBuildPreference } from '../src/lib/build-experience.ts';

function run(state, changes = {}) {
 return { id: 'test-run', state, active: ['queued', 'running', 'cancelling'].includes(state), output: '', result: '', error: null, sessionId: null, workspacePath: null, ...changes };
}

test('the experience and sound default off, preserve explicit choices, and tolerate unavailable storage', () => {
 const data = new Map(); const storage = { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value) };
 assert.equal(readBuildPreference(storage), false);
 assert.equal(readBuildPreference(storage, BUILD_SOUND_KEY), false);
 writeBuildPreference(storage, false); assert.equal(readBuildPreference(storage), false);
 writeBuildPreference(storage, true); assert.equal(readBuildPreference(storage), true);
 writeBuildPreference(storage, true, BUILD_SOUND_KEY); assert.equal(readBuildPreference(storage, BUILD_SOUND_KEY), true);
 writeBuildPreference(storage, false, BUILD_SOUND_KEY);
 assert.equal(readBuildPreference(storage), true); assert.equal(readBuildPreference(storage, BUILD_SOUND_KEY), false);
 assert.equal(readBuildPreference({ getItem() { throw new Error('Blocked'); } }), false);
 assert.equal(readBuildPreference({ getItem() { return 'invalid'; } }), false);
 assert.doesNotThrow(() => writeBuildPreference({ setItem() { throw new Error('Blocked'); } }, false));
});

test('completion and activity come from recorded run state, never reassuring output or elapsed time', () => {
 const working = describeBuild(run('running', { output: 'All tests passed. Build complete.' }));
 assert.equal(working.running, true); assert.equal(working.succeeded, false);
 assert.equal(describeBuild(run('succeeded')).succeeded, true);
 for (const state of ['failed', 'cancelled', 'interrupted', 'cancelling', 'queued']) {
  const view = describeBuild(run(state, { result: 'Success', output: 'Done' }));
  assert.equal(view.succeeded, false); assert.equal(view.running, false);
 }
});

test('loss of polling stops the active presentation without inventing a failed or successful result', () => {
 const view = describeBuild(run('running', { sessionId: 'test-session', workspacePath: '/test/worktree' }), true);
 assert.equal(view.label, 'Waiting for connection'); assert.equal(view.running, false); assert.equal(view.succeeded, false);
 assert.deepEqual(view.evidence, [true, true, true, false]);
});

test('both provider feeds can pulse for changed bounded output of the same length', () => {
 assert.notEqual(buildActivityKey('command one'), buildActivityKey('command two'));
 assert.equal(buildActivityKey('unchanged'), buildActivityKey('unchanged'));
 for (const provider of ['codex', 'claude']) {
  const view = describeBuild(run('running', { provider, output: 'Starting\nTool: Read\n\n', sessionId: 'test-session' }));
  assert.equal(view.latestActivity, 'Tool: Read'); assert.equal(view.label, 'Agent working');
 }
});
