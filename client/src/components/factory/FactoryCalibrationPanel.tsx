import { useCallback, useEffect, useRef, useState } from 'react';
import { factoryRequest } from '../../lib/factory';
import { ApiError } from '../../lib/api';
import { agentRequest } from '../../lib/agents';
import type { CalibrationJob, CalibrationLibrary } from '../../lib/factory-calibration';
import { Button } from '../ui/button';
import { Dialog } from '../ui/dialog';
import { FactoryCalibrationPolicy } from './FactoryCalibrationPolicy';

type Request = { path: string; body: object };
export function FactoryCalibrationPanel({ deliveryId, projectId }: { deliveryId: string; projectId: string }) {
 const [library, setLibrary] = useState<CalibrationLibrary | null>(null), [selected, setSelected] = useState(''), [job, setJob] = useState<CalibrationJob | null>(null);
 const [error, setError] = useState(''), [busy, setBusy] = useState(false), [editing, setEditing] = useState(false), [pending, setPending] = useState<Request | null>(null), [confirm, setConfirm] = useState<{ action: string; request: Request } | null>(null), [note, setNote] = useState('');
 const [agent, setAgent] = useState<{ result: string; output: string; error: string | null; usage: unknown } | null>(null);
 const alive = useRef(true), sequence = useRef(0);
 const refresh = useCallback(async () => {
  const n = ++sequence.current;
  try { const value = await factoryRequest<CalibrationLibrary>(`/deliveries/${deliveryId}/calibrations`, 'GET');
   if (!Number.isFinite(value.pollIntervalMs) || value.pollIntervalMs < 250) throw new Error('Calibration controls require the updated backend.');
   const id = selected || value.jobs[0]?.id;
   const current = id ? await factoryRequest<CalibrationJob>(`/calibrations/${id}`, 'GET') : null;
   if (alive.current && sequence.current === n) { setLibrary(value); setJob(current); }
  } catch (cause) { if (alive.current && sequence.current === n) setError((cause as Error).message); }
 }, [deliveryId, selected]);
 useEffect(() => { alive.current = true; void refresh(); return () => { alive.current = false; sequence.current++; }; }, [refresh]);
 useEffect(() => { if (job && !job.active) void refresh(); }, [job?.id, job?.state, refresh]);
 useEffect(() => {
  if (!job?.active || !library) return;
  const timer = setTimeout(() => { void factoryRequest<CalibrationJob>(`/calibrations/${job.id}`, 'GET').then(value => { if (alive.current) setJob(previous => previous?.id === value.id ? value : previous); }).catch(cause => { if (alive.current) setError((cause as Error).message); }); }, library.pollIntervalMs);
  return () => clearTimeout(timer);
 }, [job, library]);
 useEffect(() => {
  if (!job?.agentRunId) { setAgent(null); return; } const abort = new AbortController();
  void agentRequest<{ result: string; output: string; error: string | null; usage: unknown }>(`/runs/${job.agentRunId}`, 'GET', undefined, abort.signal).then(value => { if (!abort.signal.aborted) setAgent(value); }).catch(cause => { if (!abort.signal.aborted) setError((cause as Error).message); });
  return () => abort.abort();
 }, [job?.agentRunId, job?.state]);
 async function send(request: Request) {
  setBusy(true); setError('');
  try { const saved = await factoryRequest<CalibrationJob>(request.path, 'POST', request.body); if (alive.current) { setJob(saved); setSelected(saved.id); setPending(null); setConfirm(null); setNote(''); } }
  catch (cause) { if (alive.current) { setConfirm(null); setPending(cause instanceof ApiError && (cause.status === 0 || cause.status >= 500) ? request : null); setError((cause as Error).message); } }
  finally { if (alive.current) setBusy(false); }
 }
 function action(value: string) {
  if (!job) return;
  const request = { path: `/calibrations/${job.id}/actions`, body: { requestId: crypto.randomUUID(), expectedRevision: job.revision, action: value, proposalHash: job.proposalHash, note: value === 'close' ? note : null } };
  if (value === 'start' || value === 'apply') setConfirm({ action: value, request }); else void send(request);
 }
 return <section className="factory-calibration" aria-label="Post-delivery skill calibration"><h4>Post-delivery skill calibration</h4>
 <p className="factory-help">Verified delivery can produce isolated improvements to explicitly selected existing skills. Learning does not accept the task or publish skills.</p>
 <div className="factory-task-actions"><Button variant="ghost" disabled={busy} onClick={() => { setError(''); void refresh(); }}>Refresh calibration</Button><Button variant="secondary" disabled={busy || !!pending} onClick={() => setEditing(true)}>Configure calibration</Button></div>
 {error && <p role="alert" className="factory-error">{error}</p>}
 {pending && <p role="status">The response was uncertain. Inspect saved state before retrying the same request. <Button disabled={busy} onClick={() => { void send(pending); }}>Retry saved calibration request</Button></p>}
 {library && <>
  {!!library.readiness.errors.length && <ul className="factory-help">{library.readiness.errors.map(value => <li key={value}>{value}</li>)}</ul>}
  <Button disabled={busy || !!pending || !library.readiness.ready} onClick={() => { void send({ path: `/deliveries/${deliveryId}/calibrations`, body: { requestId: crypto.randomUUID(), expectedRevision: library.readiness.deliveryRevision } }); }}>Prepare calibration</Button>
  <p className="factory-help">Preparation captures bounded evidence and current skill versions. Start the captured work explicitly or configure automatic execution for new deliveries.</p>
  {!!library.jobs.length && <label>Calibration history<select value={selected || job?.id || ''} onChange={e => setSelected(e.target.value)}>{library.jobs.map(value => <option key={value.id} value={value.id}>{new Date(value.createdAt).toLocaleString()} · {value.id === job?.id ? job.state : value.state}</option>)}</select></label>}
  {!!library.triggers.length && <details><summary>Automatic eligibility observations · {library.triggers.length}</summary>{library.triggers.map(value => <p key={value.id}>{new Date(value.createdAt).toLocaleString()} · {value.state}{value.error && ` · ${value.error}`}</p>)}</details>}
 </>}
 {job && <>
  <p role="status">Calibration: <strong>{job.state}</strong></p>{job.error && <p className="factory-error">{job.error}</p>}
  <dl className="factory-runtime-facts"><div><dt>Verified merge</dt><dd><code>{job.capture.mergeSha}</code></dd></div><div><dt>Installed calibrator</dt><dd>{job.capture.calibrator.reference.id} · {job.capture.calibrator.reference.source}</dd></div><div><dt>Evidence hash</dt><dd><code>{job.capture.evidenceHash}</code></dd></div></dl>
  <div className="factory-task-actions">{job.state === 'prepared' && <Button disabled={busy || !!pending} onClick={() => action('start')}>Start captured calibration</Button>}{['prepared', 'queued', 'preparing', 'running', 'proposed'].includes(job.state) && <Button variant="secondary" disabled={busy || !!pending} onClick={() => action('cancel')}>Stop calibration</Button>}{job.state === 'proposed' && <Button disabled={busy || !!pending} onClick={() => action('apply')}>Adopt reviewed skill proposal</Button>}{job.state === 'needsAttention' && <Button disabled={busy || !!pending} onClick={() => action('reconcile')}>Reconcile canonical files</Button>}</div>
  {job.capture.policy.autoApply && <p className="factory-help">This captured policy permits automatic adoption after the host validates actual changes.</p>}
  {job.state === 'needsAttention' && <><label>Partial outcome acknowledgment<textarea value={note} onChange={e => setNote(e.target.value)} rows={3}/></label><Button variant="secondary" disabled={busy || !!pending || !note.trim()} onClick={() => action('close')}>Close tracking with this acknowledgment</Button><p className="factory-help">Closing preserves the journal and remaining diff. It does not retry, revert or certify unavailable file outcomes.</p></>}
  <details><summary>Captured targets · {job.capture.targets.length}</summary>{job.capture.targets.map((target, index) => <div className="factory-runtime-worktree" key={index}><strong>{target.reference.id} · {target.reference.source}</strong><code>{target.path}</code><small>Before: {target.hash}</small></div>)}</details>
  {job.edits.map(edit => { const target = job.capture.targets[edit.target], write = job.writes.find(value => value.target === edit.target); return <details className="factory-inherited" key={edit.target} open><summary>{target.reference.id} · actual proposed diff</summary><code>{target.path}</code><p className="factory-help">Before {edit.beforeHash}<br/>After {edit.afterHash}</p><pre tabIndex={0}>{edit.diff}</pre>{write && <p>{write.state} · {write.actualHash || 'Actual hash unavailable'} {write.error}</p>}</details>; })}
  {job.workspacePath && <div className="factory-runtime-worktree"><h5>Retained proposal workspace</h5><code>{job.workspacePath}</code></div>}
  {agent && <details className="factory-inherited"><summary>Calibrator report, output and usage</summary>{agent.error && <p className="factory-error">{agent.error}</p>}<pre tabIndex={0}>{agent.result || agent.output}</pre><pre tabIndex={0}>{JSON.stringify(agent.usage, null, 2)}</pre></details>}
  <details className="factory-inherited"><summary>Captured current-delivery evidence</summary><pre tabIndex={0}>{job.capture.evidenceJson}</pre></details>
 </>}
 {editing && <FactoryCalibrationPolicy projectId={projectId} onClose={() => setEditing(false)} onSaved={() => { void refresh(); }}/>}
 <Dialog open={!!confirm} title={confirm?.action === 'apply' ? 'Adopt this exact skill proposal?' : 'Start this captured calibration?'} description={confirm?.action === 'apply' ? 'This writes the selected existing canonical skill files after current-hash checks. Each file outcome is recorded; partial outcomes require reconciliation.' : 'This starts one configured model invocation in a retained worktree. The captured policy determines whether validated canonical edits are adopted automatically.'} onOpenChange={open => { if (!open && !busy) setConfirm(null); }}>
 {job && <div className="factory-calibration-confirmation">{confirm?.action === 'apply' ? <p>Write the {job.edits.length} changed existing skill {job.edits.length === 1 ? 'file' : 'files'} in this proposal.</p> : <p>{job.capture.policy.execution?.routes.map(route => `${route.provider} · ${route.model}`).join(', ')} · {job.capture.policy.execution?.maxElapsedSeconds} seconds</p>}<ul>{(confirm?.action === 'apply' ? job.edits.map(edit => job.capture.targets[edit.target]) : job.capture.targets).map((target, i) => <li key={i}>{target.reference.id}<code>{target.path}</code></li>)}</ul>{job.proposalHash && <p className="factory-help">Proposal hash: <code>{job.proposalHash}</code></p>}</div>}
 <div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setConfirm(null)}>Keep for review</Button><Button disabled={busy || !confirm} onClick={() => { if (confirm) void send(confirm.request); }}>{busy ? 'Submitting…' : confirm?.action === 'apply' ? 'Adopt this proposal' : 'Start captured calibration'}</Button></div>
 </Dialog>
 </section>;
}
