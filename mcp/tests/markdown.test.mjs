import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, symlink, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseMarkdown, toMarkdown, specJson, readMarkdownFile, MAX_SPEC_BYTES } from '../src/markdown.mjs';
import { loadConfig } from '../src/config.mjs';

const sample='# Acceptance specification\n\n**Bold** and *italic* with [reference](https://example.com).\n\n- [x] Complete\n  - [ ] Nested check\n\n> Keep the evidence.\n\n```js\nconst ready = true;\n```\n\n| Requirement | Result |\n| --- | --- |\n| API | Ready |';
test('Markdown specs retain headings, formatting, nested checks, quotes, code and tables through a native document roundtrip',()=>{
 const doc=parseMarkdown(sample);const serialized=toMarkdown(doc);const roundtrip=parseMarkdown(serialized);
 for(const type of ['heading','taskList','taskItem','blockquote','codeBlock','table','link','bold','italic'])assert.ok(JSON.stringify(roundtrip).includes(`"${type}"`),type);
 assert.ok(JSON.stringify(roundtrip).includes('Nested check'));assert.ok(JSON.stringify(roundtrip).includes('const ready = true;'));
});
test('append preserves existing native content; replace removes old text; unsupported input never yields a partial spec',()=>{
 const previous=JSON.stringify(parseMarkdown('Existing **specification**'));
 assert.match(specJson('New requirements',previous,'append'),/Existing/);
 assert.doesNotMatch(specJson('New requirements',previous,'replace'),/Existing/);
 for(const md of ['![hidden](https://example.com/image.png)','<script>alert(1)</script>','[run](javascript:alert)'])assert.throws(()=>parseMarkdown(md));
 assert.ok(parseMarkdown('```html\n<script>Example, not executable.</script>\n```'));
 assert.throws(()=>parseMarkdown('x'.repeat(MAX_SPEC_BYTES+1)),/too large/);
});
test('Markdown upload confines canonical paths, rejects escaping symlinks, binary files and wrong extensions',async()=>{
 const root=await mkdtemp(join(tmpdir(),'planner-spec-root-'));const outside=await mkdtemp(join(tmpdir(),'planner-spec-outside-'));
 try{
  await writeFile(join(root,'spec.md'),'# Valid');await writeFile(join(outside,'outside.md'),'Private');
  await symlink(join(outside,'outside.md'),join(root,'escape.md'));
  assert.equal(await readMarkdownFile(root,'spec.md'),'# Valid');
  await assert.rejects(readMarkdownFile(root,join(outside,'outside.md')),/inside/);
  await assert.rejects(readMarkdownFile(root,'escape.md'),/inside/);
  await writeFile(join(root,'binary.md'),Buffer.from([0xff,0xfe]));await assert.rejects(readMarkdownFile(root,'binary.md'));
  await writeFile(join(root,'wrong.txt'),'text');await assert.rejects(readMarkdownFile(root,'wrong.txt'),/markdown/i);
 }finally{await rm(root,{recursive:true,force:true});await rm(outside,{recursive:true,force:true});}
});
test('MCP configuration permits only loopback backends and validates timeout/read-only settings',()=>{
 assert.equal(loadConfig({PLANNER_URL:'http://127.0.0.1:5275',PLANNER_MCP_READ_ONLY:'true'}).readOnly,true);
 for(const PLANNER_URL of ['https://external.example/','file:///tmp/data','http://user:secret@localhost/','http://localhost/?token=secret'])assert.throws(()=>loadConfig({PLANNER_URL}));
 assert.throws(()=>loadConfig({PLANNER_MCP_READ_ONLY:'yes'}));assert.throws(()=>loadConfig({PLANNER_TIMEOUT_MS:'0'}));
});
