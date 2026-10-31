import { useEffect, useId, useRef, useState } from 'react';
import { Loader2, Unplug, Bot, FolderOpen } from 'lucide-react';
import { toast } from 'sonner';
import { useWorkspace } from '../lib/workspace';
import { agentRequest } from '../lib/agents';
import { useBuildPreferences } from '../lib/build-preferences';
import type { AgentConnection, AgentExecutionSettings, AgentId, AgentProviders } from '../lib/agents';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';
import { ModelPicker } from './ModelPicker';
import './agents.css';

export function AgentConnectionDialog({ projectId, onClose }: { projectId: string; onClose: () => void }) {
 const { data, refresh } = useWorkspace();
 const { enabled, soundWanted, setEnabled, setSoundWanted } = useBuildPreferences();
 const [connection, setConnection] = useState<AgentConnection | null>(null);
 const [providers, setProviders] = useState<AgentProviders | null>(null);
 const [path, setPath] = useState(''); const [baseRef, setBaseRef] = useState(''); const [provider, setProvider] = useState<AgentId | ''>('');
 const [settings, setSettings] = useState<Record<AgentId, AgentExecutionSettings>>({ codex: { model: null, effort: null }, claude: { model: null, effort: null }, gemini: { model: null, effort: null } }); const [instructions, setInstructions] = useState('');
 const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false); const [browsing, setBrowsing] = useState(false); const [error, setError] = useState('');
 const [refs, setRefs] = useState<{ root: string; isRoot: boolean; current: string | null; branches: string[] } | null>(null); const [refsError, setRefsError] = useState(''); const refEdited = useRef(false); const refList = useId();
 useEffect(() => {
  const controller = new AbortController();
  void Promise.all([agentRequest<AgentConnection | null>(`/projects/${projectId}/connection`, 'GET', undefined, controller.signal), agentRequest<AgentProviders>('/providers', 'GET', undefined, controller.signal)])
   .then(([value, available]) => { if (available.providers.some(agent => !Array.isArray(agent.effortLevels))) throw new Error('Restart the Canopy backend to use model and effort settings.'); setConnection(value); setProviders(available); if (value) { setPath(value.repositoryPath); setBaseRef(value.baseRef); refEdited.current = true; setProvider(value.provider); setSettings(value.settings); setInstructions(value.instructions); } })
   .catch(e => { if (!controller.signal.aborted) setError((e as Error).message); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
  return () => controller.abort();
 }, [projectId]);
 // Read the folder's branches so the current one can fill Base Git ref until the person types their own.
 useEffect(() => {
  const folder = path.trim(); setRefs(null); setRefsError('');
  if (loading || !folder) return;
  const controller = new AbortController();
  const timer = setTimeout(() => {
   agentRequest<{ root: string; isRoot: boolean; current: string | null; branches: string[] }>('/repository-refs', 'POST', { path: folder }, controller.signal)
    .then(value => { setRefs(value); if (!refEdited.current) setBaseRef(value.current ?? value.branches[0] ?? ''); })
    .catch(() => { if (!controller.signal.aborted) setRefsError('Canopy could not read Git branches from this folder.'); });
  }, 400);
  return () => { clearTimeout(timer); controller.abort(); };
 }, [path, loading]);
 async function browse() {
  setBrowsing(true); setError('');
  try { const choice = await agentRequest<{ path: string | null }>('/folder-picker', 'POST', { start: path.trim() || null }); if (choice.path) setPath(choice.path); }
  catch (e) { setError((e as Error).message); } finally { setBrowsing(false); }
 }
 async function save(event: React.FormEvent) {
  event.preventDefault(); setBusy(true); setError('');
  try {
   const expectedRevision = data.projects.find(p => p.id === projectId)!.revision;
   await agentRequest(`/projects/${projectId}/connection`, 'PUT', { expectedRevision, repositoryPath: path, baseRef, provider, model: provider ? settings[provider].model : null, settings, instructions });
   await refresh(); toast.success('Agent connection saved'); onClose();
  } catch (e) { setError((e as Error).message); await refresh(); } finally { setBusy(false); }
 }
 return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title="Agent connection" description="Optional. Connect this project to a local repository for writing and implementation." className="agent-connection-dialog">
  {loading ? <p className="muted"><Loader2 size={15} className="spin"/> Loading connection…</p> : <form onSubmit={event => { void save(event); }}>
   <p className="agent-note">Your project remains a planner. Work starts only when you explicitly run an agent from a task.</p>
   <fieldset className="agent-build-effects"><legend>Build effects</legend>
    <div className="agent-build-effects-controls">
     <label><input type="checkbox" role="switch" aria-label="Build graphics" checked={enabled} onChange={event => setEnabled(event.target.checked)}/><span>Build graphics</span><small aria-hidden="true">{enabled ? 'On' : 'Off'}</small></label>
     <label><input type="checkbox" role="switch" aria-label="Build sound" checked={enabled && soundWanted} disabled={!enabled} onChange={event => setSoundWanted(event.target.checked)}/><span>Build sound</span><small aria-hidden="true">{enabled && soundWanted ? 'On' : 'Off'}</small></label>
    </div>
    <p className="agent-note">Graphics and sound during agent builds. Saves immediately for all projects in this browser. Turning effects off keeps builds running.</p>
   </fieldset>
   <div className="agent-connection-workspace">
   <div className="repository-folder"><label htmlFor="repository-folder">Repository folder</label><div><input id="repository-folder" required value={path} onChange={e => setPath(e.target.value)} placeholder="Absolute path to the repository" autoComplete="off"/>{providers?.folderPicker && <Button type="button" variant="secondary" disabled={browsing || busy} onClick={() => { void browse(); }}>{browsing ? <Loader2 size={14} className="spin"/> : <FolderOpen size={14}/>}{browsing ? 'Choosing…' : 'Browse…'}</Button>}</div>{browsing && <p className="agent-note" role="status">Choose the folder in the window that opened on this computer.</p>}</div>
   <div className="form-row"><label>Base Git ref<input required list={refList} value={baseRef} onChange={e => { setBaseRef(e.target.value); refEdited.current = true; }} placeholder="Branch, tag or commit" autoComplete="off"/><datalist id={refList}>{refs?.branches.map(branch => <option key={branch} value={branch}/>)}</datalist>{refs ? <small className="base-ref-note">{!refs.isRoot ? `This folder is inside ${refs.root}. Choose the repository root.` : refs.current ? `Current branch: ${refs.current}. Implementation starts from this ref.` : 'Detached HEAD. Enter the branch, tag or commit to start from.'}</small> : refsError && <small className="base-ref-note">{refsError}</small>}</label><label>Preferred agent<select required value={provider} onChange={e => setProvider(e.target.value as AgentId)}><option value="">Choose an agent</option>{providers?.providers.map(p => <option key={p.id} value={p.id}>{p.name}{p.available ? '' : ' · not installed'}</option>)}</select></label></div>
   <label>Project instructions <span className="optional">optional</span><textarea rows={6} maxLength={100000} value={instructions} onChange={e => setInstructions(e.target.value)} placeholder="Shared context, conventions and verification requirements"/></label>
   </div>
   <fieldset className="agent-project-defaults"><legend>Model and effort defaults <span className="optional">optional</span></legend>
    {providers?.providers.map(agent => <div key={agent.id} className="agent-default-row"><h4>{agent.name}</h4><div className="form-row"><ModelPicker provider={agent.id} label={`${agent.name} model`} presets={[{ key: 'default', label: 'Agent default' }]} selection={settings[agent.id].model === null ? { preset: 'default' } : { model: settings[agent.id].model! }} onChange={selection => setSettings(current => ({ ...current, [agent.id]: { ...current[agent.id], model: 'model' in selection ? selection.model : null } }))}/><label>{agent.name} effort<select disabled={agent.effortLevels.length === 0} aria-label={`${agent.name} default effort`} value={settings[agent.id].effort ?? ''} onChange={e => setSettings(current => ({ ...current, [agent.id]: { ...current[agent.id], effort: e.target.value || null } }))}><option value="">{agent.effortLevels.length ? 'Agent default' : 'Managed by CLI'}</option>{agent.effortLevels.map(effort => <option key={effort} value={effort}>{effort}</option>)}</select></label></div></div>)}
    <p className="agent-note">Choose a model the agent lists, or Other model for any ID or alias your account can use. Each run can still pick a different model. Effort support depends on the chosen model.</p>
   </fieldset>
   <p className="agent-note">Uses the installed CLI’s sign-in. Implementation changes stay in a separate Git worktree for review.</p>
   {error && <p role="alert" className="agent-error">{error}</p>}
   <div className="dialog-footer">{connection && <Button type="button" variant="ghost" disabled={busy} onClick={() => { setBusy(true); void agentRequest(`/projects/${projectId}/connection?expectedRevision=${data.projects.find(p => p.id === projectId)!.revision}`, 'DELETE').then(async () => { await refresh(); onClose(); toast.success('Project disconnected'); }).catch(e => { setError((e as Error).message); void refresh(); }).finally(() => setBusy(false)); }}><Unplug size={14}/>Disconnect</Button>}<Button type="button" variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy || !providers}>{busy ? <Loader2 size={14} className="spin"/> : <Bot size={14}/>}Save connection</Button></div>
  </form>}
  {loading && error && <p role="alert" className="agent-error">{error}</p>}
 </Dialog>;
}
