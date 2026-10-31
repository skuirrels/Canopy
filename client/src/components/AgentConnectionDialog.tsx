import { useEffect, useState } from 'react';
import { Loader2, Unplug, Bot } from 'lucide-react';
import { toast } from 'sonner';
import { useWorkspace } from '../lib/workspace';
import { agentRequest } from '../lib/agents';
import { useBuildPreferences } from '../lib/build-preferences';
import type { AgentConnection, AgentExecutionSettings, AgentId, AgentProviders } from '../lib/agents';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';
import './agents.css';

export function AgentConnectionDialog({ projectId, onClose }: { projectId: string; onClose: () => void }) {
 const { data, refresh } = useWorkspace();
 const { enabled, soundWanted, setEnabled, setSoundWanted } = useBuildPreferences();
 const [connection, setConnection] = useState<AgentConnection | null>(null);
 const [providers, setProviders] = useState<AgentProviders | null>(null);
 const [path, setPath] = useState(''); const [baseRef, setBaseRef] = useState(''); const [provider, setProvider] = useState<AgentId | ''>('');
 const [settings, setSettings] = useState<Record<AgentId, AgentExecutionSettings>>({ codex: { model: null, effort: null }, claude: { model: null, effort: null }, gemini: { model: null, effort: null } }); const [instructions, setInstructions] = useState('');
 const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
 useEffect(() => {
  const controller = new AbortController();
  void Promise.all([agentRequest<AgentConnection | null>(`/projects/${projectId}/connection`, 'GET', undefined, controller.signal), agentRequest<AgentProviders>('/providers', 'GET', undefined, controller.signal)])
   .then(([value, available]) => { if (available.providers.some(agent => !Array.isArray(agent.effortLevels))) throw new Error('Restart the Canopy backend to use model and effort settings.'); setConnection(value); setProviders(available); if (value) { setPath(value.repositoryPath); setBaseRef(value.baseRef); setProvider(value.provider); setSettings(value.settings); setInstructions(value.instructions); } })
   .catch(e => { if (!controller.signal.aborted) setError((e as Error).message); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
  return () => controller.abort();
 }, [projectId]);
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
   <label>Repository folder<input required value={path} onChange={e => setPath(e.target.value)} placeholder="Absolute path to the repository" autoComplete="off"/></label>
   <div className="form-row"><label>Base Git ref<input required value={baseRef} onChange={e => setBaseRef(e.target.value)} placeholder="Branch, tag or commit"/></label><label>Preferred agent<select required value={provider} onChange={e => setProvider(e.target.value as AgentId)}><option value="">Choose an agent</option>{providers?.providers.map(p => <option key={p.id} value={p.id}>{p.name}{p.available ? '' : ' · not installed'}</option>)}</select></label></div>
   <label>Project instructions <span className="optional">optional</span><textarea rows={6} maxLength={100000} value={instructions} onChange={e => setInstructions(e.target.value)} placeholder="Shared context, conventions and verification requirements"/></label>
   </div>
   <fieldset className="agent-project-defaults"><legend>Model and effort defaults <span className="optional">optional</span></legend>
    {providers?.providers.map(agent => <div key={agent.id} className="agent-default-row"><h4>{agent.name}</h4><div className="form-row"><label>{agent.name} model<input aria-label={`${agent.name} default model`} maxLength={200} value={settings[agent.id].model ?? ''} onChange={e => setSettings(current => ({ ...current, [agent.id]: { ...current[agent.id], model: e.target.value || null } }))} placeholder="Agent default"/></label><label>{agent.name} effort<select disabled={agent.effortLevels.length === 0} aria-label={`${agent.name} default effort`} value={settings[agent.id].effort ?? ''} onChange={e => setSettings(current => ({ ...current, [agent.id]: { ...current[agent.id], effort: e.target.value || null } }))}><option value="">{agent.effortLevels.length ? 'Agent default' : 'Managed by CLI'}</option>{agent.effortLevels.map(effort => <option key={effort} value={effort}>{effort}</option>)}</select></label></div></div>)}
    <p className="agent-note">Enter a model ID or alias available to your account. Effort support depends on the chosen model.</p>
   </fieldset>
   <p className="agent-note">Uses the installed CLI’s sign-in. Implementation changes stay in a separate Git worktree for review.</p>
   {error && <p role="alert" className="agent-error">{error}</p>}
   <div className="dialog-footer">{connection && <Button type="button" variant="ghost" disabled={busy} onClick={() => { setBusy(true); void agentRequest(`/projects/${projectId}/connection?expectedRevision=${data.projects.find(p => p.id === projectId)!.revision}`, 'DELETE').then(async () => { await refresh(); onClose(); toast.success('Project disconnected'); }).catch(e => { setError((e as Error).message); void refresh(); }).finally(() => setBusy(false)); }}><Unplug size={14}/>Disconnect</Button>}<Button type="button" variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy || !providers}>{busy ? <Loader2 size={14} className="spin"/> : <Bot size={14}/>}Save connection</Button></div>
  </form>}
  {loading && error && <p role="alert" className="agent-error">{error}</p>}
 </Dialog>;
}
