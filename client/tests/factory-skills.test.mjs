import test from 'node:test';
import assert from 'node:assert/strict';
import { skillKey, skillMatches, skillSelectionStatus } from '../src/lib/factory-skills.ts';
const entry = { id:'review', source:'project', paths:['/explicit/skills/review/SKILL.md'], hash:'observed', title:'Review code', state:'available', error:null, origins:['root'] };
const catalogue = { entries:[entry], scopes:[{ source:'project', complete:true }], complete:true };
test('incomplete source coverage cannot certify a readable matching skill', () => {
 assert.equal(skillSelectionStatus(entry, { ...catalogue, scopes:[{source:'project',complete:false,error:'directory bound'}] }).state,'unavailable');
});
test('references require the exact source and a current observation', () => {
 assert.equal(skillSelectionStatus(entry,null).state,'unverified');
 assert.equal(skillSelectionStatus({...entry,source:'different'},catalogue).state,'unavailable');
 assert.equal(skillSelectionStatus(entry,catalogue).state,'available');
 assert.notEqual(skillKey({id:'a/b',source:'c'}),skillKey({id:'a',source:'b/c'}));
});
test('catalogue search includes observed paths and display titles', () => {
 assert.equal(skillMatches(entry,' /EXPLICIT/SKILLS '),true);
 assert.equal(skillMatches(entry,'review code'),true);
 assert.equal(skillMatches(entry,'unknown package'),false);
});
