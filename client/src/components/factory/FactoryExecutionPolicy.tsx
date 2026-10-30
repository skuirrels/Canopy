import { useEffect, useState } from 'react';
import { Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { factoryRequest } from '../../lib/factory';
import type { ExecutionPolicyDetail, ModelRoute } from '../../lib/factory-execution';
import { Button } from '../ui/button';
import { Dialog } from '../ui/dialog';

export function FactoryExecutionPolicy({ projectId, onClose, onSaved }: { projectId: string; onClose: () => void; onSaved: () => void }) {
 const [detail, setDetail] = useState<ExecutionPolicyDetail | null>(null), [error, setError] = useState(''), [busy, setBusy] = useState(false);
 const [enabled, setEnabled] = useState(false), [unmetered, setUnmetered] = useState(false), [routes, setRoutes] = useState<ModelRoute[]>([]);
 const [limits, setLimits] = useState({ agents: '', commands: '', seconds: '', tokens: '', cost: '', currency: '' });
 useEffect(() => {
  const controller = new AbortController();
  void factoryRequest<ExecutionPolicyDetail>(`/projects/${projectId}/execution-policy`, 'GET', undefined, controller.signal).then(value => {
   if (controller.signal.aborted) return;
   setDetail(value); const p = value.policy;
   if (p) { setEnabled(p.enabled); setUnmetered(p.allowUnmeteredCost); setRoutes(p.routes); setLimits({ agents: String(p.maxAgentInvocations), commands: String(p.maxCommandInvocations), seconds: String(p.maxElapsedSeconds), tokens: p.maxTokens === null ? '' : String(p.maxTokens), cost: p.maxCost === null ? '' : String(p.maxCost), currency: p.currency ?? '' }); }
  }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); });
  return () => controller.abort();
 }, [projectId]);
 async function save(event: React.FormEvent) {
  event.preventDefault(); if (!detail) return; setBusy(true); setError('');
  try {
   await factoryRequest(`/projects/${projectId}/execution-policy`, 'PUT', { expectedRevision: detail.projectRevision, policy: { enabled, routes, maxAgentInvocations: Number(limits.agents), maxCommandInvocations: Number(limits.commands), maxElapsedSeconds: Number(limits.seconds), allowUnmeteredCost: unmetered, maxTokens: limits.tokens === '' ? null : Number(limits.tokens), maxCost: limits.cost === '' ? null : Number(limits.cost), currency: limits.currency || null } });
   onSaved(); onClose();
  } catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 function change(index: number, patch: Partial<ModelRoute>) { setRoutes(values => values.map((value, i) => i === index ? { ...value, ...patch } : value)); }
 return <Dialog open title="Model routing and budgets" description="Opt-in project policy for newly prepared factory work. Saving grants no execution authority." wide className="factory-dialog factory-execution-policy" onOpenChange={open => { if (!open && !busy) onClose(); }}>
  {!detail && !error && <p role="status"><Loader2 className="spin"/>Loading execution policy…</p>}
  {detail && <form onSubmit={event => { void save(event); }}><fieldset disabled={busy} className="factory-step-fields">
   <label className="factory-policy-checkbox"><input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)}/>Enable project execution policy</label>
   <p className="factory-help">Explicit step provider or model choices take priority. Other agent steps use their role route, then project settings. Enabled policy requires a requested model ID for every agent. Model access is checked by the provider during execution.</p>
   <h3>Role routes</h3>{!routes.length && <p className="factory-help">No role routes configured.</p>}
   {routes.map((route, index) => <fieldset key={index} className="factory-policy-route"><legend>Route {index + 1}</legend><div className="factory-form-row">
    <label>Role<select required aria-label={`Route ${index + 1} role`} value={route.role} onChange={e => change(index, { role: e.target.value })}><option value="">Choose role</option>{detail.roles.filter(role => role === route.role || !routes.some(value => value.role === role)).map(role => <option key={role}>{role}</option>)}</select></label>
    <label>Provider<select required aria-label={`Route ${index + 1} provider`} value={route.provider} onChange={e => change(index, { provider: e.target.value, effort: null })}><option value="">Choose provider</option>{detail.providers.map(provider => <option key={provider.id} value={provider.id}>{provider.name}{!provider.available ? ' · unavailable' : ''}</option>)}</select></label></div>
    <div className="factory-form-row"><label>Model ID<input required maxLength={200} aria-label={`Route ${index + 1} model`} value={route.model} onChange={e => change(index, { model: e.target.value })}/></label><label>Reasoning effort<select aria-label={`Route ${index + 1} effort`} value={route.effort ?? ''} onChange={e => change(index, { effort: e.target.value || null })}><option value="">Inherit project setting</option>{detail.providers.find(value => value.id === route.provider)?.effortLevels.map(effort => <option key={effort}>{effort}</option>)}</select></label></div>
    <Button type="button" variant="ghost" onClick={() => setRoutes(values => values.filter((_, i) => i !== index))}><Trash2 size={13}/>Remove route {index + 1}</Button>
   </fieldset>)}
   <Button type="button" variant="secondary" disabled={routes.length >= detail.roles.length} onClick={() => setRoutes(values => [...values, { role: '', provider: '', model: '', effort: null }])}><Plus size={13}/>Add role route</Button>
   {!detail.capabilities.elapsedTimeLimit && <p role="alert" className="factory-error">This backend cannot enforce owned-process elapsed limits on its current operating system. Enabled policy blocks preparation.</p>}
   <h3>Per-work-order limits</h3><p className="factory-help">Invocations are reserved before dispatch. Elapsed time starts with execution and includes manual approval waits. Repair attempts also retain their existing loop limits.</p>
   <div className="factory-form-row factory-policy-limits">{(['agents', 'commands', 'seconds'] as const).map(key => <label key={key}>{key === 'agents' ? 'Maximum agent invocations' : key === 'commands' ? 'Maximum command invocations' : 'Maximum elapsed seconds'}<input required type="number" min={key === 'seconds' ? 1 : 0} max={2147483647} step={1} value={limits[key]} onChange={e => setLimits({ ...limits, [key]: e.target.value })}/></label>)}</div>
   <label className="factory-policy-checkbox"><input type="checkbox" checked={unmetered} onChange={e => setUnmetered(e.target.checked)}/>I permit model execution without a hard token or currency cap</label>
   <p className="factory-help">{detail.capabilities.usageSemantics}</p>
   <details><summary>Requested token or currency cap · unsupported by these CLI runners</summary><p className="factory-help">Setting either cap blocks execution. Provider completion reports arrive after use and cannot enforce a spending ceiling.</p><div className="factory-form-row"><label>Hard token cap<input type="number" min={0} max={Number.MAX_SAFE_INTEGER} step={1} value={limits.tokens} onChange={e => setLimits({ ...limits, tokens: e.target.value })}/></label><label>Hard currency cap<input type="number" min={0} step="any" value={limits.cost} onChange={e => setLimits({ ...limits, cost: e.target.value })}/></label><label>Currency<input required={limits.cost !== ''} value={limits.currency} onChange={e => setLimits({ ...limits, currency: e.target.value })}/></label></div></details>
   <p className="factory-help">Changing an enabled policy invalidates prepared work and stops active work under the previous policy. Its captured settings and evidence are retained. Prepare a new work order to use new settings.</p>
  </fieldset><div className="dialog-footer"><Button type="button" variant="ghost" disabled={busy} onClick={onClose}>Cancel</Button><Button disabled={busy}><Save size={14}/>{busy ? 'Saving…' : 'Save policy'}</Button></div></form>}
  {error && <p role="alert" className="factory-error">{error} Reopen this panel to load the current revision.</p>}
 </Dialog>;
}
