import test from 'node:test';
import assert from 'node:assert/strict';
import { needsAttention, phaseLabel, workingPhases } from '../src/lib/factory-build-phases.ts';

const phases = ['starting', 'writing', 'testing', 'checking', 'yourTurn', 'kept', 'changesRequested', 'finished', 'failed', 'stopped'];

test('every build phase has a plain label', () => {
 assert.deepEqual(Object.keys(phaseLabel).sort(), [...phases].sort());
 for (const phase of phases) assert.match(phaseLabel[phase], /^[A-Z][a-z ]+$/);
});

test('working, waiting and failed builds need attention; finished ones do not', () => {
 assert.deepEqual(phases.filter(needsAttention), ['starting', 'writing', 'testing', 'checking', 'yourTurn', 'failed']);
 assert.deepEqual([...workingPhases], ['starting', 'writing', 'testing', 'checking']);
});
