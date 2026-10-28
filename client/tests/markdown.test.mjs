import test from 'node:test';
import assert from 'node:assert/strict';
import { parseMarkdown, specJson, toMarkdown } from '../src/lib/markdown.ts';

test('browser imports preserve editable formatting through a Markdown roundtrip', () => {
 const markdown = '# Specification\n\n**Objective** with [context](https://example.invalid).\n\n- [ ] First check\n  - [x] Nested check\n\n| Input | Outcome |\n| --- | --- |\n| Empty | No words |\n\n```python\ncount_words(text)\n```';
 const document = parseMarkdown(markdown);
 assert.deepEqual(parseMarkdown(toMarkdown(document)), document);
 assert.match(toMarkdown(document), /Nested check/);
});

test('reviewed append retains existing content while replace uses only the new work order', () => {
 const previous = JSON.stringify(parseMarkdown('## Existing\n\nKeep the user’s wording.'));
 const appended = toMarkdown(JSON.parse(specJson('## Additional\n\nNew acceptance criterion.', previous, 'append')));
 assert.match(appended, /Keep the user’s wording/);
 assert.match(appended, /New acceptance criterion/);
 const replaced = toMarkdown(JSON.parse(specJson('## Replacement', previous, 'replace')));
 assert.doesNotMatch(replaced, /Existing|wording/);
 assert.throws(() => specJson('text', previous, 'invalid'), /append or replace/);
});

test('unsupported imports fail before replacing a specification', () => {
 for (const markdown of ['<script>alert(1)</script>', '![image](image.png)', '[unsafe](javascript:alert%281%29)'])
  assert.throws(() => parseMarkdown(markdown));
});

test('styled inline code retains emphasis and links through import, export, append and replace', () => {
 const markdown = '**`bold_code`** and *`italic_code`* and ~~`struck_code`~~ and ***`both_styles`*** and [`linked_code`](index.html).';
 const document = parseMarkdown(markdown);
 const text = document.content[0].content.filter(node => node.marks?.some(mark => mark.type === 'code'));
 assert.deepEqual(text.map(node => node.marks.map(mark => mark.type).sort()), [
  ['bold', 'code'], ['code', 'italic'], ['code', 'strike'], ['bold', 'code', 'italic'], ['code', 'link'],
 ]);
 assert.equal(toMarkdown(document).trim(), markdown);
 assert.deepEqual(parseMarkdown(toMarkdown(document)), document);
 const previous = JSON.stringify(parseMarkdown('Keep existing wording.'));
 const appended = toMarkdown(JSON.parse(specJson(markdown, previous, 'append')));
 assert.ok(appended.includes('Keep existing wording.'));
 assert.ok(appended.includes(markdown));
 assert.equal(toMarkdown(JSON.parse(specJson(markdown, previous, 'replace'))).trim(), markdown);
});
