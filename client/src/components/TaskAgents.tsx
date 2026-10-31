import { useCallback, useEffect, useRef, useState } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import { Bot, Download, FileInput, Loader2, Play, Square, RefreshCw } from 'lucide-react';
import { agentRequest, downloadMarkdown, purposeLabels, agentNames } from '../lib/agents';
import type { AgentConnection, AgentDraft, AgentId, AgentProviders, AgentPurpose, AgentRun } from '../lib/agents';
import type { Item } from '../lib/types';
import { request } from '../lib/api';
import { AgentConnectionDialog } from './AgentConnectionDialog';
import { MarkdownImport } from './MarkdownImport';
import { Button } from './ui/button';
import { changeAgent, executionOverrides, inheritedSettings } from '../lib/agent-settings';
import { BuildExperience } from './BuildExperience';
import type { BuildExperienceHandle } from './BuildExperience';
import './agents.css';

export function TaskAgents({ item, beforeRun, onImport, draft, onDraftChange }: { item: Item; beforeRun: () => Promise<void>; onImport: (markdown: string, mode: 'append' | 'replace') => Promise<void>; draft: AgentDraft; onDraftChange: Dispatch<SetStateAction<AgentDraft>> }) {
 const [connection, setConnection] = useState<AgentConnection | null>(null); const [providers, setProviders] = useState<AgentProviders | null>(null);
 const [runs, setRuns] = useState<AgentRun[]>([]); const [selectedId, setSelectedId] = useState<string | null>(null);
 const { provider, purpose, instructions, previousId } = draft;
 const setProvider = (provider: AgentId | '') => onDraftChange(current => changeAgent(current, provider));
 const setPurpose = (purpose: AgentPurpose) => onDraftChange(current => ({ ...current, purpose }));
 const setInstructions = (instructions: string) => onDraftChange(current => ({ ...current, instructions }));
 const setPreviousId = (previousId: string | null) => onDraftChange(current => ({ ...current, previousId }));
 const [connecting, setConnecting] = useState(false); const [importing, setImporting] = useState<string | null>(null);
 const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(true);
 const [pollEpoch, setPollEpoch] = useState(0);
 const [pollError, setPollError] = useState('');
 const buildExperience = useRef<BuildExperienceHandle>(null);
 const pendingRequest = useRef<{ id: string; fingerprint: string } | null>(null);
 const load = useCallback(async (signal?: AbortSignal) => {
  const [binding, available, history] = await Promise.all([
   agentRequest<AgentConnection | null>(`/projects/${item.projectId}/connection`, 'GET', undefined, signal),
   agentRequest<AgentProviders>('/providers', 'GET', undefined, signal),
   agentRequest<AgentRun[]>(`/tasks/${item.id}/runs`, 'GET', undefined, signal)
  ]);
  if (signal?.aborted) return 0;
  if (available.providers.some(agent => !Array.isArray(agent.effortLevels))) throw new Error('Restart the Canopy backend to use model and effort settings.');
  setConnection(binding); setProviders(available); setRuns(history); setPollError(''); onDraftChange(current => current.provider || !binding ? current : { ...current, provider: binding.provider }); setLoading(false);
  return available.pollIntervalMs;
 }, [item.id, item.projectId, onDraftChange]);
 useEffect(() => {
  const controller = new AbortController(); let timer: ReturnType<typeof setTimeout>;
  async function poll() {
   try { const interval = await load(controller.signal); if (!controller.signal.aborted && interval > 0) timer = setTimeout(() => { void poll(); }, interval); }
   catch (e) { if (!controller.signal.aborted) { setPollError((e as Error).message); setLoading(false); } }
  }
  void poll(); return () => { controller.abort(); clearTimeout(timer); };
 }, [load, pollEpoch]);
 const active = runs.some(run => run.active); const selected = runs.find(run => run.id === selectedId) ?? runs[0];
 const buildRun = runs.find(run => run.active && run.purpose === 'implementation') ?? (selected?.purpose === 'implementation' ? selected : undefined);
 const previous = runs.find(run => run.id === previousId);
 const defaults = connection ? inheritedSettings(connection, provider, previous) : { model: null, effort: null };
 const defaultLabel = previousId ? 'Previous run' : 'Project default';
 async function start(event: React.FormEvent) {
  event.preventDefault(); if (purpose === 'implementation') buildExperience.current?.prepare(); setBusy(true); setError('');
  try {
   await beforeRun(); const workspace = await request(); const expectedRevision = workspace.projects.find(p => p.id === item.projectId)!.revision;
   const overrides = executionOverrides(draft);
   const fingerprint = JSON.stringify({ provider, purpose, instructions, previousId, ...overrides });
   if (pendingRequest.current?.fingerprint !== fingerprint) pendingRequest.current = { id: crypto.randomUUID(), fingerprint };
   const run = await agentRequest<AgentRun>(`/tasks/${item.id}/runs`, 'POST', { requestId: pendingRequest.current.id, expectedRevision, provider, purpose, instructions, previousRunId: previousId, ...overrides });
   pendingRequest.current = null; setSelectedId(run.id); setPreviousId(null); setInstructions(''); await load();
  } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
 }
 function stopBuild() {
  if (!buildRun?.active) return;
  setBusy(true); void agentRequest(`/runs/${buildRun.id}/cancel`, 'POST').then(() => load()).catch(e => setError((e as Error).message)).finally(() => setBusy(false));
 }
 return <section className="task-agents" aria-label="Agent activity">
  <div className="agent-section-heading"><h3><Bot size={16}/>Agents</h3><button className="text-button" onClick={() => setConnecting(true)}>{connection ? 'Connection settings' : 'Connect project'}</button></div>
  <p className="agent-note">Draft specs and documentation, or explicitly run an implementation. Task status stays under your control.</p>
  <BuildExperience ref={buildExperience} run={buildRun} disconnected={!!pollError} onStop={stopBuild} stopping={busy} controlError={error}/>
  {loading ? <p className="muted"><Loader2 size={14} className="spin"/> Loading agents…</p> : connection ? <form className="agent-run-form" onSubmit={event => { void start(event); }}>
   <div className="form-row"><label>Agent<select required value={provider} disabled={busy || active || !!previousId} onChange={e => setProvider(e.target.value as AgentId)}><option value="">Choose an agent</option>{providers?.providers.map(p => <option key={p.id} value={p.id} disabled={!p.available}>{p.name}{p.available ? '' : ' · unavailable'}</option>)}</select></label><label>Action<select value={purpose} disabled={busy || active || !!previousId} onChange={e => setPurpose(e.target.value as AgentPurpose)}>{Object.entries(purposeLabels).map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label></div>
   {previousId && <p className="agent-note">Continuing the selected run’s session. <button type="button" className="text-button" onClick={() => setPreviousId(null)}>Start a new session instead</button></p>}
   <div className="form-row agent-execution-settings"><label>Model<select aria-label="Run model choice" disabled={busy || active} value={draft.modelMode} onChange={e => onDraftChange(current => ({ ...current, modelMode: e.target.value as AgentDraft['modelMode'] }))}><option value="inherit">{defaultLabel}: {defaults.model || 'agent default'}</option><option value="default">Use agent default</option><option value="custom">Specify model…</option></select></label><label>Effort<select aria-label="Run reasoning effort" disabled={busy || active || !provider || !providers?.providers.find(agent => agent.id === provider)?.effortLevels.length} value={draft.effort === null ? 'inherit' : draft.effort === '' ? 'default' : draft.effort} onChange={e => onDraftChange(current => ({ ...current, effort: e.target.value === 'inherit' ? null : e.target.value === 'default' ? '' : e.target.value }))}><option value="inherit">{defaultLabel}: {defaults.effort || 'agent default'}</option><option value="default">Use agent default</option>{providers?.providers.find(agent => agent.id === provider)?.effortLevels.map(effort => <option key={effort} value={effort}>{effort}</option>)}</select></label></div>
   {draft.modelMode === 'custom' && <label>Model ID or alias<input aria-label="Run model ID or alias" required maxLength={200} disabled={busy || active} value={draft.model} onChange={e => onDraftChange(current => ({ ...current, model: e.target.value }))} placeholder="Enter a model available to your account"/></label>}
   <label>{previousId ? 'Follow-up instructions' : 'Instructions'}<textarea rows={4} maxLength={100000} required={purpose !== 'implementation' || !!previousId} value={instructions} onChange={e => setInstructions(e.target.value)} placeholder={purpose === 'implementation' ? 'Optional guidance in addition to the saved specification' : 'Describe what you want to write or improve'}/></label>
   <p className="agent-note">{purpose === 'implementation' ? 'Runs against the configured Git ref in a separate worktree. Changes are retained for review.' : 'Reads project context and returns Markdown for review. Repository files stay unchanged.'}</p>
   <Button type="submit" disabled={busy || active || !provider || !providers?.providers.find(p => p.id === provider)?.available}>{busy ? <Loader2 size={14} className="spin"/> : <Play size={14}/>} {previousId ? 'Send follow-up' : purposeLabels[purpose]}</Button>
  </form> : <div className="agent-empty">Planning and importing specs work without a connection. Connect a repository when you want an agent to write or implement.</div>}
  {(error || pollError) && <div role="alert" className="agent-error">{error || pollError} <button type="button" className="text-button" onClick={() => { setError(''); setPollEpoch(value => value + 1); }}>Refresh agent activity</button></div>}
  {runs.length > 0 && <><label className="agent-history-label">Run history<select value={selected?.id ?? ''} onChange={e => { setSelectedId(e.target.value); setPreviousId(null); }}>{runs.map(run => <option key={run.id} value={run.id}>{new Date(run.createdAt).toLocaleString()} · {agentNames[run.provider]} · {purposeLabels[run.purpose]} · {run.state}</option>)}</select></label>
   {selected && <article className="agent-run-detail"><div className="agent-run-heading"><span className={`agent-state agent-state-${selected.state}`} aria-live="polite">{selected.active && <Loader2 size={12} className="spin"/>}{selected.state}</span>{selected.active && <Button size="sm" variant="secondary" disabled={busy || selected.state === 'cancelling'} onClick={() => { setBusy(true); void agentRequest(`/runs/${selected.id}/cancel`, 'POST').then(() => load()).catch(e => setError((e as Error).message)).finally(() => setBusy(false)); }}><Square size={12}/>Stop run</Button>}</div>
    {selected.error && <p role="alert" className="agent-error">{selected.error}</p>}
    <dl className="agent-run-settings"><div><dt>Requested model</dt><dd>{selected.model || 'Agent default'}</dd></div><div><dt>Requested effort</dt><dd>{selected.effort || 'Agent default'}</dd></div></dl>
    {selected.state === 'succeeded' && <p className="agent-note">Agent finished. Review the result and verification evidence before completing the task.</p>}
    {selected.result && <><h4>Result</h4><pre className="agent-output">{selected.result}</pre><div className="agent-result-actions"><Button size="sm" variant="secondary" onClick={() => downloadMarkdown(selected.result)}><Download size={13}/>Download Markdown</Button>{selected.purpose !== 'implementation' && <Button size="sm" variant="secondary" onClick={() => setImporting(selected.result)}><FileInput size={13}/>Preview / import</Button>}</div></>}
    <details><summary>Activity output</summary><pre className="agent-output" aria-label="Agent output">{selected.output || 'Waiting for the agent…'}</pre></details>
    <details><summary>Captured work order</summary><p>{selected.title}</p><pre className="agent-output">{selected.instructions || 'Execute the saved specification.'}</pre><pre className="agent-output">{selected.specJson}</pre></details>
    {selected.workspacePath && <details><summary>Workspace and session</summary><dl className="agent-metadata"><dt>Workspace</dt><dd>{selected.workspacePath}</dd><dt>Starting commit</dt><dd>{selected.baseCommit}</dd><dt>Session</dt><dd>{selected.sessionId || 'Not received'}</dd></dl></details>}
    {selected.purpose === 'implementation' && <details open={!!selected.diff}><summary>Changes for review</summary><pre className="agent-output">{selected.diff || (selected.active ? 'Changes will be collected when this run stops.' : 'No diff was captured. Inspect the workspace and activity output.')}</pre></details>}
    {!active && selected.sessionId && selected.workspacePath && connection?.repositoryPath === selected.repositoryPath && <Button size="sm" variant="secondary" onClick={() => { onDraftChange(current => ({ ...current, previousId: selected.id, provider: selected.provider, purpose: selected.purpose, instructions: '', modelMode: 'inherit', model: '', effort: null })); }}><RefreshCw size={13}/>Continue this session</Button>}
   </article>}
  </>}
  {connecting && <AgentConnectionDialog projectId={item.projectId} onClose={() => { setConnecting(false); void load().catch(e => setError((e as Error).message)); }}/>}
  {importing !== null && <MarkdownImport initialMarkdown={importing} onApply={onImport} onClose={() => setImporting(null)}/>}
 </section>;
}
