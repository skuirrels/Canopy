import { MarkdownManager } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';
import { TableKit } from '@tiptap/extension-table';
import { getSchema } from '@tiptap/core';
import { open, realpath } from 'node:fs/promises';
import { relative, resolve, extname, isAbsolute } from 'node:path';

const extensions = [StarterKit,TaskList,TaskItem.configure({nested:true}),TableKit];
const manager = new MarkdownManager({extensions});
const schema = getSchema(extensions);
export const MAX_SPEC_BYTES = 2_000_000; // Same resource limit enforced by Rules.Spec in the backend.
function checkBytes(value) { if (Buffer.byteLength(value,'utf8') > MAX_SPEC_BYTES) throw new Error('The specification is too large (maximum 2 MB, including rich-text JSON).'); }
export function parseMarkdown(markdown) {
 checkBytes(markdown);
 // The editor has no image or raw-HTML nodes. Reject them before parsing so no
 // uploaded content is silently discarded or interpreted as executable HTML.
 manager.instance.walkTokens(manager.instance.lexer(markdown),token => {
  if (token.type === 'image' || token.type === 'html') throw new Error('Images and raw HTML are not supported in task specs. Use Markdown links and text instead. No changes were saved.');
  if (token.type === 'link' && !['http:','https:','mailto:','tel:'].includes(new URL(token.href,'https://planner.invalid/').protocol)) throw new Error('Specification links must use HTTP(S), mailto, tel or a relative URL. No changes were saved.');
 });
 const document = manager.parse(markdown); schema.nodeFromJSON(document).check();
 checkBytes(JSON.stringify(document)); return document;
}
export function toMarkdown(document) { schema.nodeFromJSON(document).check(); return manager.serialize(document); }
export function specJson(markdown, previous, mode) {
 const incoming = parseMarkdown(markdown);
 const document = mode === 'append' ? {type:'doc',content:[...(JSON.parse(previous).content ?? []),...(incoming.content ?? [])]} : incoming;
 schema.nodeFromJSON(document).check();
 const json = JSON.stringify(document); checkBytes(json); return json;
}
export async function readMarkdownFile(specRoot, filePath) {
 const root = await realpath(specRoot); const file = await realpath(resolve(root,filePath));
 const inside = relative(root,file);
 if (inside === '..' || inside.startsWith(`..${process.platform === 'win32' ? '\\' : '/'}`) || isAbsolute(inside)) throw new Error('Specification files must be inside PLANNER_SPEC_ROOT, including after resolving symlinks.');
 if (!['.md','.markdown'].includes(extname(file).toLowerCase())) throw new Error('Upload a .md or .markdown file.');
 const handle = await open(file,'r');
 try {
  const stat = await handle.stat();
  if (!stat.isFile() || stat.size > MAX_SPEC_BYTES) throw new Error('Upload a regular Markdown file smaller than 2 MB.');
  const bytes = Buffer.alloc(MAX_SPEC_BYTES + 1); let length = 0;
  while (length < bytes.length) { const {bytesRead} = await handle.read(bytes,length,bytes.length-length,null); if (!bytesRead) break; length += bytesRead; }
  if (length > MAX_SPEC_BYTES) throw new Error('The Markdown file exceeds 2 MB.');
  return new TextDecoder('utf-8',{fatal:true}).decode(bytes.subarray(0,length));
 } finally { await handle.close(); }
}
