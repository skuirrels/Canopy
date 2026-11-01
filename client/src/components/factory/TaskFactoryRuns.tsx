import { useEffect, useId, useRef, useState } from 'react';
import { ArrowUpRight, ClipboardCheck, Loader2 } from 'lucide-react';
import { factoryRequest } from '../../lib/factory';
import { mergeFactoryRunHistory } from '../../lib/factory-runs';
import { factoryHistoryChanged } from '../../lib/factory-events';
import { ApiError } from '../../lib/api';
import type { FactoryReadiness as Readiness, FactoryRunSummary as Summary, FactoryRunDetail as Detail } from '../../lib/factory-runs';
import { FactoryRunControls } from './FactoryRunControls';
import { useFactoryRun } from './useFactoryRun';
import { blockedBy } from '../../lib/blocked';
import { BlockedNote, Button } from '../ui/button';

export function TaskFactoryRuns({ itemId, dirty, beforeChange, onOpenRun, configured, waitingReason }: { configured: boolean; itemId: string; onOpenRun: (id: string) => void; dirty: boolean; beforeChange: () => Promise<void>; waitingReason?: string }) {
 const [readiness, setReadiness] = useState<Readiness | null>(null), [runs, setRuns] = useState<Summary[]>([]), [selectedRun, setSelectedRun] = useState<string | null>(null);
 const runtime = useFactoryRun(selectedRun); const detail = runtime.detail;
 const [error, setError] = useState(''), [busy, setBusy] = useState(false); const requestId = useRef(crypto.randomUUID()), noteId = useId();
 const blocked = blockedBy([!configured, 'Set up the factory for this project first.'], [dirty, 'Save or discard your acceptance criteria or Advanced configuration edits first.']);
 const prepareBlocked = blocked ?? waitingReason;
 useEffect(() => { const controller = new AbortController(); void factoryRequest<Summary[]>(`/tasks/${itemId}/runs`, 'GET', undefined, controller.signal).then(value => { if (!controller.signal.aborted) setRuns(current => mergeFactoryRunHistory(value, current)); }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); }); return () => controller.abort(); }, [itemId]);
 useEffect(() => { setReadiness(null); }, [dirty]);
 async function check() {
  setBusy(true); setError(''); setReadiness(null);
  try { await beforeChange(); const value = await factoryRequest<Readiness>(`/tasks/${itemId}/readiness`); requestId.current = crypto.randomUUID(); setReadiness(value); return value; }
  catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 async function prepare(checked = readiness) {
  if (!checked?.canPrepare || dirty) return; setBusy(true); setError('');
  try {
   await beforeChange();
   const value = await factoryRequest<Detail>(`/tasks/${itemId}/runs`, 'POST', { requestId: requestId.current, expectedRevision: checked.projectRevision });
   setSelectedRun(value.run.id); runtime.seed(value); factoryHistoryChanged(); setReadiness(null);
   try { const received = await factoryRequest<Summary[]>(`/tasks/${itemId}/runs`); setRuns(current => mergeFactoryRunHistory(received, current)); }
   catch { setError('The work order was prepared, but run history could not be refreshed. Reopen this tab to reload it.'); }
  } catch (cause) {
   if (cause instanceof ApiError && cause.status === 409) { setReadiness(null); setError('The task or pipeline changed. Check readiness again before preparing.'); }
   else setError(cause instanceof ApiError && cause.status === 0 ? 'The preparation response was lost. Retry Prepare run to recover the same work order.' : (cause as Error).message);
  } finally { setBusy(false); }
 }
 async function prepareBuild() { if (!configured || dirty || busy || waitingReason) return; const checked = readiness?.canPrepare ? readiness : await check(); if (checked?.canPrepare) await prepare(checked); }
 function open(id: string) { setSelectedRun(id); runtime.refresh(); }
 useEffect(() => { if (detail) setRuns(current => [...current.filter(value => value.id !== detail.run.id), { id: detail.run.id, itemId: detail.run.itemId, state: detail.run.state, createdAt: detail.run.createdAt, pipelineName: detail.run.snapshot.pipelineName, versionNumber: detail.run.snapshot.versionNumber, title: detail.run.snapshot.title, revision: detail.run.revision }].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt))); }, [detail]);
 return <section className="factory-run-preparation" aria-label="Pipeline run preparation"><h4>Prepare your build</h4><p className="factory-help">We’ll check prerequisites and capture this task’s workflow and criteria. Review the prepared build, then choose Start run explicitly. Credentials and model access remain unverified.</p>
 <div className="factory-task-actions"><Button disabled={busy || !!prepareBlocked} aria-describedby={prepareBlocked ? noteId : undefined} onClick={() => { void prepareBuild(); }}>{busy ? <Loader2 size={14} className="spin"/> : <ClipboardCheck size={14}/>}Prepare build</Button><Button variant="ghost" disabled={busy || !!blocked} aria-describedby={blocked ? noteId : undefined} onClick={()=>{void check();}}>Check prerequisites only</Button></div><BlockedNote id={noteId} reason={prepareBlocked}/>
 {error && <p role="alert" className="factory-error">{error}</p>}
 {readiness && <div aria-live="polite"><p><strong>{readiness.canPrepare ? 'Work order can be prepared' : 'Resolve readiness errors'}</strong></p><ul className="factory-readiness-checks">{readiness.checks.map((check, index) => <li className={check.severity === 'error' ? 'factory-error' : 'factory-help'} key={index}><strong>{check.severity}: </strong>{check.message}</li>)}</ul>{readiness.environment && <p className="factory-help">Base commit: <code>{readiness.environment.baseCommit}</code></p>}</div>}
 {!!runs.length && <><h4>Pipeline runs</h4><div className="factory-step-picker">{runs.map(run => <button type="button" disabled={busy} key={run.id} onClick={() => { void open(run.id); }}><span>{run.pipelineName} · v{run.versionNumber}<small> · {new Date(run.createdAt).toLocaleString()} · {run.state}</small></span></button>)}</div></>}
 {runtime.error && <p role="alert" className="factory-error">{runtime.error}</p>}
 {selectedRun && <Button variant="ghost" onClick={runtime.refresh}>Refresh run</Button>}
 {detail && <section aria-label="Prepared run snapshot"><h4>{detail.run.snapshot.pipelineName} · version {detail.run.snapshot.versionNumber}</h4><p className="factory-help">State: {detail.run.state}.{detail.run.state === 'prepared' ? ' Prepared work orders require an explicit Start run action.' : ' Runtime state and evidence are reported by the backend.'}</p>{detail.run.error && <p role="alert" className="factory-error">{detail.run.error}</p>}<Button variant="secondary" onClick={() => { void beforeChange().then(() => onOpenRun(detail.run.id)).catch(cause => setError((cause as Error).message)); }}><ArrowUpRight size={14}/>Open execution view</Button><FactoryRunControls key={detail.run.id} detail={detail} onChanged={runtime.update} onRefresh={runtime.refresh} beforeChange={beforeChange}/>{detail.run.workspacePath && <p className="factory-help">Retained worktree: <code>{detail.run.workspacePath}</code></p>}<ol>{detail.run.snapshot.environment.definition.stages.map(stage => { const stageRun = detail.stages.find(run => run.stageId === stage.id); return <li key={stage.id}>{stage.name} · {stageRun?.state}<ul>{stage.steps.map(step => <li key={step.id}>{step.name} · {detail.steps.find(run => run.stageRunId === stageRun?.id && run.stepId === step.id)?.state}</li>)}</ul></li>; })}</ol>{detail.steps.map(step => <details className="factory-inherited" key={step.id}><summary>{detail.run.snapshot.environment.definition.stages.find(stage => stage.id === detail.stages.find(run => run.id === step.stageRunId)?.stageId)?.steps.find(value => value.id === step.stepId)?.name || step.stepId} · {step.state}{step.exitCode !== null ? ` · exit ${step.exitCode}` : ''}</summary>{step.error && <p className="factory-error">{step.error}</p>}{step.output && <pre aria-label="Step output">{step.output}</pre>}{step.outputLimitReached && <p className="factory-help">Output reached the configured retention limit; earlier output may be unavailable. Worktree files are retained.</p>}{step.evidence && <pre aria-label="Step evidence">{step.evidence}</pre>}</details>)}{detail.artifacts.length > 0 && <details className="factory-inherited"><summary>Logical evidence artifacts</summary><p className="factory-help">These hashes identify actual step evidence; they do not imply a physical file exists.</p><pre>{JSON.stringify(detail.artifacts, null, 2)}</pre></details>}<details className="factory-inherited"><summary>Captured work order</summary><pre>{JSON.stringify(detail.run.snapshot, null, 2)}</pre></details></section>}
 </section>;
}
