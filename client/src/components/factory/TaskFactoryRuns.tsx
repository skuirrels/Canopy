import { useEffect, useRef, useState } from 'react';
import { ClipboardCheck, Loader2 } from 'lucide-react';
import { factoryRequest } from '../../lib/factory';
import { ApiError } from '../../lib/api';
import type { FactoryDefinition, FactoryOverrides } from '../../lib/factory';
import { Button } from '../ui/button';

type Check = { code: string; severity: string; message: string; stageId?: string; stepId?: string };
type Environment = { repositoryPath: string; baseRef: string; baseCommit: string; projectInstructions: string; definition: FactoryDefinition; skills: { id: string; source: string; path: string; hash: string }[] };
type Readiness = { projectRevision: number; canPrepare: boolean; executionReadiness: string; checkedAt: string; checks: Check[]; environment: Environment | null };
type Summary = { id: string; itemId: string; state: string; createdAt: string; pipelineName: string; versionNumber: number };
type Detail = { run: { id: string; state: string; createdAt: string; snapshot: { pipelineVersionId: string; versionNumber: number; pipelineName: string; source: string; title: string; specJson: string; projectRevision: number; overrides: FactoryOverrides; environment: Environment; checks: Check[]; checkedAt: string } }; stages: { id: string; stageId: string; state: string }[]; steps: { id: string; stageRunId: string; stepId: string; state: string }[] };

export function TaskFactoryRuns({ itemId, dirty, beforeChange }: { itemId: string; dirty: boolean; beforeChange: () => Promise<void> }) {
 const [readiness, setReadiness] = useState<Readiness | null>(null), [runs, setRuns] = useState<Summary[]>([]), [detail, setDetail] = useState<Detail | null>(null);
 const [error, setError] = useState(''), [busy, setBusy] = useState(false); const requestId = useRef(crypto.randomUUID());
 useEffect(() => { const controller = new AbortController(); void factoryRequest<Summary[]>(`/tasks/${itemId}/runs`, 'GET', undefined, controller.signal).then(value => { if (!controller.signal.aborted) setRuns(value); }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); }); return () => controller.abort(); }, [itemId]);
 useEffect(() => { setReadiness(null); }, [dirty]);
 async function check() {
  setBusy(true); setError(''); setReadiness(null);
  try { await beforeChange(); const value = await factoryRequest<Readiness>(`/tasks/${itemId}/readiness`); requestId.current = crypto.randomUUID(); setReadiness(value); }
  catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 async function prepare() {
  if (!readiness?.canPrepare || dirty) return; setBusy(true); setError('');
  try {
   await beforeChange();
   const value = await factoryRequest<Detail>(`/tasks/${itemId}/runs`, 'POST', { requestId: requestId.current, expectedRevision: readiness.projectRevision });
   setDetail(value); setReadiness(null);
   try { setRuns(await factoryRequest<Summary[]>(`/tasks/${itemId}/runs`)); }
   catch { setError('The work order was prepared, but run history could not be refreshed. Reopen this tab to reload it.'); }
  } catch (cause) {
   if (cause instanceof ApiError && cause.status === 409) { setReadiness(null); setError('The task or pipeline changed. Check readiness again before preparing.'); }
   else setError(cause instanceof ApiError && cause.status === 0 ? 'The preparation response was lost. Retry Prepare run to recover the same work order.' : (cause as Error).message);
  } finally { setBusy(false); }
 }
 async function open(id: string) { setBusy(true); setError(''); try { setDetail(await factoryRequest<Detail>(`/runs/${id}`)); } catch (cause) { setError((cause as Error).message); } finally { setBusy(false); } }
 return <section className="factory-run-preparation" aria-label="Pipeline run preparation"><h4>Run preparation</h4><p className="factory-help">Check local prerequisites and capture an immutable work order. Preparing does not start agents or commands. Credentials and model access remain unverified.</p>
 {dirty && <p className="factory-help">Apply or discard your task configuration edits before checking readiness.</p>}
 <div className="factory-task-actions"><Button variant="secondary" disabled={busy || dirty} onClick={() => { void check(); }}>{busy ? <Loader2 size={14} className="spin"/> : <ClipboardCheck size={14}/>}Check readiness</Button>{readiness && <Button disabled={busy || dirty || !readiness.canPrepare} onClick={() => { void prepare(); }}>Prepare run</Button>}</div>
 {error && <p role="alert" className="factory-error">{error}</p>}
 {readiness && <div aria-live="polite"><p><strong>{readiness.canPrepare ? 'Work order can be prepared' : 'Resolve readiness errors'}</strong></p><ul className="factory-readiness-checks">{readiness.checks.map((check, index) => <li className={check.severity === 'error' ? 'factory-error' : 'factory-help'} key={index}><strong>{check.severity}: </strong>{check.message}</li>)}</ul>{readiness.environment && <p className="factory-help">Base commit: <code>{readiness.environment.baseCommit}</code></p>}</div>}
 {!!runs.length && <><h4>Prepared runs</h4><div className="factory-step-picker">{runs.map(run => <button type="button" disabled={busy} key={run.id} onClick={() => { void open(run.id); }}><span>{run.pipelineName} · v{run.versionNumber}<small> · {new Date(run.createdAt).toLocaleString()} · {run.state}</small></span></button>)}</div></>}
 {detail && <section aria-label="Prepared run snapshot"><h4>{detail.run.snapshot.pipelineName} · version {detail.run.snapshot.versionNumber}</h4><p className="factory-help">State: {detail.run.state}. No execution has been dispatched.</p><ol>{detail.run.snapshot.environment.definition.stages.map(stage => { const stageRun = detail.stages.find(run => run.stageId === stage.id); return <li key={stage.id}>{stage.name} · {stageRun?.state}<ul>{stage.steps.map(step => <li key={step.id}>{step.name} · {detail.steps.find(run => run.stageRunId === stageRun?.id && run.stepId === step.id)?.state}</li>)}</ul></li>; })}</ol><details className="factory-inherited"><summary>Captured work order</summary><pre>{JSON.stringify(detail.run.snapshot, null, 2)}</pre></details></section>}
 </section>;
}
