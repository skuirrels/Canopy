import { useEffect, useState } from 'react';
import { factoryRequest } from '../../lib/factory';
import type { FactorySkill } from '../../lib/factory';
import type { CalibrationPolicyDetail } from '../../lib/factory-calibration';
import { SkillFields } from './FactorySkillPicker';
import { Button } from '../ui/button';
import { Dialog } from '../ui/dialog';
export function FactoryCalibrationPolicy({ projectId, onClose, onSaved }: { projectId: string; onClose: () => void; onSaved: () => void }) {
 const [detail, setDetail] = useState<CalibrationPolicyDetail | null>(null), [error, setError] = useState(''), [busy, setBusy] = useState(false);
 const [enabled, setEnabled] = useState(false), [autoRun, setAutoRun] = useState(false), [autoApply, setAutoApply] = useState(false), [unmetered, setUnmetered] = useState(false);
 const [calibrator, setCalibrator] = useState<FactorySkill[]>([]), [targets, setTargets] = useState<FactorySkill[]>([]);
 const [provider, setProvider] = useState(''), [model, setModel] = useState(''), [effort, setEffort] = useState(''), [seconds, setSeconds] = useState('');
 useEffect(() => { const abort = new AbortController(); void factoryRequest<CalibrationPolicyDetail>(`/projects/${projectId}/calibration-policy`, 'GET', undefined, abort.signal).then(value => {
  if (abort.signal.aborted) return; setDetail(value); const p = value.policy; if (!p) return;
  setEnabled(p.enabled); setAutoRun(p.autoRun); setAutoApply(p.autoApply); setCalibrator(p.calibrator ? [p.calibrator] : []); setTargets(p.targets);
  const route = p.execution?.routes[0]; setProvider(route?.provider ?? ''); setModel(route?.model ?? ''); setEffort(route?.effort ?? ''); setSeconds(p.execution ? String(p.execution.maxElapsedSeconds) : ''); setUnmetered(p.execution?.allowUnmeteredCost ?? false);
 }).catch(cause => { if (!abort.signal.aborted) setError((cause as Error).message); }); return () => abort.abort(); }, [projectId]);
 async function save(event: React.FormEvent) {
  event.preventDefault(); if (!detail) return; setBusy(true); setError('');
  try {
   const execution = provider || model || seconds ? { ...detail.policy?.execution, enabled: true, routes: [{ role: 'documentation', provider, model, effort: effort || null }], maxAgentInvocations: 1, maxCommandInvocations: 0, maxConcurrentStages: null, maxElapsedSeconds: Number(seconds), allowUnmeteredCost: unmetered, maxTokens: detail.policy?.execution?.maxTokens ?? null, maxCost: detail.policy?.execution?.maxCost ?? null, currency: detail.policy?.execution?.currency ?? null } : null;
   await factoryRequest(`/projects/${projectId}/calibration-policy`, 'PUT', { expectedRevision: detail.projectRevision, policy: { enabled, autoRun, autoApply, calibrator: calibrator[0] ?? null, targets, execution } }); onSaved(); onClose();
  } catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 return <Dialog open wide title="Skill calibration" description="Learn from verified delivered work using an installed calibrator and explicitly permitted existing skills." className="factory-dialog" onOpenChange={open => { if (!open && !busy) onClose(); }}>
 {!detail && !error && <p role="status">Loading calibration policy…</p>}
 {detail && <form onSubmit={event => { void save(event); }}><fieldset className="factory-step-fields" disabled={busy}>
  <label className="factory-policy-checkbox"><input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)}/>Enable calibration</label>
  {!detail.editableScopeConfigured && <p role="status" className="factory-help">Canonical editing is unavailable: configure explicit calibration edit roots on the backend. Existing skill discovery remains read-only.</p>}
  {!detail.processLifetimeSupported && <p role="alert" className="factory-error">This backend cannot own calibration process lifetimes.</p>}
  <p className="factory-help">Calibration creates isolated proposals after verified delivery. It requires the actual installed external skill and one bounded model invocation. Saving settings and observing eligibility start no work.</p>
  <SkillFields projectId={projectId} skills={calibrator} onChange={setCalibrator} legend="Installed calibrator" maxSelections={1}/>
  <SkillFields projectId={projectId} skills={targets} onChange={setTargets} legend="Existing skill edit targets"/>
  <div className="factory-form-row"><label>Provider<select required={enabled || !!model || !!seconds} value={provider} onChange={e => { setProvider(e.target.value); setEffort(''); }}><option value="">Choose provider</option>{detail.providers.map(p => <option key={p.id} value={p.id}>{p.name}{!p.available ? ' · unavailable' : ''}</option>)}</select></label><label>Model ID<input required={enabled || !!provider || !!seconds} maxLength={200} value={model} onChange={e => setModel(e.target.value)}/></label></div>
  <div className="factory-form-row"><label>Reasoning effort<select value={effort} onChange={e => setEffort(e.target.value)}><option value="">Provider default</option>{detail.providers.find(p => p.id === provider)?.effortLevels.map(value => <option key={value}>{value}</option>)}</select></label><label>Maximum elapsed seconds<input type="number" min={1} max={2147483647} step={1} required={enabled || !!provider || !!model} value={seconds} onChange={e => setSeconds(e.target.value)}/></label></div>
  <label className="factory-policy-checkbox"><input type="checkbox" checked={unmetered} onChange={e => setUnmetered(e.target.checked)}/>Permit this bounded invocation without a hard token or currency cap</label>
  <p className="factory-help">Provider usage is an observation and can be unknown. These CLI runners cannot enforce hard token/currency caps. Product execution budgets remain separate.</p>
  <label className="factory-policy-checkbox"><input type="checkbox" checked={autoRun} onChange={e => setAutoRun(e.target.checked)}/>Automatically prepare and run after new verified deliveries</label>
  <p className="factory-help">Existing deliveries are not replayed when settings are saved. Unavailable automatic observations are recorded and need explicit preparation after the gap is resolved.</p>
  <label className="factory-policy-checkbox"><input type="checkbox" checked={autoApply} onChange={e => setAutoApply(e.target.checked)}/>Automatically adopt validated proposals within the selected edit scope</label>
  <p className="factory-help">Without automatic adoption, review the exact diff and approve it explicitly. Canonical writes check current byte hashes and record each file outcome. Partial outcomes require reconciliation; they are never silently retried. This grants no skill installation, memory edit, commit or publication authority.</p>
 </fieldset><div className="dialog-footer"><Button type="button" variant="ghost" disabled={busy} onClick={onClose}>Cancel</Button><Button disabled={busy}>{busy ? 'Saving…' : 'Save calibration policy'}</Button></div></form>}
 {error && <p role="alert" className="factory-error">{error} Reopen this panel to refresh its revision.</p>}
 </Dialog>;
}
