#!/usr/bin/env node
import { spawn } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const settings = JSON.parse(await readFile(new URL('../server/Planner.Api/appsettings.json', import.meta.url), 'utf8'));
const apiUrl = process.env.PLANNER_API_URL || process.env.Urls || settings.Urls || process.env.ASPNETCORE_URLS;
const children = new Set();
let stopping = false;

function signal(child, name) {
 try { process.platform === 'win32' ? child.kill(name) : process.kill(-child.pid, name); }
 catch (error) { if (error.code !== 'ESRCH') console.error(error.message); }
}
async function stop(code) {
 if (stopping) return;
 stopping = true;
 const running = [...children];
 const closed = Promise.all(running.map(child => new Promise(resolve => child.once('close', resolve))));
 running.forEach(child => signal(child, 'SIGTERM'));
 const force = setTimeout(() => running.forEach(child => signal(child, 'SIGKILL')), 3000);
 await closed;
 clearTimeout(force);
 process.exit(code);
}
function run(command, args, env = {}) {
 return new Promise((resolve, reject) => {
  const child = spawn(command, args, { cwd: root, stdio: 'inherit', detached: process.platform !== 'win32', env: { ...process.env, ...env } });
  children.add(child);
  child.once('error', reject);
  child.once('close', (code, cause) => {
   children.delete(child);
   if (code === 0 || stopping) resolve();
   else reject(new Error(`${command} stopped (${cause || code}).`));
  });
 });
}
async function checkAddress() {
 const url = new URL(apiUrl);
 if (!['http:', 'https:'].includes(url.protocol) || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) || url.pathname !== '/' || url.search || url.hash || url.username || url.password)
  throw new Error('Configure a single loopback HTTP(S) address in PLANNER_API_URL or appsettings.json Urls.');
 const host = url.hostname === '[::1]' ? '::1' : url.hostname;
 const port = Number(url.port || (url.protocol === 'https:' ? 443 : 80));
 await new Promise((resolve, reject) => {
  const server = createServer();
  server.once('error', error => reject(new Error(error.code === 'EADDRINUSE' ? `Canopy's API address ${apiUrl} is already in use. Stop the existing Canopy before starting another backend.` : error.message)));
  server.listen({ host, port, exclusive: true }, () => server.close(resolve));
 });
}
process.once('SIGINT', () => { void stop(130); });
process.once('SIGTERM', () => { void stop(143); });
try {
 await checkAddress();
 if (!process.argv.includes('--check')) {
  await run('npm', ['--prefix', 'client', 'ci']);
  console.log(`Starting development API at ${apiUrl}; Ctrl+C stops both API and Vite.`);
  await Promise.race([
   run('dotnet', ['watch', '--project', 'server/Planner.Api', 'run', '--no-launch-profile', '--urls', apiUrl], { ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER: '1', DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH: '1', DOTNET_WATCH_SUPPRESS_STATIC_FILE_HANDLING: '1' }),
   run('npm', ['--prefix', 'client', 'run', 'dev', '--', '--strictPort', ...(process.env.PLANNER_WEB_PORT ? ['--port', process.env.PLANNER_WEB_PORT] : [])], { PLANNER_API_URL: apiUrl })
  ]);
  await stop(0);
 }
} catch (error) {
 if (!stopping) console.error(error.message);
 await stop(1);
}
