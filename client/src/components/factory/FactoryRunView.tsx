import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, Copy, Download, List, Loader2, Network, RefreshCw, Workflow } from 'lucide-react';
import { toast } from 'sonner';
import type { FactoryDiagnostic, FactoryStage, FactoryStep } from '../../lib/factory';
import type { FactoryRunDetail, FactoryStepRun } from '../../lib/factory-runs';
import { Button } from '../ui/button';
import { FactoryCanvas } from './FactoryCanvas';
import type { FactorySelection } from './FactoryCanvas';
import { FactoryRunControls } from './FactoryRunControls';
import { FactoryDeliveryPanel } from './FactoryDeliveryPanel';
import { FactoryRepairPanel } from './FactoryRepairPanel';
import { FactoryReviewPanel } from './FactoryReviewPanel';
import { FactoryExecutionEvidence, FactoryUsageEvidence } from './FactoryExecutionEvidence';
import { FactoryQualityPanel } from './FactoryQualityPanel';
import { FactoryRunStatus } from './FactoryRunStatus';
import { useFactoryRun } from './useFactoryRun';

const noEdit = () => {};
const noPendingTaskEdits = async () => {};
const noDiagnostics: FactoryDiagnostic[] = [];
function recordedAt(value: string | null) { return value ? new Date(value).toLocaleString() : 'Not recorded'; }
function download(text: string, filename: string, type = 'text/plain;charset=utf-8') {
 const url = URL.createObjectURL(new Blob([text], { type }));
 const link = document.createElement('a'); link.href = url; link.download = filename; link.click();
 setTimeout(() => URL.revokeObjectURL(url), 1000);
}
function evidence(value: string): Record<string, unknown> | null {
 try { const parsed: unknown = JSON.parse(value); return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : null; } catch { return null; }
}
function Timing({ startedAt, finishedAt }: { startedAt: string | null; finishedAt: string | null }) {
 return <dl className="factory-runtime-facts"><div><dt>Started</dt><dd>{recordedAt(startedAt)}</dd></div><div><dt>Finished</dt><dd>{recordedAt(finishedAt)}</dd></div></dl>;
}
function TextEvidence({ title, text, filename, type }: { title: string; text: string; filename: string; type?: string }) {
 return <section className="factory-runtime-evidence"><div><h4>{title}</h4>{text && <button type="button" className="text-button" onClick={() => download(text, filename, type)}><Download size={12}/>Download {title.toLocaleLowerCase()}</button>}</div>{text ? <pre tabIndex={0} aria-label={title}>{text}</pre> : <p className="factory-help">No {title.toLocaleLowerCase()} recorded.</p>}</section>;
}
function ArtifactList({ detail, select }: { detail: FactoryRunDetail; select: (selection: FactorySelection) => void }) {
 return <section className="factory-runtime-artifacts" aria-label="Logical evidence artifacts"><h4>Logical evidence artifacts · {detail.artifacts.length}</h4><p className="factory-help">These hashes identify recorded step evidence. A declared name does not prove a physical file exists.</p>{!detail.artifacts.length && <p className="factory-help">No successful output artifacts recorded.</p>}{detail.artifacts.map(artifact => {
  const step = detail.steps.find(step => step.id === artifact.stepRunId), stage = detail.stages.find(stage => stage.id === step?.stageRunId);
  const definition = detail.run.snapshot.environment.definition.stages.find(value => value.id === stage?.stageId);
  return <article key={artifact.id}><button type="button" disabled={!stage || !step} onClick={() => select({ stageId: stage!.stageId, stepId: step!.stepId })}><strong>{artifact.name}</strong><small>Producer: {definition?.name} / {definition?.steps.find(value => value.id === step?.stepId)?.name}</small></button><code title="SHA-256 of producer evidence">{artifact.evidenceHash}</code>{step?.evidence && <button type="button" className="text-button" onClick={() => download(step.evidence, `canopy-${artifact.id}-evidence.json`, 'application/json')}><Download size={12}/>Download evidence</button>}</article>;
 })}</section>;
}
function StepInspector({ definition, step, detail }: { definition: FactoryStep; step: FactoryStepRun; detail: FactoryRunDetail }) {
 const result = useMemo(() => evidence(step.evidence), [step.evidence]);
 const report = typeof result?.result === 'string' ? result.result : '', diff = typeof result?.diff === 'string' ? result.diff : '';
 const prefix = `canopy-${step.id}`;
 const stageId = detail.stages.find(stage => stage.id === step.stageRunId)?.stageId;
 const route = detail.run.snapshot.execution?.routes.find(route => route.stageId === stageId && route.stepId === step.stepId);
 return <><p className="factory-help">{definition.kind}{definition.role ? ` · ${definition.role}` : ''}</p><FactoryRunStatus state={step.state}/><Timing startedAt={step.startedAt} finishedAt={step.finishedAt}/>{step.error && <p role="alert" className="factory-error">{step.error}</p>}
 {definition.instructions && <details className="factory-inherited"><summary>Captured instructions</summary><pre>{definition.instructions}</pre></details>}
 {definition.kind === 'agent' && <><dl className="factory-runtime-facts"><div><dt>Agent</dt><dd>{definition.provider}</dd></div><div><dt>Permission</dt><dd>{definition.permission}</dd></div><div><dt>Model requested</dt><dd>{definition.model || 'Agent default; no model ID pinned'}</dd></div><div><dt>Effort requested</dt><dd>{definition.effort || 'Agent default'}</dd></div></dl>{route && <p className="factory-help">Model selection: {route.selectionSource}. Effort selection: {route.effortSource}.</p>}<FactoryUsageEvidence agentRunId={step.agentRunId} active={detail.run.active}/>{step.agentRunId && <p className="factory-help">Agent record: <code>{step.agentRunId}</code></p>}<TextEvidence title="Agent report" text={report} filename={`${prefix}-report.md`} type="text/markdown;charset=utf-8"/><TextEvidence title="Worktree diff" text={diff} filename={`${prefix}.diff`}/><p className="factory-help">A completed agent step records a report; it does not certify correctness or accept the task.</p></>}
 {definition.command && <><h4>Captured command</h4><pre tabIndex={0} className="factory-runtime-command">{JSON.stringify(definition.command, null, 2)}</pre><p className="factory-help">Environment references name backend variables. Their resolved values are not included in this configuration.</p><p>Exit code: <strong>{step.exitCode === null ? 'Not recorded' : step.exitCode}</strong></p></>}
 {definition.kind === 'manual' && <dl className="factory-runtime-facts"><div><dt>Decision</dt><dd>{step.decision === null ? 'No decision recorded' : step.decision ? 'Approved' : 'Declined'}</dd></div><div><dt>Operator note</dt><dd>{step.decisionNote || 'No note recorded'}</dd></div></dl>}
 {!!definition.skills?.length && <details className="factory-inherited"><summary>Captured skill sources · {definition.skills.length}</summary>{definition.skills.map(skill => { const resolved = detail.run.snapshot.environment.skills.find(value => value.id === skill.id && value.source === skill.source); return <div key={`${skill.source}/${skill.id}`}><p>{skill.id} · {skill.source}</p>{resolved ? <pre tabIndex={0}>{resolved.path}{'\n'}SHA-256: {resolved.hash}</pre> : <p className="factory-error">No resolved skill source recorded.</p>}</div>; })}</details>}
 <TextEvidence title="Activity output" text={step.output} filename={`${prefix}.log`}/><p className="factory-help">Activity retention is bounded by backend settings. Inspect the retained worktree for complete files.</p>{step.outputLimitReached && <p className="factory-help">The output retention limit was reached; earlier output may be unavailable.</p>}
 <details className="factory-inherited"><summary>Recorded step evidence</summary><TextEvidence title="Step evidence" text={step.evidence} filename={`${prefix}-evidence.json`} type="application/json"/></details>
 {(!!definition.consumes?.length || !!definition.produces?.length) && <dl className="factory-runtime-facts"><div><dt>Required evidence</dt><dd>{definition.consumes?.join(', ') || 'None declared'}</dd></div><div><dt>Declared outputs</dt><dd>{definition.produces?.join(', ') || 'None declared'}</dd></div></dl>}
 </>;
}

