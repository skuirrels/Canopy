import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { factoryRequest } from '../../lib/factory';
import type { PipelineDetail, PipelineList, PipelineVersion } from '../../lib/factory';
import type { FactoryRunDetail } from '../../lib/factory-runs';
import type { RepairLibrary, RepairLoopDetail, RepairPolicy } from '../../lib/factory-repair';
import { useWorkspace } from '../../lib/workspace';
import { ApiError } from '../../lib/api';
import { blockedBy } from '../../lib/blocked';
import { BlockedNote, Button } from '../ui/button';
import { Dialog } from '../ui/dialog';

type Pending = { path: string; body: object; label: string };
const retryFirst = 'Retry the saved repair request first.';
export function FactoryRepairPanel({ detail, onOpenRun, onHistoryChanged }: { detail: FactoryRunDetail; onOpenRun: (id: string) => void; onHistoryChanged: () => void }) {
 const { refresh: refreshWorkspace } = useWorkspace();
 const [library, setLibrary] = useState<RepairLibrary | null>(null), [error, setError] = useState(''), [busy, setBusy] = useState(false), [pending, setPending] = useState<Pending | null>(null);
 const [owner, setOwner] = useState('implementation'), [selected, setSelected] = useState<string[]>([]), [confirm, setConfirm] = useState<RepairLoopDetail | null>(null);
 const [editing, setEditing] = useState(false), [versions, setVersions] = useState<PipelineVersion[]>([]);
 const sequence = useRef(0), controller = useRef<AbortController | null>(null), alive = useRef(true);
 const refresh = useCallback(async () => {
  const n = ++sequence.current; controller.current?.abort(); const abort = new AbortController(); controller.current = abort;
  try {
   const result = await factoryRequest<RepairLibrary>(`/runs/${detail.run.id}/repair-loops`, 'GET', undefined, abort.signal);
   if (!alive.current || n !== sequence.current) return;
   if (!Number.isFinite(result.pollIntervalMs) || result.pollIntervalMs < 250 || result.runId !== detail.run.id) throw new Error('Repair controls require the updated backend.');
   setLibrary(result);
  } catch (cause) { if (!abort.signal.aborted && alive.current && n === sequence.current) setError((cause as Error).message); }
 }, [detail.run.id]);
 useEffect(() => { alive.current = true; void refresh(); return () => { alive.current = false; sequence.current++; controller.current?.abort(); }; }, [refresh, detail.run.revision]);
 useEffect(() => { if (!library?.loops.some(x => x.loop.open)) return; const timer = setTimeout(() => { void refresh(); }, library.pollIntervalMs); return () => clearTimeout(timer); }, [library, refresh]);
 const historySignature = library?.loops.flatMap(x => x.attempts.map(a => `${a.run.id}:${a.run.state}`)).join('|') ?? '';
 useEffect(() => { if (historySignature) onHistoryChanged(); }, [historySignature, onHistoryChanged]);
 async function send(action: Pending) {
  setBusy(true); setError('');
  try { await factoryRequest(action.path, 'POST', action.body); if (alive.current) { setPending(null); setConfirm(null); setSelected([]); await refresh(); } }
  catch (cause) { if (alive.current) { const uncertain = cause instanceof ApiError && (cause.status === 0 || cause.status >= 500); setPending(uncertain ? action : null); setConfirm(null); setError((cause as Error).message); await refresh(); } }
  finally { if (alive.current) setBusy(false); }
 }
 async function edit() {
  setBusy(true); setError('');
  try { const list = await factoryRequest<PipelineList>(`/projects/${detail.run.projectId}/pipelines`); const details = await Promise.all(list.pipelines.filter(x => !x.archived).map(x => factoryRequest<PipelineDetail>(`/pipelines/${x.id}`))); if (alive.current) { setVersions(details.flatMap(x => x.versions)); setEditing(true); } }
  catch (cause) { if (alive.current) setError((cause as Error).message); } finally { if (alive.current) setBusy(false); }
 }
 const active = library?.loops.find(x => x.loop.open), noteId = useId();
 const prepareReason = blockedBy([pending, retryFirst], [detail.run.active, 'Wait for the run to finish before preparing repair.']);
 const captureReason = library && blockedBy([!library.policy.policy, 'Configure the repair policy first.'], [!library.policy.policy?.enabled, 'Enable repair in the repair policy first.'], [!library.context, 'Verification context is unavailable. See the note above.'], [!selected.length, 'Select at least one finding or criterion.']);
 return <details className="factory-inherited factory-repair" aria-label="Bounded repair"><summary>Bounded repair and verification</summary>
 <p className="factory-help">Repair attempts fork the reviewed code into retained worktrees, then run a published repair plan. Fresh criterion QA and independent review are required for completion. Task acceptance stays explicit.</p>
 {detail.run.snapshot.repair && <p>Repair attempt {detail.run.snapshot.repair.attemptNumber} · <button type="button" className="text-button" onClick={() => onOpenRun(detail.run.snapshot.repair!.parentRunId)}>Open parent work order</button></p>}
 {error && <p className="factory-error" role="alert">{error}</p>}
 {pending && <p role="status">The response was uncertain. <button type="button" disabled={busy} onClick={() => { void send(pending); }}>Retry saved {pending.label}</button></p>}
 <Button variant="ghost" disabled={busy} onClick={() => { void refresh(); }}>Refresh repair</Button>
 {library && <><div className="factory-task-actions"><Button variant="secondary" disabled={busy} blockedReason={pending && retryFirst} onClick={() => { void edit(); }}>{library.policy.policy ? 'Edit repair policy' : 'Configure repair policy'}</Button></div>
 <p className="factory-help">Cost: unavailable. A currency cap blocks execution; uncapped execution requires explicit consent to unmetered cost.</p>
 {library.loops.map(value => <article className="factory-repair-loop" key={value.loop.id}><h4>Repair loop · {value.loop.state}</h4><p role="status">{value.loop.reason}</p><p className="factory-help">Owner: {value.loop.scope.owner} · attempts {value.attempts.length}/{value.loop.policy.maxAttempts} · reserved agent calls {value.loop.reservedAgentInvocations}/{value.loop.policy.maxAgentInvocations} · started {value.attempts.reduce((n, x) => n + x.actualAgentInvocations, 0)} · no-progress rounds {value.loop.noProgressRounds}/{value.loop.policy.maxNoProgressRounds} · elapsed limit {value.loop.policy.maxElapsedMinutes} min</p>
 {value.loop.startedAt && <p className="factory-help">Started {new Date(value.loop.startedAt).toLocaleString()}. Queue and human review time count toward the limit.</p>}
 <ul>{value.loop.scope.issues.map(issue => <li key={issue.kind + issue.id}>{issue.title} · <code>{issue.id}</code></li>)}</ul>
 {value.attempts.map(attempt => <div key={attempt.attempt.id}><button type="button" className="text-button" onClick={() => onOpenRun(attempt.run.id)}>Open attempt {attempt.attempt.number} · {attempt.run.state}</button>{attempt.attempt.feedback && <p className="factory-help">{attempt.attempt.feedback.kind}: {attempt.attempt.feedback.reason}</p>}</div>)}
 {value.loop.open && <><div className="factory-task-actions"><Button disabled={busy || !!pending} aria-describedby={pending ? `${noteId}-${value.loop.id}` : undefined} blockedReason={!value.canAdvance && (value.blockedReason || 'Refresh the repair loop before continuing.')} onClick={() => setConfirm(value)}>{value.attempts.length ? 'Continue bounded repair' : 'Start bounded repair'}</Button><Button variant="secondary" disabled={busy || !!pending} aria-describedby={pending ? `${noteId}-${value.loop.id}` : undefined} blockedReason={value.loop.state === 'stopping' && 'Already stopping. Refresh to see the outcome.'} onClick={() => { void send({ path: `/repair-loops/${value.loop.id}/stop`, body: { requestId: crypto.randomUUID(), expectedRevision: value.loop.revision, reason: 'Operator stopped this repair loop.' }, label: 'stop' }); }}>Stop repair loop</Button></div><BlockedNote id={`${noteId}-${value.loop.id}`} reason={pending && retryFirst}/></>}
 <details className="factory-inherited"><summary>Captured repair scope and policy</summary><pre tabIndex={0}>{JSON.stringify(value, null, 2)}</pre></details></article>)}
 {!active && <fieldset className="factory-step-fields" disabled={busy || !!prepareReason} aria-describedby={prepareReason ? noteId : undefined}><legend>Prepare targeted repair</legend><BlockedNote id={noteId} reason={prepareReason}/><label>Responsible role<select value={owner} onChange={event => { setOwner(event.target.value); setSelected([]); }}><option value="implementation">Implementation</option><option value="verification">Verification</option></select></label>
 {library.issues.map(issue => <label className="factory-repair-check" key={issue.kind + issue.id}><input type="checkbox" disabled={!!issue.owner && issue.owner !== owner} aria-describedby={issue.owner && issue.owner !== owner ? `${noteId}-${issue.kind}${issue.id}` : undefined} checked={selected.includes(issue.kind + issue.id)} onChange={event => setSelected(old => event.target.checked ? [...old, issue.kind + issue.id] : old.filter(x => x !== issue.kind + issue.id))}/><span>{issue.title} · {issue.kind} · {issue.owner || 'Choose responsible role'}<small>{issue.reason}</small>{issue.owner && issue.owner !== owner && <small className="blocked-reason" id={`${noteId}-${issue.kind}${issue.id}`}>Choose the {issue.owner} role to select this.</small>}</span></label>)}
 {!library.issues.length && <p className="factory-help">No current actionable findings or failed criteria. Record fresh QA or finish independent review before preparing repair.</p>}
 {library.contextError && <p className="factory-help">{library.contextError}</p>}
 <Button blockedReason={!prepareReason && captureReason} onClick={() => { void send({ path: `/runs/${detail.run.id}/repair-loops`, label: 'preparation', body: { requestId: crypto.randomUUID(), expectedRevision: library.runRevision, expectedContext: library.context?.fingerprint, owner, findingIds: library.issues.filter(x => x.kind === 'finding' && selected.includes(x.kind + x.id)).map(x => x.id), criterionIds: library.issues.filter(x => x.kind === 'criterion' && selected.includes(x.kind + x.id)).map(x => x.id) } }); }}>Capture repair scope</Button><p className="factory-help">Capturing does not execute. All current blocking findings must belong to the same selected role.</p></fieldset>}
 </>}
 {editing && library && <PolicyEditor projectId={detail.run.projectId} revision={library.policy.projectRevision} policy={library.policy.policy} versions={versions} onClose={() => setEditing(false)} onSaved={() => { setEditing(false); void refresh(); void refreshWorkspace().catch(cause => { if (alive.current) setError((cause as Error).message); }); }}/>}
 <Dialog open={!!confirm} onOpenChange={open => { if (!busy && !open) setConfirm(null); }} title="Execute bounded repair?" description="Run the captured repair agents and commands in a separate worktree under this local account. Configured automatic continuation may queue later attempts after failed verification or explicit review feedback, within the captured limits."><p>{confirm?.loop.scope.owner} · {confirm?.loop.policy.maxAttempts} attempts · {confirm?.loop.policy.maxElapsedMinutes} minutes · {confirm?.loop.policy.maxAgentInvocations} agent calls</p><p>Money usage is unavailable. Fresh QA and independent review remain required.</p><div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setConfirm(null)}>Keep captured</Button><Button disabled={busy} blockedReason={pending && retryFirst} onClick={() => { if (confirm) void send({ path: `/repair-loops/${confirm.loop.id}/advance`, body: { requestId: crypto.randomUUID(), expectedRevision: confirm.loop.revision }, label: 'advance' }); }}>Execute captured repair</Button></div></Dialog>
 </details>;
}
function PolicyEditor({ projectId, revision, policy, versions, onClose, onSaved }: { projectId: string; revision: number; policy: RepairPolicy | null; versions: PipelineVersion[]; onClose: () => void; onSaved: () => void }) {
 const [implementation, setImplementation] = useState(policy?.implementationPlanVersionId ?? ''), [verification, setVerification] = useState(policy?.verificationPlanVersionId ?? '');
 const [attempts, setAttempts] = useState(policy?.maxAttempts.toString() ?? ''), [minutes, setMinutes] = useState(policy?.maxElapsedMinutes.toString() ?? ''), [noProgress, setNoProgress] = useState(policy?.maxNoProgressRounds.toString() ?? ''), [calls, setCalls] = useState(policy?.maxAgentInvocations.toString() ?? '');
 const [enabled, setEnabled] = useState(policy?.enabled ?? false), [auto, setAuto] = useState(policy?.autoContinue ?? false), [safe, setSafe] = useState(policy?.commandsSafeToRepeat ?? false), [unmetered, setUnmetered] = useState(policy?.allowUnmeteredCost ?? false), [cost, setCost] = useState(policy?.maxCost?.toString() ?? ''), [currency, setCurrency] = useState(policy?.currency ?? '');
 const [busy, setBusy] = useState(false), [error, setError] = useState('');
 const capturedRevision = useRef(revision);
 async function save() {
  setBusy(true); setError('');
  try { await factoryRequest(`/projects/${projectId}/repair-policy`, 'PUT', { expectedRevision: capturedRevision.current, policy: { enabled, implementationPlanVersionId: implementation || null, verificationPlanVersionId: verification || null, maxAttempts: Number(attempts), maxElapsedMinutes: Number(minutes), maxNoProgressRounds: Number(noProgress), maxAgentInvocations: Number(calls), autoContinue: auto, commandsSafeToRepeat: safe, allowUnmeteredCost: unmetered, maxCost: cost === '' ? null : Number(cost), currency: currency || null } }); onSaved(); }
  catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 const whole = (x: string, min: number) => x !== '' && Number.isSafeInteger(Number(x)) && Number(x) >= min && Number(x) <= 2147483647;
 const invalid = blockedBy([!whole(attempts, 1), 'Enter maximum attempts as a whole number of 1 or more.'], [!whole(minutes, 1), 'Enter the elapsed limit as whole minutes, 1 or more.'], [!whole(noProgress, 1), 'Enter maximum no-progress rounds as a whole number of 1 or more.'], [!whole(calls, 0), 'Enter maximum agent invocations as a whole number of 0 or more.']);
 return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title="Project repair policy" description="Choose published plans and explicit limits. Each plan needs write agents for its responsible role, subsequent command verification and later independent read-only review. Existing loops retain their captured policy.">
 <fieldset className="factory-step-fields" disabled={busy}><label><input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)}/> Enable repair</label>
 <label>Implementation plan<select value={implementation} onChange={e => setImplementation(e.target.value)}><option value="">No implementation plan</option>{versions.map(x => <option key={x.id} value={x.id}>{x.name} · v{x.number}</option>)}</select></label>
 <label>Verification plan<select value={verification} onChange={e => setVerification(e.target.value)}><option value="">No verification plan</option>{versions.map(x => <option key={x.id} value={x.id}>{x.name} · v{x.number}</option>)}</select></label>
 <label>Maximum attempts<input type="number" min="1" required value={attempts} onChange={e => setAttempts(e.target.value)}/></label><label>Elapsed limit in minutes<input type="number" min="1" required value={minutes} onChange={e => setMinutes(e.target.value)}/></label><label>Maximum no-progress rounds<input type="number" min="1" required value={noProgress} onChange={e => setNoProgress(e.target.value)}/></label><label>Maximum agent invocations<input type="number" min="0" required value={calls} onChange={e => setCalls(e.target.value)}/></label>
 <label><input type="checkbox" checked={safe} onChange={e => setSafe(e.target.checked)}/> I reviewed these commands: they are safe to repeat and do not merge, deploy, release or create non-repeatable external effects.</label><label><input type="checkbox" checked={auto} onChange={e => setAuto(e.target.checked)}/> Automatically continue eligible failed attempts within these limits</label><label><input type="checkbox" checked={unmetered} onChange={e => setUnmetered(e.target.checked)}/> Permit execution with unavailable monetary usage</label><label>Optional monetary cap (blocks dispatch until enforceable)<input type="number" min="0" step="any" value={cost} onChange={e => setCost(e.target.value)}/></label><label>Currency for monetary cap<input value={currency} onChange={e => setCurrency(e.target.value)}/></label>
 </fieldset>{error && <p role="alert" className="factory-error">{error}</p>}<div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={onClose}>Close policy</Button><Button disabled={busy} blockedReason={invalid} onClick={() => { void save(); }}>Save explicit policy</Button></div></Dialog>;
}
