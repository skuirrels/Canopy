import { useEffect, useState } from 'react';
import { ArrowRight, FolderGit2, FolderOpen, Loader2 } from 'lucide-react';
import type { ProjectSetup } from '../../../lib/factory-setup';
import { agentRequest } from '../../../lib/agents';
import type { AgentExecutionSettings, AgentId } from '../../../lib/agents';
import { blockedBy } from '../../../lib/blocked';
import { Button } from '../../ui/button';
import { ModelPicker } from '../../ModelPicker';

type Folder = { root: string; isRepository: boolean; isRoot: boolean; hasCommits: boolean; hasFiles: boolean; current: string | null; branches: string[] };
const noSettings: Record<AgentId, AgentExecutionSettings> = { codex: { model: null, effort: null }, claude: { model: null, effort: null }, gemini: { model: null, effort: null } };

// Step 1: where the code lives, which branch kept builds go into, and who writes the code.
export function GuidedCode({ projectId, projectRevision, setup, onSaved, onNext }: { projectId: string; projectRevision: number; setup: ProjectSetup; onSaved: () => Promise<void>; onNext: () => void }) {
 const connection = setup.connection, available = setup.providers.filter(p => p.available);
 const provider = setup.providers.find(p => p.id === connection?.provider);
 const [editing, setEditing] = useState(!connection);
 const [path, setPath] = useState(connection?.repositoryPath ?? ''), [branch, setBranch] = useState(connection?.baseRef ?? '');
 const [agent, setAgent] = useState<AgentId | ''>(connection?.provider ?? available[0]?.id ?? '');
 const [settings, setSettings] = useState<Record<AgentId, AgentExecutionSettings>>({ ...noSettings, ...connection?.settings });
 const [folder, setFolder] = useState<Folder | null>(null), [folderError, setFolderError] = useState(''), [checking, setChecking] = useState(false);
 const [busy, setBusy] = useState(false), [error, setError] = useState(''), [canBrowse, setCanBrowse] = useState(false);
 useEffect(() => { void agentRequest<{ folderPicker?: boolean }>('/providers').then(value => setCanBrowse(!!value.folderPicker)).catch(() => setCanBrowse(false)); }, []);
 useEffect(() => {
  if (!editing || !path.trim()) { setFolder(null); setFolderError(''); return; }
  const abort = new AbortController(), timer = window.setTimeout(() => {
   setChecking(true);
   agentRequest<Folder>('/repository-refs', 'POST', { path: path.trim() }, abort.signal)
    .then(value => { setFolder(value); setFolderError(''); setBranch(current => value.branches.includes(current) ? current : value.current ?? value.branches[0] ?? ''); })
    .catch(cause => { if (!abort.signal.aborted) { setFolder(null); setFolderError((cause as Error).message); } })
    .finally(() => { if (!abort.signal.aborted) setChecking(false); });
  }, 400);
  return () => { abort.abort(); window.clearTimeout(timer); };
 }, [path, editing]);
 const needsGit = !!folder && (!folder.isRepository || (folder.isRoot && !folder.hasCommits));

 async function browse() {
  try { const value = await agentRequest<{ path: string | null }>('/folder-picker', 'POST', { start: path.trim() || null }); if (value.path) setPath(value.path); }
  catch (cause) { setError((cause as Error).message); }
 }
 async function startGit() {
  setBusy(true); setError('');
  try { const value = await agentRequest<Folder>('/repository-init', 'POST', { path: path.trim(), includeFiles: true }); setFolder(value); setBranch(value.current ?? ''); }
  catch (cause) { setError((cause as Error).message); }
  finally { setBusy(false); }
 }
 async function save() {
  if (!agent) return;
  setBusy(true); setError('');
  try {
   await agentRequest(`/projects/${projectId}/connection`, 'PUT', { expectedRevision: projectRevision, repositoryPath: path.trim(), baseRef: branch, provider: agent, model: settings[agent].model, settings, instructions: connection?.instructions ?? '' });
   await onSaved(); setEditing(false); onNext();
  } catch (cause) { setError((cause as Error).message); }
  finally { setBusy(false); }
 }
 const blocked = blockedBy(
  [!path.trim(), 'Choose the folder first.'],
  [checking, 'Checking the folder…'],
  [folderError, folderError],
  [needsGit, 'Set up Git in this folder first.'],
  [!branch, 'Choose the branch kept builds go into.'],
  [!available.length, 'No coding agent is installed on this machine. Install Claude Code, Codex or Gemini, then reload.'],
  [!agent, 'Choose who writes the code.']);

 return <>
  <h2>Your code</h2>
  <p className="guided-lead">Choose the folder that holds this project’s code. Each build works in its own copy of that folder, so your files only change when you keep a result.</p>
  {!editing && connection ? <>
   <div className="guided-box">
    <FolderGit2 size={18}/>
    <dl>
     <dt>Folder</dt><dd><code>{connection.repositoryPath}</code></dd>
     <dt>Kept builds go into</dt><dd><code>{connection.baseRef}</code></dd>
     <dt>Written by</dt><dd>{provider?.name ?? connection.provider}{connection.model ? ` · ${connection.model}` : ''}{provider && !provider.available && <strong className="guided-warning"> is not installed on this machine</strong>}</dd>
    </dl>
    <Button variant="secondary" size="sm" onClick={() => setEditing(true)}>Change</Button>
   </div>
   <div className="guided-actions">
    <Button blockedReason={provider && !provider.available ? `${provider.name} isn’t installed on this machine. Press Change to pick another.` : undefined} onClick={onNext}>Next: how to test it<ArrowRight size={14}/></Button>
   </div>
  </> : <form className="guided-form" onSubmit={event => { event.preventDefault(); void save(); }}>
   <label>Folder
    <span className="guided-test-line"><input value={path} onChange={e => setPath(e.target.value)} placeholder="For example: /Users/you/Code/my-app" autoComplete="off" spellCheck={false}/>{canBrowse && <Button type="button" variant="secondary" size="sm" onClick={() => { void browse(); }}><FolderOpen size={14}/>Browse…</Button>}</span>
   </label>
   {checking && <small className="guided-muted"><Loader2 size={12} className="spin"/> Checking the folder…</small>}
   {folderError && <small className="blocked-reason" role="note">{folderError}</small>}
   {needsGit && <div className="guided-note">
    <p>{folder!.isRepository ? 'This folder uses Git but has nothing saved in it yet.' : 'This folder doesn’t use Git yet.'} Canopy needs Git to make a separate copy for each build and to merge the ones you keep.</p>
    <Button type="button" size="sm" variant="secondary" disabled={busy} onClick={() => { void startGit(); }}>Set up Git here</Button>
    <small className="guided-muted"> Saves the files already in this folder as a first version.</small>
   </div>}
   {folder && !needsGit && <label>Kept builds go into
    <select value={branch} onChange={e => setBranch(e.target.value)}>{folder.branches.map(name => <option key={name} value={name}>{name}{name === folder.current ? ' (current)' : ''}</option>)}</select>
    <small className="guided-muted">Usually your main branch. Each build starts from it, and keeping a build merges into it.</small>
   </label>}
   <label>Who writes the code
    <select value={agent} onChange={e => setAgent(e.target.value as AgentId)}>{setup.providers.map(p => <option key={p.id} value={p.id} disabled={!p.available}>{p.name}{p.available ? '' : ' (not installed)'}</option>)}</select>
   </label>
   {agent && <ModelPicker provider={agent} label="Model" presets={[{ key: 'default', label: 'The agent’s default' }]} selection={settings[agent].model === null ? { preset: 'default' } : { model: settings[agent].model! }} onChange={selection => setSettings(current => ({ ...current, [agent]: { ...current[agent], model: 'model' in selection ? selection.model : null } }))}/>}
   {error && <p role="alert" className="guided-error">{error}</p>}
   <div className="guided-actions">
    {connection && <Button type="button" variant="ghost" disabled={busy} onClick={() => setEditing(false)}>Cancel</Button>}
    <Button type="submit" disabled={busy} blockedReason={blocked}>{busy && <Loader2 size={14} className="spin"/>}Save and continue<ArrowRight size={14}/></Button>
   </div>
  </form>}
 </>;
}
