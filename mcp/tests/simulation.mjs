import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/client';
import { StdioClientTransport } from '@modelcontextprotocol/client/stdio';
if(!process.env.PLANNER_SIM_URL)throw new Error('Set PLANNER_SIM_URL to the running Postman simulation member URL.');
const client=new Client({name:'planner-mcp-simulation',version:'1.0'});
const transport=new StdioClientTransport({command:process.execPath,args:[fileURLToPath(new URL('../src/index.mjs',import.meta.url))],env:{...process.env,PLANNER_URL:process.env.PLANNER_SIM_URL},stderr:'pipe'});
try{await client.connect(transport);const response=await client.callTool({name:'list_projects',arguments:{}});assert.ok(!response.isError,JSON.stringify(response));assert.deepEqual(response.structuredContent.items,[]);console.log('MCP process called the simulated Planner workspace over HTTP and returned the real first-use empty state.');}finally{await client.close();}
