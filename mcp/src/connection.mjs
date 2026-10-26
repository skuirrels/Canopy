import { fileURLToPath } from 'node:url';
import { loadConfig } from './config.mjs';

export function connectionConfig(config=loadConfig()) {
 const command=process.execPath;const args=[fileURLToPath(new URL('./index.mjs',import.meta.url))];
 const env={PLANNER_URL:config.baseUrl,PLANNER_SPEC_ROOT:config.specRoot};
 if(config.readOnly)env.PLANNER_MCP_READ_ONLY='true';
 const quote=value=>`'${value.replaceAll("'","'\\''")}'`;
 return {
  claudeDesktop:{mcpServers:{'canopy-planner':{command,args,env}}},
  codexCommand:`codex mcp add canopy-planner ${Object.entries(env).map(([k,v])=>`--env ${quote(`${k}=${v}`)}`).join(' ')} -- ${[command,...args].map(quote).join(' ')}`,
  codexToml:`[mcp_servers.canopy-planner]\ncommand = ${JSON.stringify(command)}\nargs = ${JSON.stringify(args)}\n\n[mcp_servers.canopy-planner.env]\n${Object.entries(env).map(([k,v])=>`${k} = ${JSON.stringify(v)}`).join('\n')}\n`,
 };
}
