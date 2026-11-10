import test from 'node:test';
import assert from 'node:assert/strict';
import { blankNote, noteExcerpt, noteFileName, noteLabel, notesFor, sortNotes } from '../src/lib/notes.ts';

const note = (id, heldOn, createdAt, itemId = 'component') => ({ id, projectId: 'p', itemId, title: '', heldOn, markdown: '', createdAt, updatedAt: createdAt });

test('notes list the latest call first and undated notes last', () => {
 const ordered = sortNotes([note('old', '2026-10-01', '2026-10-01T09:00'), note('undated', null, '2026-11-09T09:00'), note('new', '2026-11-05', '2026-11-05T09:00'), note('same-day-later', '2026-11-05', '2026-11-05T15:00')]);
 assert.deepEqual(ordered.map(value => value.id), ['same-day-later', 'new', 'old', 'undated']);
});

test('only the requested component’s notes are returned', () => {
 assert.deepEqual(notesFor([note('a', null, '1'), note('b', null, '2', 'other')], 'component').map(value => value.id), ['a']);
 assert.deepEqual(notesFor(undefined, 'component'), []);
});

test('labels and excerpts come from the note, never invented content', () => {
 assert.equal(noteLabel({ title: '  Call with Aleks ', heldOn: '2026-11-05', markdown: '' }), 'Call with Aleks');
 assert.match(noteLabel({ title: '', heldOn: '2026-11-05', markdown: '' }), /^Notes from /);
 assert.equal(noteLabel({ title: '', heldOn: null, markdown: '' }), 'Untitled note');
 assert.equal(noteExcerpt('\n# Attendees\n- [x] Aleks'), 'Attendees');
 assert.equal(noteExcerpt('- **Agreed** to use [AeroDataBox](https://example.invalid)'), 'Agreed to use AeroDataBox');
 assert.equal(noteExcerpt('x'.repeat(200), 10).length, 10);
 assert.ok(blankNote({ title: ' ', heldOn: '2026-11-05', markdown: '\n' }));
 assert.ok(!blankNote({ title: '', heldOn: null, markdown: 'text' }));
});

test('downloaded file names are safe and dated', () => {
 assert.equal(noteFileName({ title: 'Call: Aleks / C&C', heldOn: '2026-11-05', markdown: '' }), '2026-11-05-call-aleks-c-c.md');
 assert.equal(noteFileName({ title: '', heldOn: null, markdown: '' }), 'note.md');
});

test('rich-text Markdown is made readable without changing the parsed document', async () => {
 const { parseMarkdown, toMarkdown } = await import('../src/lib/markdown.ts');
 const { readableMarkdown } = await import('../src/lib/notes.ts');
 const cases = ['C&C and Q&A', 'literal &lt; text and &amp;amp;', 'if a < b and c > d', 'code `a &amp; b` and `x & y`', '```\na & b &amp; c\n```\n\nafter & code', '> quote & more', '- [ ] Send C&C schedule'];
 for (const markdown of cases) {
  const serialized = toMarkdown(parseMarkdown(markdown)); const readable = readableMarkdown(serialized);
  assert.deepEqual(parseMarkdown(readable), parseMarkdown(serialized), markdown);
 }
 assert.equal(readableMarkdown(toMarkdown(parseMarkdown('C&C and Q&A'))), 'C&C and Q&A');
 assert.equal(readableMarkdown(toMarkdown(parseMarkdown('if a < b and c > d'))), 'if a < b and c > d');
 assert.match(readableMarkdown('literal &amp;lt; stays'), /&amp;lt;/);
});
