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
