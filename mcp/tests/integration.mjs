import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { readFile, writeFile } from 'node:fs/promises';
import { Client } from '@modelcontextprotocol/client';
import { StdioClientTransport } from '@modelcontextprotocol/client/stdio';

const base=process.env.PLANNER_TEST_URL;
if(!base)throw new Error('Set PLANNER_TEST_URL to an isolated test backend. This suite creates and removes test data.');
const root=fileURLToPath(new URL('../',import.meta.url));
const transport=new StdioClientTransport({command:process.execPath,args:[`${root}src/index.mjs`],env:{...process.env,PLANNER_URL:base,PLANNER_SPEC_ROOT:`${root}examples`},stderr:'pipe'});
const client=new Client({name:'planner-mcp-regression',version:'1.0.0'});await client.connect(transport);
let count=0,pid,personId,secondPersonId;const fixtureName=`MCP validation ${crypto.randomUUID()}`;
function check(value,message){assert.ok(value,message);console.log(`PASS ${++count}: ${message}`);}
async function call(name,args={},error=false){const r=await client.callTool({name,arguments:args});assert.equal(!!r.isError,error,JSON.stringify(r));return r.structuredContent??JSON.parse(r.content[0].text);}
async function workspace(){return await(await fetch(`${base}/api/workspace`)).json();}
let rev;
try{
 const catalog=await client.listTools();check(catalog.tools.length===14&&catalog.tools.find(t=>t.name==='list_projects').annotations.readOnlyHint,'MCP initialization and annotated tool discovery');
 const created=await call('create_project',{name:fixtureName,color:'#35649a',description:'Temporary MCP validation, removed by test.'});pid=created.projects[0].id;
 const project=await call('get_project',{projectId:pid});rev=project.project.revision;const status=project.statuses;
 const a=await call('create_task',{projectId:pid,expectedRevision:rev,title:'Component A'});rev=a.revision;
 const b=await call('create_task',{projectId:pid,expectedRevision:rev,title:'Component B',statusId:status.at(-1).id});rev=b.revision;
 const child=await call('create_task',{projectId:pid,expectedRevision:rev,title:'Markdown specification',parentId:a.task.id});rev=child.revision;
 const grandchild=await call('create_task',{projectId:pid,expectedRevision:rev,title:'Nested acceptance task',parentId:child.task.id});rev=grandchild.revision;
 check((await call('list_tasks',{projectId:pid,parentId:null,limit:1})).nextOffset===1,'Nested task creation and complete pagination');
 const person=await call('create_assignee',{name:`MCP assignee ${crypto.randomUUID()}`});personId=person.assignees[0].id;
 const secondPerson=await call('create_assignee',{name:`MCP second assignee ${crypto.randomUUID()}`});secondPersonId=secondPerson.assignees[0].id;
 const updated=await call('update_task',{taskId:child.task.id,expectedRevision:rev,assigneeIds:[personId,secondPersonId],estimatedHours:3.75,startDate:'2026-10-26',dueDate:'2026-11-02',priority:'high',dependencyIds:[b.task.id]});rev=updated.revision;
 const uploaded=await call('upload_task_spec',{taskId:child.task.id,expectedRevision:rev,filePath:'task-spec.md',mode:'replace'});rev=uploaded.revision;
 const task=await call('get_task',{taskId:child.task.id,includeDocument:true});
 check(task.markdown.includes('Acceptance criteria')&&JSON.stringify(task.document).includes('table')&&JSON.stringify(task.document).includes('taskList'),'Markdown file upload, readable Markdown retrieval and editable rich-text tables/checklists');
 check(task.task.estimatedHours===3.75&&task.task.assigneeIds.join() === [personId,secondPersonId].join()&&task.assignees.length===2&&task.task.dueDate==='2026-11-02'&&task.dependencyIds[0]===b.task.id&&task.subitems[0].id===grandchild.task.id,'Spec upload preserves effort, both assignees, dates, dependencies and nested tasks');
 check((await call('list_tasks',{projectId:pid,assigneeId:secondPersonId})).items.some(item=>item.id===child.task.id),'MCP filters find the second assignee');
 const appended=await call('set_task_spec',{taskId:child.task.id,expectedRevision:rev,markdown:'## Added through MCP\n\nExtra acceptance evidence.',mode:'append'});rev=appended.revision;
 check((await call('get_task',{taskId:child.task.id})).markdown.includes('Acceptance criteria'),'Appending preserves the existing spec');
 const before=JSON.stringify(await workspace());
 const stale=await call('set_task_spec',{taskId:child.task.id,expectedRevision:rev-1,markdown:'Overwrite',mode:'replace'},true);check(stale.error.code==='revision_conflict','Stale edits return an MCP revision conflict');
 await call('set_task_spec',{taskId:child.task.id,expectedRevision:rev,markdown:'<script>bad()</script>',mode:'replace'},true);
 await call('upload_task_spec',{taskId:child.task.id,expectedRevision:rev,filePath:'../../README.md',mode:'replace'},true);
 await call('move_task',{taskId:a.task.id,expectedRevision:rev,parentId:grandchild.task.id},true);
 await call('update_task',{taskId:b.task.id,expectedRevision:rev,dependencyIds:[child.task.id]},true);
 await call('update_task',{taskId:child.task.id,expectedRevision:rev,startDate:'2026-12-01',dueDate:'2026-10-01'},true);
 check(JSON.stringify(await workspace())===before,'Unsafe uploads, escaping paths, hierarchy/dependency cycles and reversed dates leave saved data unchanged');
 const reordered=await call('reorder_components',{projectId:pid,expectedRevision:rev,parentId:null,itemIds:[b.task.id,a.task.id]});rev=reordered.revision;
 check(reordered.components[0].id===b.task.id&&reordered.components[1].statusId===a.task.statusId,'MCP component resequencing preserves statuses');
 const moved=await call('move_task',{taskId:grandchild.task.id,expectedRevision:rev,parentId:b.task.id});rev=moved.revision;
 check(moved.task.parentId===b.task.id,'MCP structural edits use the backend domain rules');
 const deleted=await call('delete_task',{taskId:b.task.id,expectedRevision:rev});rev=deleted.revision;
 check(deleted.deletedTaskIds.includes(b.task.id)&&deleted.deletedTaskIds.includes(grandchild.task.id),'Explicit MCP subtree deletion removes only the requested branch');
 const readOnlyTransport=new StdioClientTransport({command:process.execPath,args:[`${root}src/index.mjs`],env:{...process.env,PLANNER_URL:base,PLANNER_MCP_READ_ONLY:'true'},stderr:'pipe'});
 const readOnly=new Client({name:'read-only-check',version:'1.0.0'});
 try{await readOnly.connect(readOnlyTransport);check((await readOnly.listTools()).tools.every(t=>t.annotations.readOnlyHint),'Read-only mode exposes no mutation or file-upload tools');}finally{await readOnly.close();}
 const unavailableTransport=new StdioClientTransport({command:process.execPath,args:[`${root}src/index.mjs`],env:{...process.env,PLANNER_URL:'http://127.0.0.1:1',PLANNER_TIMEOUT_MS:'500'},stderr:'pipe'});
 const unavailable=new Client({name:'offline-check',version:'1.0.0'});
 try{await unavailable.connect(unavailableTransport);const failed=await unavailable.callTool({name:'list_projects',arguments:{}});check(failed.isError&&failed.content[0].text.includes('could not be reached'),'Unavailable backend produces an explicit MCP error');}finally{await unavailable.close();}
 if(process.env.PLANNER_KEEP_MCP_FIXTURE==='true'){
  await writeFile('/tmp/planner-mcp-fixture.json',JSON.stringify({projectId:pid,taskId:child.task.id,personId,secondPersonId}));pid=undefined;personId=undefined;secondPersonId=undefined;
 }
 console.log(`${count} MCP checks passed.`);
}finally{
 await client.close();
 if(pid){const s=await workspace();const p=s.projects.find(p=>p.id===pid);if(p)await fetch(`${base}/api/projects/${pid}?expectedRevision=${p.revision}`,{method:'DELETE'});}
 if(secondPersonId)await fetch(`${base}/api/people/${secondPersonId}`,{method:'DELETE'});
 if(personId)await fetch(`${base}/api/people/${personId}`,{method:'DELETE'});
}