export function FactoryRunView({ id, projectId, workspaceRevision, onChanged, onOpenTask, onOpenRun, onHistoryChanged }: { id: string; projectId: string; workspaceRevision: number; onChanged: (detail: FactoryRunDetail) => void; onOpenTask: (id: string) => void; onOpenRun: (id: string) => void; onHistoryChanged: () => void }) {
 const runtime = useFactoryRun(id), detail = runtime.detail;
 const [selection, setSelection] = useState<FactorySelection>(null), [mode, setMode] = useState<'canvas' | 'list'>(() => window.matchMedia('(max-width: 900px)').matches ? 'list' : 'canvas');
 const inspector = useRef<HTMLElement>(null), graph = useRef<HTMLElement>(null), heading = useRef<HTMLHeadingElement>(null);
 useEffect(() => { if (detail?.run.projectId === projectId) onChanged(detail); }, [detail, projectId, onChanged]);
 useEffect(() => { if (detail?.run.id === id) heading.current?.focus(); }, [id, detail?.run.id]);
 useEffect(() => {
  const panel = inspector.current;
  if (selection && panel?.parentElement && getComputedStyle(panel.parentElement).display === 'flex') panel.querySelector('.factory-panel-heading')?.scrollIntoView({ block: 'start' });
 }, [selection]);
 const observedRevision = useRef(workspaceRevision);
 useEffect(() => { if (observedRevision.current !== workspaceRevision) { observedRevision.current = workspaceRevision; runtime.refresh(); } }, [workspaceRevision, runtime.refresh]);
 const select = useCallback((value: FactorySelection) => { setSelection(value); inspector.current?.scrollTo({ top: 0 }); }, []);
 if (!detail) return <section className="factory-state" aria-label="Factory execution"><Loader2 className={runtime.error ? '' : 'spin'}/><p role={runtime.error ? 'alert' : 'status'}>{runtime.error || 'Loading captured execution…'}</p><Button onClick={runtime.refresh}><RefreshCw size={14}/>Refresh run</Button></section>;
 if (detail.run.projectId !== projectId) return <section className="factory-state" role="alert">This run belongs to another project. Select its project to inspect the work order.</section>;
 const { run } = detail, snapshot = run.snapshot, definition = snapshot.environment.definition;
 const stage = definition.stages.find(stage => stage.id === selection?.stageId), step = stage?.steps.find(step => step.id === selection?.stepId);
 const stageRun = detail.stages.find(value => value.stageId === stage?.id), stepRun = detail.steps.find(value => value.stageRunId === stageRun?.id && value.stepId === step?.id);
 const completed = detail.steps.filter(step => step.state === 'succeeded').length;
 return <section className="factory-editor factory-execution" aria-label="Factory execution">
  <header className="factory-editor-heading"><div><span className="factory-eyebrow"><Workflow size={12}/>CAPTURED EXECUTION</span><h2 ref={heading} tabIndex={-1}>{snapshot.title}</h2><p>{snapshot.pipelineName} · version {snapshot.versionNumber} · prepared {recordedAt(run.createdAt)}</p></div><FactoryRunStatus state={run.state}/></header>
  <div className="factory-actions"><div className="factory-view-switch"><button type="button" aria-pressed={mode === 'canvas'} onClick={() => setMode('canvas')}><Network size={14}/>Execution graph</button><button type="button" aria-pressed={mode === 'list'} onClick={() => setMode('list')}><List size={14}/>Step list</button></div><Button variant="ghost" onClick={runtime.refresh}><RefreshCw size={13}/>Refresh run</Button><Button variant="ghost" onClick={() => onOpenTask(run.itemId)}><ArrowLeft size={13}/>View task</Button><Button variant="ghost" onClick={() => download(JSON.stringify(snapshot, null, 2), `canopy-${run.id}-work-order.json`, 'application/json')}><Download size={13}/>Work order</Button></div>
  {runtime.error && <p role="alert" className="factory-error">{runtime.error}</p>}{run.error && <p role="alert" className="factory-error">{run.error}</p>}
  <div className="factory-runtime-summary"><p role="status">{completed} of {detail.steps.length} steps completed. Task status stays independent.</p>{detail.steps.some(step => step.state === 'waiting') && <Button className="factory-review-decision" variant="secondary" onClick={() => { inspector.current?.scrollTo({ top: 0 }); inspector.current?.querySelector('.factory-fieldset')?.scrollIntoView({ block: 'nearest' }); }}>Review decision</Button>}<FactoryRunControls showDecision={false} detail={detail} onChanged={runtime.update} onRefresh={runtime.refresh} beforeChange={noPendingTaskEdits}/></div>
  <div className="factory-editor-body"><section ref={graph} className="factory-graph-area" aria-label={mode === 'canvas' ? 'Captured execution graph' : 'Captured execution steps'}>{mode === 'canvas' ? <FactoryCanvas definition={definition} identity={`run:${run.id}`} selection={selection} onSelect={select} onConnect={noEdit} onAddStage={noEdit} readOnly diagnostics={noDiagnostics} runtime={detail}/> : <div className="factory-stage-list">{definition.stages.map((stage, index) => {
   const record = detail.stages.find(value => value.stageId === stage.id);
   return <article key={stage.id} className={selection?.stageId === stage.id ? 'active' : ''}><button type="button" className="factory-list-stage" onClick={() => select({ stageId: stage.id })}><span className="factory-order">{index + 1}</span><span><strong>{stage.name}</strong><small>{stage.group} · {stage.dependsOn.length} prerequisites</small></span><FactoryRunStatus state={record?.state ?? 'unavailable'}/></button>{stage.steps.map(step => <button type="button" className={`factory-list-step ${selection?.stageId === stage.id && selection.stepId === step.id ? 'active' : ''}`} key={step.id} onClick={() => select({ stageId: stage.id, stepId: step.id })}><span>{step.name}<small>{step.kind}</small></span><FactoryRunStatus state={detail.steps.find(value => value.stageRunId === record?.id && value.stepId === step.id)?.state ?? 'unavailable'}/></button>)}</article>;
  })}</div>}</section>
  <aside ref={inspector} className="factory-property-panel factory-runtime-inspector" aria-label="Execution inspector"><FactoryRunControls showActions={false} detail={detail} onChanged={runtime.update} onRefresh={runtime.refresh} beforeChange={noPendingTaskEdits}/><div className="factory-panel-heading"><h3>{step?.name || stage?.name || 'Run overview'}</h3><button type="button" className="text-button factory-runtime-back" onClick={() => graph.current?.scrollIntoView({ block: 'start' })}>Back to steps</button>{selection && <button type="button" className="text-button" onClick={() => select(null)}>Overview</button>}</div>
   {step ? stepRun ? <StepInspector key={stepRun.id} definition={step} step={stepRun} detail={detail}/> : <p className="factory-error">The step runtime record is unavailable.</p> : stage ? <StageInspector stage={stage} detail={detail} select={select}/> : <><FactoryRunStatus state={run.state}/><Timing startedAt={run.startedAt} finishedAt={run.finishedAt}/><p className="factory-help">Run: <code>{run.id}</code></p><dl className="factory-runtime-facts"><div><dt>Base commit</dt><dd><code>{snapshot.environment.baseCommit}</code></dd></div><div><dt>Repository</dt><dd><code>{snapshot.environment.repositoryPath}</code></dd></div><div><dt>Configuration source</dt><dd>{snapshot.source}</dd></div></dl><p className="factory-help">This graph is the immutable work order. Layout changes stay in this browser. Select a step to inspect its actual activity and evidence.</p></>}
   {detail.parallel && <section className="factory-runtime-worktree"><h4>Parallel integration</h4><p>State: {detail.parallel.state} · up to {snapshot.execution?.policy.maxConcurrentStages} stages</p><p className="factory-help">Only explicit prerequisite outputs enter each stage. Conflicts retain the branches for review.</p><dl className="factory-runtime-facts"><div><dt>Initial tree</dt><dd><code>{detail.parallel.initial?.tree || 'Pending capture'}</code></dd></div><div><dt>Final tree</dt><dd><code>{detail.parallel.final?.tree || 'Awaiting verified integration'}</code></dd></div></dl>{detail.parallel.error && <p className="factory-error">{detail.parallel.error}</p>}</section>}
   <FactoryExecutionEvidence detail={detail}/>
   <FactoryDeliveryPanel key={detail.run.id} detail={detail} onOpenRun={onOpenRun}/>
   <FactoryRepairPanel key={detail.run.id} detail={detail} onOpenRun={onOpenRun} onHistoryChanged={onHistoryChanged}/>
   <FactoryReviewPanel key={detail.run.id} detail={detail} select={select} onChanged={runtime.refresh}/>
   <FactoryQualityPanel detail={detail} select={select} onChanged={runtime.refresh}/>
   <section className="factory-runtime-worktree"><h4>Retained worktree</h4>{run.workspacePath ? <><code>{run.workspacePath}</code><button type="button" className="text-button" onClick={() => { void navigator.clipboard.writeText(run.workspacePath!).then(() => toast.success('Worktree path copied')).catch(() => toast.error('Could not copy the path. Select and copy the displayed text.')); }}><Copy size={12}/>Copy worktree path</button></> : <p className="factory-help">No worktree recorded. An explicit start is required to create one.</p>}</section>
   <ArtifactList detail={detail} select={select}/><details className="factory-inherited"><summary>Captured work order</summary><pre tabIndex={0}>{JSON.stringify(snapshot, null, 2)}</pre></details>
  </aside></div>
 </section>;
}
function StageInspector({ stage, detail, select }: { stage: FactoryStage; detail: FactoryRunDetail; select: (selection: FactorySelection) => void }) {
 const record = detail.stages.find(value => value.stageId === stage.id), branch = detail.parallelStages?.find(value => value.id === record?.id);
 return <><FactoryRunStatus state={record?.state ?? 'unavailable'}/>{record && <Timing startedAt={record.startedAt} finishedAt={record.finishedAt}/>}<p className="factory-help">Display group: {stage.group}. Scheduling follows declared prerequisites and step order.</p>{branch && <section className="factory-runtime-worktree"><h4>Isolated stage workspace</h4><code>{branch.workspacePath}</code><dl className="factory-runtime-facts"><div><dt>Input tree</dt><dd><code>{branch.input?.tree || 'Pending capture'}</code></dd></div><div><dt>Output tree</dt><dd><code>{branch.output?.tree || 'Not yet captured'}</code></dd></div><div><dt>Workspace checkpoint</dt><dd><code>{branch.checkpoint?.fingerprint || 'Not yet captured'}</code></dd></div></dl>{branch.error && <p className="factory-error">{branch.error}</p>}<details><summary>Step Git checkpoints ({branch.stepCheckpoints.length})</summary>{branch.stepCheckpoints.map(checkpoint => <dl className="factory-runtime-facts" key={checkpoint.stepRunId}><div><dt>Step</dt><dd>{detail.steps.find(step => step.id === checkpoint.stepRunId)?.stepId || checkpoint.stepRunId}</dd></div><div><dt>Execution HEAD</dt><dd><code>{checkpoint.context.headCommit}</code></dd></div><div><dt>Source tree</dt><dd><code>{checkpoint.files.tree}</code></dd></div><div><dt>Index artifact hash</dt><dd><code>{checkpoint.indexHash}</code></dd></div></dl>)}</details></section>}<h4>Prerequisites</h4>{stage.dependsOn.length ? <ul className="factory-runtime-prerequisites">{stage.dependsOn.map(id => <li key={id}><button type="button" onClick={() => select({ stageId: id })}>{detail.run.snapshot.environment.definition.stages.find(value => value.id === id)?.name || id}</button><FactoryRunStatus state={detail.stages.find(value => value.stageId === id)?.state ?? 'unavailable'}/></li>)}</ul> : <p className="factory-help">No prerequisites declared.</p>}<div className="factory-step-picker">{stage.steps.map(step => <button type="button" key={step.id} onClick={() => select({ stageId: stage.id, stepId: step.id })}>{step.name}<FactoryRunStatus state={detail.steps.find(value => value.stageRunId === record?.id && value.stepId === step.id)?.state ?? 'unavailable'}/></button>)}</div></>;
}
