#!/usr/bin/env node
import { serveStdio } from '@modelcontextprotocol/server/stdio';
import { createServer } from './server.mjs';
import { loadConfig } from './config.mjs';
import { connectionConfig } from './connection.mjs';
import { Planner } from './planner.mjs';
try {
 const config=loadConfig();
 if(process.argv.includes('--print-config')) console.log(JSON.stringify(connectionConfig(config),null,2));
 else if(process.argv.includes('--check')) { const state=await new Planner(config).request();console.log(`Canopy MCP ready: ${config.baseUrl} (${state.projects.length} projects, ${config.readOnly?'read-only':'read/write'})`); }
 else serveStdio(()=>createServer(config),{onerror:error=>console.error(`Canopy MCP: ${error.message}`)});
} catch(error) { console.error(`Canopy MCP: ${error.message}`);process.exitCode=1; }
