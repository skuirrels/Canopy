import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

test('Claude/Codex 2025 protocol clients can initialize and discover tools without a running backend',async()=>{
 const child=spawn(process.execPath,[fileURLToPath(new URL('../src/index.mjs',import.meta.url))],{env:{...process.env,PLANNER_URL:'http://127.0.0.1:1'},stdio:['pipe','pipe','pipe']});
 const lines=createInterface({input:child.stdout});const pending=new Map();let seq=0;
 lines.on('line',line=>{const message=JSON.parse(line);if(message.id&&pending.has(message.id)){pending.get(message.id)(message);pending.delete(message.id);}});
 function request(method,params){const id=++seq;return new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(new Error('Legacy MCP response timed out')),5000);pending.set(id,response=>{clearTimeout(timer);resolve(response);});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');});}
 try{
  const initialized=await request('initialize',{protocolVersion:'2025-11-25',capabilities:{},clientInfo:{name:'legacy-regression',version:'1.0'}});
  assert.equal(initialized.result.protocolVersion,'2025-11-25');assert.match(initialized.result.instructions,/revision/);
  child.stdin.write(JSON.stringify({jsonrpc:'2.0',method:'notifications/initialized'})+'\n');
  const tools=await request('tools/list',{});assert.equal(tools.result.tools.length,14);assert.ok(tools.result.tools.some(t=>t.name==='upload_task_spec'));
 }finally{lines.close();child.kill();}
});
