import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

export function loadConfig(env = process.env) {
 const configuredUrl = env.PLANNER_URL ?? JSON.parse(readFileSync(new URL('../../server/Planner.Api/appsettings.json', import.meta.url), 'utf8')).Urls;
 const url = new URL(configuredUrl);
 if (!['http:', 'https:'].includes(url.protocol) || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) || url.username || url.password || url.search || url.hash)
  throw new Error('PLANNER_URL must be a loopback HTTP(S) address without credentials, query or fragment.');
 if (env.PLANNER_MCP_READ_ONLY && !['true','false'].includes(env.PLANNER_MCP_READ_ONLY)) throw new Error('PLANNER_MCP_READ_ONLY must be true or false.');
 const timeoutMs = Number(env.PLANNER_TIMEOUT_MS ?? 10000);
 if (!Number.isInteger(timeoutMs) || timeoutMs < 100 || timeoutMs > 120000) throw new Error('PLANNER_TIMEOUT_MS must be 100–120000.');
 return { baseUrl: url.href.replace(/\/?$/, '/'), timeoutMs, readOnly: env.PLANNER_MCP_READ_ONLY === 'true', specRoot: resolve(env.PLANNER_SPEC_ROOT ?? fileURLToPath(new URL('../../', import.meta.url))) };
}
