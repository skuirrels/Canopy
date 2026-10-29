import test from 'node:test';
import assert from 'node:assert/strict';
import { qualityCounts, qualityLabel } from '../src/lib/factory-quality.ts';

test('criterion coverage preserves uncovered, stale, failed and excluded results independently of run completion', () => {
 const criteria = ['pass', 'pass', 'fail', 'stale', 'unverified', 'notApplicable'].map(status => ({ status }));
 assert.deepEqual(qualityCounts(criteria), { pass: 2, fail: 1, stale: 1, unverified: 1, notApplicable: 1 });
 assert.deepEqual(qualityCounts([]), {}, 'missing criteria cannot become successful coverage');
 assert.equal(qualityLabel('notApplicable'), 'Not applicable');
 assert.equal(criteria[3].status, 'stale', 'coverage never rewrites stale evidence as pass');
});
