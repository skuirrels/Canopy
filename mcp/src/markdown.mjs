import { MarkdownManager } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';
import { TableKit } from '@tiptap/extension-table';
import { getSchema } from '@tiptap/core';
import { open, realpath } from 'node:fs/promises';
import { relative, resolve, extname, isAbsolute } from 'node:path';
import { createSpecCodec, createSpecStarterKit, MAX_SPEC_BYTES } from '../../shared/spec-markdown.mjs';

const extensions = [createSpecStarterKit(StarterKit),TaskList,TaskItem.configure({nested:true}),TableKit];
const manager = new MarkdownManager({extensions});
const schema = getSchema(extensions);
export { MAX_SPEC_BYTES };
export const { parseMarkdown, toMarkdown, specJson } = createSpecCodec(manager, schema);
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
