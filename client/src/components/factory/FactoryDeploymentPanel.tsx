import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../lib/api';
import { factoryRequest } from '../../lib/factory';
import type { DeliveryRecord } from '../../lib/factory-delivery';
import type { DeploymentDetail, DeploymentLibrary } from '../../lib/factory-deployment';
import { Button } from '../ui/button';
import { Dialog } from '../ui/dialog';

const labels: Record<string, string> = { approveDeploy: 'Approve deployment', deploy: 'Deploy approved revision', approveRecovery: 'Approve recovery', recover: 'Run approved recovery', reconcile: 'Observe target outcome', close: 'Close deployment tracking' };
type SavedRequest = { path: string; body: object };
export function FactoryDeploymentPanel({ delivery }: { delivery: DeliveryRecord }) {
 const [library, setLibrary] = useState<DeploymentLibrary | null>(null), [current, setCurrent] = useState<DeploymentDetail | null>(null), [selected, setSelected] = useState('');
 const [target, setTarget] = useState(''), [action, setAction] = useState(''), [note, setNote] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState(''), [pending, setPending] = useState<SavedRequest | null>(null);
 const alive = useRef(true), sequence = useRef(0), controller = useRef<AbortController | null>(null), submitting = useRef(false);
 const refresh = useCallback(async () => {
  const n = ++sequence.current; controller.current?.abort(); const abort = new AbortController(); controller.current = abort;
  try {
   const list = await factoryRequest<DeploymentLibrary>(`/deliveries/${delivery.id}/deployments`, 'GET', undefined, abort.signal);
   if (list.deliveryId !== delivery.id || !Number.isFinite(list.pollIntervalMs) || list.pollIntervalMs < 250) throw new Error('Deployment controls require the updated backend.');
   const id = selected || list.deployments[0]?.id;
   const detail = id ? await factoryRequest<DeploymentDetail>(`/deployments/${id}`, 'GET', undefined, abort.signal) : null;
   if (alive.current && sequence.current === n) { setLibrary(list); setCurrent(detail); }
  } catch (cause) { if (alive.current && sequence.current === n && !abort.signal.aborted) setError((cause as Error).message); }
 }, [delivery.id, selected]);
 useEffect(() => { alive.current = true; void refresh(); return () => { alive.current = false; sequence.current++; controller.current?.abort(); }; }, [refresh, delivery.revision]);
 useEffect(() => { if (!current?.deployment.currentOperationId || !library) return; const timer = setTimeout(() => { void refresh(); }, library.pollIntervalMs); return () => clearTimeout(timer); }, [current, library, refresh]);
 async function send(request: SavedRequest) {
  if (submitting.current) return; submitting.current = true; setBusy(true); setError('');
  try { const value = await factoryRequest<DeploymentDetail>(request.path, 'POST', request.body); if (alive.current) { setSelected(value.deployment.id); setCurrent(value); setPending(null); setAction(''); setNote(''); } }
  catch (cause) { if (alive.current) { setPending(cause instanceof ApiError && (cause.status === 0 || cause.status >= 500) ? request : null); setError((cause as Error).message); setAction(''); await refresh(); } }
  finally { submitting.current = false; if (alive.current) setBusy(false); }
 }
 function act(kind: string) { if (current) void send({ path: `/deployments/${current.deployment.id}/actions`, body: { requestId: crypto.randomUUID(), expectedRevision: current.deployment.revision, action: kind, approvalNote: note } }); }
 const d = current?.deployment, locked = busy || !!pending || !!d?.currentOperationId;
 const configured = library?.targets.find(x => x.target.id === target);
 const approval = (kind: string) => d?.approvals.some(x => x.action === kind && x.binding === (kind === 'deploy' ? current?.deployBinding : current?.recoveryBinding));
 return <details className="factory-inherited factory-deployment" aria-label="Deployment environments"><summary>Deployment environments</summary>
  <p className="factory-help">Deploy the recorded merge to a configured environment. Capture, approval, execution and task acceptance are separate decisions. Recovery returns to the recorded pre-deployment revision and checks the original persistence snapshot.</p>
  {error && <p className="factory-error" role="alert">{error}</p>}{pending && <p role="status">The response was uncertain. <button type="button" disabled={busy} onClick={() => { void send(pending); }}>Retry saved deployment request</button></p>}
  <Button variant="ghost" disabled={busy} onClick={() => { setError(''); void refresh(); }}>Refresh deployments</Button>
  {library && <><fieldset className="factory-step-fields" disabled={locked || !delivery.remote?.mergeSha}><legend>Capture deployment</legend><label>Deployment target<select value={target} onChange={e => setTarget(e.target.value)}><option value="">Choose a configured environment</option>{library.targets.map(x => <option key={x.target.id} value={x.target.id} disabled={!x.allowed || !x.ready}>{x.target.name} · {x.target.environment}{!x.allowed ? ' · not authorized' : !x.ready ? ' · not ready' : ''}</option>)}</select></label><Button disabled={!configured?.allowed || !configured.ready} onClick={() => { void send({ path: `/deliveries/${delivery.id}/deployments`, body: { requestId: crypto.randomUUID(), expectedRevision: library.sourceRevision, targetId: target } }); }}>Capture merged revision</Button></fieldset>
   {configured?.error && <p className="factory-help" role="status">{configured.error}</p>}
   {!library.targets.length && <p className="factory-help">No deployment targets are configured. Add named targets in backend deployment configuration, then authorize the chosen targets in project delivery policy.</p>}
   {!!library.targets.length && !library.targets.some(x => x.allowed) && <p className="factory-help">Authorize a configured target in project delivery policy before deployment.</p>}
   {!!library.deployments.length && <label>Deployment history<select value={selected || d?.id || ''} onChange={e => setSelected(e.target.value)}>{library.deployments.map(x => <option key={x.id} value={x.id}>{x.capture.target.name} · {x.state} · {new Date(x.createdAt).toLocaleString()}</option>)}</select></label>}
  </>}
  {current && d && <article><h4>{d.capture.target.name} · {d.state}</h4><p role="status">{d.reason}</p><dl className="factory-runtime-facts"><div><dt>Environment</dt><dd>{d.capture.target.environment}</dd></div><div><dt>Approved deployment revision</dt><dd><code>{d.capture.mergeSha}</code></dd></div><div><dt>Recovery revision</dt><dd><code>{d.deployment?.before.revision ?? 'Captured immediately before deployment'}</code></dd></div></dl>
   {!!current.authorityErrors.length && <ul className="factory-error">{current.authorityErrors.map(x => <li key={x}>{x}</li>)}</ul>}
   {d.open && <div className="factory-task-actions">{!d.deployment && <><Button disabled={locked || !!current.authorityErrors.length} onClick={() => setAction('approveDeploy')}>Approve deployment</Button><Button disabled={locked || !!current.authorityErrors.length || !approval('deploy')} onClick={() => setAction('deploy')}>Deploy approved revision</Button></>}
    {d.deployment && !d.recovery && d.capture.target.recoveryHookId && <><Button variant="secondary" disabled={locked || !!current.authorityErrors.length} onClick={() => setAction('approveRecovery')}>Approve recovery</Button><Button disabled={locked || !!current.authorityErrors.length || !approval('recover')} onClick={() => setAction('recover')}>Run approved recovery</Button></>}
    {d.deployment && <Button variant="secondary" disabled={locked || !!current.authorityErrors.length} onClick={() => act('reconcile')}>Observe target outcome</Button>}<Button variant="ghost" disabled={locked} onClick={() => setAction('close')}>Close deployment tracking</Button></div>}
   <details className="factory-inherited"><summary>Deployment operations · {current.operations.length}</summary>{current.operations.map(op => <article key={op.id}><strong>{labels[op.kind]} · {op.state}</strong>{op.error && <p className="factory-error">{op.error}</p>}{op.approvalNote && <p>{op.approvalNote}</p>}<pre tabIndex={0}>{JSON.stringify(op.result, null, 2)}</pre></article>)}</details>
   <details className="factory-inherited"><summary>Original deployment and recovery receipts</summary><pre tabIndex={0}>{JSON.stringify({ deployment: d.deployment, recovery: d.recovery }, null, 2)}</pre></details>
  </article>}
  <Dialog open={!!action} onOpenChange={open => { if (!busy && !open) setAction(''); }} title={labels[action] ?? 'Deployment action'} description={action === 'close' ? 'Retain receipts and stop tracking. This does not reverse a deployment or establish an unknown target outcome. Inspect the target before starting new work.' : action.toLowerCase().includes('recovery') || action === 'recover' ? 'Authorize the configured recovery command for the recorded pre-deployment revision. The original failed deployment remains in history. Recovery must restore the original persistence snapshot.' : 'Authorize only this merged revision and configured environment. No PR merge will be repeated.'}>
   <p>{delivery.policy.repository} · {d?.capture.target.name} · {d?.capture.target.environment}</p><p><code>{action.toLowerCase().includes('recovery') || action === 'recover' ? d?.deployment?.before.revision : d?.capture.mergeSha}</code></p>
   {!!delivery.capture.migrationFiles.length && <p className="factory-help">Migration files: {delivery.capture.migrationFiles.join(', ')}. Confirm compatibility and recovery before approving.</p>}
   {!!delivery.capture.sensitiveFiles.length && <p className="factory-help">Sensitive files: {delivery.capture.sensitiveFiles.join(', ')}.</p>}
   {action.startsWith('approve') && <label>Approval rationale and migration/recovery considerations<textarea rows={4} value={note} onChange={e => setNote(e.target.value)}/></label>}
   <div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setAction('')}>Keep current state</Button><Button disabled={busy || !!pending || action.startsWith('approve') && !note.trim()} onClick={() => act(action)}>{labels[action]}</Button></div>
  </Dialog>
 </details>;
}
