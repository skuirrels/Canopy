import test from 'node:test';
import assert from 'node:assert/strict';
import { parsePlan } from '../../shared/plan-import.mjs';

test('extracts requirement tables with criteria, explicit dependencies and source priority', () => {
 const {items} = parsePlan('# Example\n## 1. Storage\n| ID | Requirement | Acceptance | Pri | Dependencies |\n|---|---|---|---|---|\n| DB-1 | Store `A|B` correctly | Reads the same value | P0 | |\n| DB-2 | Query data | Query returns records | P1 | DB-1 |','example.md');
 assert.equal(items.length,3); assert.equal(items[0].title,'Storage');
 assert.equal(items[1].acceptance,'Reads the same value'); assert.equal(items[1].sourcePriority,'P0');
 assert.equal(items[1].priority,'none'); assert.deepEqual(items[2].dependencyKeys,['DB-1']);
 assert.ok(items[1].markdown.includes('`A|B`'));
});

test('preserves nested checklist parents, steps and explicit supporting text', () => {
 const {items} = parsePlan('## Work\n- [ ] Parent\n  - [x] Child\n- [ ] Sibling\nAcceptance: An explicit outcome\n1. Deploy\nAcceptance: Service responds\n','work.md');
 assert.equal(items[2].parentKey,items[1].key); assert.equal(items[3].parentKey,items[0].key);
 assert.equal(items[4].acceptance,'Service responds'); assert.ok(!('statusId' in items[2]));
});

test('does not convert evidence tables or code samples into runtime work', () => {
 const result=parsePlan('## Evidence\n| ID | Finding |\n|---|---|\n| E1 | An observation |\n```md\n- [ ] Fake task\n```\n## Actual work\n1. Explicit step','source.md');
 assert.equal(result.items.length,2); assert.ok(!result.items.some(i=>i.title.includes('Fake')));
 assert.throws(()=>parsePlan('## Context\nNarrative only.','source.md'),/No structured tasks/);
});

test('rejects duplicate identities and reports unresolved dependencies and missing acceptance',()=>{
 assert.throws(()=>parsePlan('## Work\n| ID | Task |\n|---|---|\n| T-1 | One |\n| T-1 | Two |','work.md'),/Duplicate plan ID/);
 const result=parsePlan('## Work\n| ID | Task | Dependencies |\n|---|---|---|\n| T-1 | One | T-9 |','work.md');
 assert.ok(result.warnings.some(w=>w.includes('unresolved'))); assert.ok(result.warnings.some(w=>w.includes('no explicit acceptance')));
});

test('long task titles retain their full specification',()=>{
 const title='Preserve a long user requirement '.repeat(12);
 const result=parsePlan('## Work\n1. '+title,'work.md');
 assert.ok(result.items[1].title.length<=200); assert.ok(result.items[1].markdown.includes(title.trim()));
});

test('preserves inline-code acceptance so generic type and XML examples remain safe Markdown',async()=>{
 const {parseMarkdown}=await import('../src/lib/markdown.ts');
 const result=parsePlan('## Work\n| ID | Requirement | Acceptance |\n|---|---|---|\n| XML-1 | Navigate MSBuild | Resolve `<Target Name="Build">` |','work.md');
 assert.equal(result.items[1].acceptance,'Resolve `<Target Name="Build">`');
 assert.doesNotThrow(()=>parseMarkdown(result.items[1].markdown+'\n\n## Acceptance\n'+result.items[1].acceptance));
});
