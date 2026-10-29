import { useEffect, useRef, useState } from 'react';
import { Play, Square } from 'lucide-react';
import { factoryRequest } from '../../lib/factory';
import { ApiError } from '../../lib/api';
import type { FactoryRunDetail } from '../../lib/factory-runs';
import { Button } from '../ui/button';
import { Dialog } from '../ui/dialog';

export function FactoryRunControls({ detail, onChanged, onRefresh, beforeChange, showActions = true, showDecision = true }: { showActions?: boolean; showDecision?: boolean; detail: FactoryRunDetail; onChanged: (detail: FactoryRunDetail) => void; onRefresh: () => void; beforeChange: () => Promise<void> }) {
 const [starting, setStarting] = useState(false), [attempted, setAttempted] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState(''), [note, setNote] = useState('');
 const startId = useRef(crypto.randomUUID()), decisionId = useRef(crypto.randomUUID());
 const waiting = detail.steps.find(step => step.state === 'waiting');
 const stageRun = detail.stages.find(stage => stage.id === waiting?.stageRunId);
 const step = detail.run.snapshot.environment.definition.stages.find(stage => stage.id === stageRun?.stageId)?.steps.find(step => step.id === waiting?.stepId);
 useEffect(() => { startId.current = crypto.randomUUID(); setError(''); setStarting(false); }, [detail.run.id]);
 useEffect(() => { if (detail.run.state !== 'prepared') { setStarting(false); setError(''); } }, [detail.run.state]);
 useEffect(() => { decisionId.current = crypto.randomUUID(); setNote(''); }, [waiting?.id]);
 async function act(action: 'start' | 'cancel' | 'approve' | 'decline') {
  setBusy(true); setError(''); if (action === 'start') setAttempted(true);
  try {
   await beforeChange();
   const path = action === 'approve' || action === 'decline' ? `/runs/${detail.run.id}/steps/${waiting!.id}/decision` : `/runs/${detail.run.id}/${action}`;
   const result = await factoryRequest<FactoryRunDetail>(path, 'POST', { expectedRevision: detail.run.revision, ...(action === 'start' ? { requestId: startId.current } : action === 'cancel' ? {} : { requestId: decisionId.current, approved: action === 'approve', note }) });
   onChanged(result); setStarting(false);
  } catch (cause) { setError(cause instanceof ApiError && cause.status === 0 ? 'The response was lost; the outcome is unknown. Refresh the saved run state before retrying.' : (cause as Error).message); onRefresh(); } finally { setBusy(false); }
 }
 return <div className="factory-run-controls">{error && <p role="alert" className="factory-error">{error}</p>}{showActions && <div className="factory-task-actions">{detail.run.state === 'prepared' && <Button disabled={busy} onClick={() => { setAttempted(false); setStarting(true); }}><Play size={14}/>Start run</Button>}{detail.run.active && <Button variant="secondary" disabled={busy || detail.run.state === 'cancelling'} onClick={() => { void act('cancel'); }}><Square size={13}/>{detail.run.state === 'cancelling' ? 'Stopping…' : 'Stop run'}</Button>}</div>}
 {showDecision && detail.run.state === 'waiting' && waiting && step && <fieldset className="factory-fieldset" disabled={busy}><legend>Human decision required</legend><strong>{step.name}</strong><p className="factory-help">{step.instructions}</p><label>Decision note<textarea rows={3} value={note} onChange={event => setNote(event.target.value)}/></label><div className="factory-task-actions"><Button onClick={() => { void act('approve'); }}>Approve and continue</Button><Button variant="secondary" onClick={() => { void act('decline'); }}>Decline step</Button></div></fieldset>}
 {showActions && <Dialog open={starting} onOpenChange={open => { if (!busy) setStarting(open); }} className="factory-start-dialog" title="Start this pipeline run?" description="This executes the captured agent steps and commands in a new worktree using this local account. Credentials and model access remain unverified. Task status stays independent."><p>{detail.run.snapshot.title}</p><p>{detail.run.snapshot.pipelineName} · version {detail.run.snapshot.versionNumber}</p><p className="factory-help">Base commit: <code>{detail.run.snapshot.environment.baseCommit}</code>. Later task or pipeline edits do not replace this work order.</p><details className="factory-inherited"><summary>Captured execution steps</summary><ol>{detail.run.snapshot.environment.definition.stages.flatMap(stage => stage.steps.map(step => <li key={`${stage.id}/${step.id}`}>{stage.name} / {step.name} · {step.kind}{step.kind === 'agent' ? ` · ${step.provider} · ${step.permission}` : ''}{step.command && <code>{[step.command.executable, ...step.command.arguments].join(' \u00b7 ')}</code>}</li>))}</ol></details><div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setStarting(false)}>{attempted ? 'Close confirmation' : 'Keep prepared'}</Button><Button disabled={busy} onClick={() => { void act('start'); }}>{busy ? 'Starting…' : 'Start captured run'}</Button></div></Dialog>}
 </div>;
}
