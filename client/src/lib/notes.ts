import type { ComponentNote } from './types.ts';
import { dateLabel } from './types.ts';

export type NoteFields = Pick<ComponentNote, 'title' | 'heldOn' | 'markdown'>;
export const noteFields = (note: ComponentNote): NoteFields => ({ title: note.title, heldOn: note.heldOn, markdown: note.markdown });
export const blankNote = (fields: NoteFields) => !fields.title.trim() && !fields.markdown.trim();

// Newest call first. Undated notes follow dated ones, newest created first.
export function sortNotes(notes: ComponentNote[]): ComponentNote[] {
 return [...notes].sort((a, b) => (b.heldOn ?? '').localeCompare(a.heldOn ?? '') || b.createdAt.localeCompare(a.createdAt));
}
export function notesFor(notes: ComponentNote[] | undefined, itemId: string): ComponentNote[] {
 return sortNotes((notes ?? []).filter(note => note.itemId === itemId));
}
export function noteLabel(note: NoteFields): string {
 return note.title.trim() || (note.heldOn ? `Notes from ${dateLabel(note.heldOn)}` : 'Untitled note');
}
// First line of prose, without Markdown markers, for list and card previews.
export function noteExcerpt(markdown: string, length = 120): string {
 const line = markdown.split('\n').map(value => value.replace(/^\s*(#{1,6}\s+|[-*+]\s+(\[[ xX]\]\s+)?|\d+[.)]\s+|>\s*)/, '').replace(/[*_`~]/g, '').replace(/\[([^\]]*)\]\([^)]*\)/g, '$1').trim()).find(Boolean) ?? '';
 return line.length > length ? line.slice(0, length - 1).trimEnd() + '…' : line;
}
export function noteFileName(note: NoteFields): string {
 const stem = [note.heldOn, note.title.trim()].filter(Boolean).join(' ').replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-+|-+$/g, '').toLowerCase();
 return (stem || 'note') + '.md';
}

// The rich-text serializer escapes &, < and > in prose (C&C becomes C&amp;C). Undo that
// outside code so the Markdown stays readable. Callers keep the result only if it parses
// back to the same document, so readability never changes meaning.
export function readableMarkdown(markdown: string): string {
 const prose = (text: string) => text.replace(/&amp;(?![A-Za-z0-9#]+;)/g, '&').replace(/(^|[ \t])&lt;(?=[ \t=]|$)/gm, '$1<').replace(/([^ \t\n])?([ \t])&gt;(?=[ \t=]|$)/g, (match, before: string | undefined, space: string) => before === undefined ? match : `${before}${space}>`);
 const inline = (text: string) => text.split(/(`+[^`]*?`+)/).map((part, index) => index % 2 ? part : prose(part)).join('');
 return markdown.split(/(^```[^\n]*\n[\s\S]*?^```[^\n]*$)/m).map((part, index) => index % 2 ? part : inline(part)).join('');
}
