import { useEffect, useState } from 'react';
import { ArrowUpRight, Check, GitBranch, Loader2, RefreshCw, Save, Unplug } from 'lucide-react';
import { toast } from 'sonner';
import type { Item } from '../../lib/types';
import { useWorkspace } from '../../lib/workspace';
import { factoryRequest, patchOverride, resolveFactoryOverrides, taskFactorySignature, configurationFingerprint } from '../../lib/factory';
import type { TaskFactoryDetail, PipelineDetail, PipelineList, FactoryOverrides, StepOverride, FactoryValidation } from '../../lib/factory';
import { Button } from '../ui/button';
import { blockedBy } from '../../lib/blocked';
import { openPrerequisites } from '../../lib/factory-readiness';
import { Dialog } from '../ui/dialog';
import { SkillFields } from './FactorySkillPicker';
import { CommandFields } from './FactoryStepFields';
import './factory.css';
import { TaskAcceptanceEditor } from './TaskAcceptanceEditor';
import { TaskFactoryRuns } from './TaskFactoryRuns';
import { FactorySetupDialog } from './FactorySetupDialog';
import { FactoryWorkflowPreview } from './FactoryWorkflowPreview';
import './factory-setup.css';
import { factoryDesignerRequested } from '../../lib/factory-events';

type TaskDraft = { pipelineId: string; versionId: string; overrides: FactoryOverrides; base: string };
const signature = taskFactorySignature;
// The pipeline this task chose itself; a task following the project workflow has none.
const ownPipelineId = (value: TaskFactoryDetail) => value.configuration ? value.version?.pipelineId ?? '' : '';

export function TaskFactory({ item, beforeChange, onOpenDesigner, onOpenRun, onOpenTask }: { item: Item; onOpenRun: (id: string) => void; beforeChange: () => Promise<void>; onOpenDesigner: (pipelineId?: string) => void; onOpenTask?: (id: string) => void }) {
 const { refresh, data } = useWorkspace();
 const waitingOn = openPrerequisites(item, data);
 const [saved, setSaved] = useState<TaskFactoryDetail | null>(null), [list, setList] = useState<PipelineList | null>(null), [pipeline, setPipeline] = useState<PipelineDetail | null>(null);
 const [draft, setDraft] = useState<TaskDraft | null>(null), [stepKey, setStepKey] = useState(''), [validation, setValidation] = useState<FactoryValidation | null>(null);
 const [busy, setBusy] = useState(false), [loading, setLoading] = useState(true), [error, setError] = useState(''), [pipelineError, setPipelineError] = useState(''), [epoch, setEpoch] = useState(0);
 const [choice, setChoice] = useState<{ pipelineId?: string; versionId?: string } | null>(null), [detaching, setDetaching] = useState(false);
 const [criteriaDirty, setCriteriaDirty] = useState(false), [setupOpen, setSetupOpen] = useState(false);
 const cacheKey = `canopy:factory-task-draft:${item.id}`;
 useEffect(() => {
  const controller = new AbortController(); setLoading(true); setError('');
  void Promise.all([factoryRequest<TaskFactoryDetail>(`/tasks/${item.id}/configuration`, 'GET', undefined, controller.signal), factoryRequest<PipelineList>(`/projects/${item.projectId}/pipelines`, 'GET', undefined, controller.signal)]).then(([configuration, catalogue]) => {
   if (controller.signal.aborted) return;
   setSaved(configuration); setList(catalogue);
   let recovery: TaskDraft | null = null;
   try {
    const value = JSON.parse(localStorage.getItem(cacheKey) || 'null');
    if (value && typeof value.pipelineId === 'string' && typeof value.versionId === 'string' && typeof value.base === 'string' && Array.isArray(value.overrides?.steps) && value.overrides.steps.every((step: StepOverride) => step && typeof step.stageId === 'string' && typeof step.stepId === 'string')) recovery = { ...value, base: configurationFingerprint(JSON.parse(value.base)) };
   } catch { /* Use the server configuration when there is no readable recovery copy. */ }
   if (recovery && configurationFingerprint({ versionId: recovery.versionId, overrides: recovery.overrides }) === signature(configuration)) recovery.base = signature(configuration);
   setDraft(recovery ?? { pipelineId: configuration.configuration ? configuration.version?.pipelineId ?? '' : '', versionId: configuration.configuration?.pipelineVersionId ?? '', overrides: configuration.configuration?.overrides ?? { steps: [] }, base: signature(configuration) });
  }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
  return () => controller.abort();
 }, [item.id, item.projectId, cacheKey, epoch]);
 const pipelineId = draft?.pipelineId ?? '';
 useEffect(() => {
  const controller = new AbortController(); setPipeline(null); setPipelineError('');
  if (pipelineId) void factoryRequest<PipelineDetail>(`/pipelines/${pipelineId}`, 'GET', undefined, controller.signal).then(value => { if (!controller.signal.aborted) setPipeline(value); }).catch(cause => { if (!controller.signal.aborted) setPipelineError((cause as Error).message); });
  return () => controller.abort();
 }, [pipelineId, epoch]);
 const dirty = !!saved && !!draft && configurationFingerprint({ versionId: draft.versionId, overrides: draft.overrides }) !== signature(saved);
 const stale = !!saved && !!draft && draft.base !== signature(saved);
 useEffect(() => {
  if (!draft || !saved) return;
  try { if (dirty || draft.pipelineId !== ownPipelineId(saved)) localStorage.setItem(cacheKey, JSON.stringify(draft)); else localStorage.removeItem(cacheKey); }
  catch { setError('Browser recovery is unavailable. Apply your configuration before closing task details.'); }
 }, [draft, saved, dirty, cacheKey]);
 const version = pipeline?.versions.find(version => version.id === draft?.versionId);
 const steps = version?.definition.stages.flatMap(stage => stage.steps.map(step => ({ stage, step, key: `${stage.id}/${step.id}` }))) ?? [];
 const selected = steps.find(value => value.key === stepKey);
 const override = draft?.overrides.steps.find(value => value.stageId === selected?.stage.id && value.stepId === selected?.step.id);
 function change(field: Partial<StepOverride>) { if (!draft || !selected) return; setDraft({ ...draft, overrides: patchOverride(draft.overrides, selected.stage.id, selected.step.id, field) }); setValidation(null); }
 function select(value: { pipelineId?: string; versionId?: string }) {
  if (!draft) return;
  if (draft.overrides.steps.length) setChoice(value); else applyChoice(value);
 }
 function applyChoice(value: { pipelineId?: string; versionId?: string }) {
  if (!draft) return;
  setDraft({ ...draft, pipelineId: value.pipelineId ?? draft.pipelineId, versionId: value.pipelineId !== undefined ? '' : value.versionId ?? draft.versionId, overrides: { steps: [] } }); setStepKey(''); setValidation(null); setChoice(null);
 }
 async function apply() {
  if (!draft || !version || stale) return; setBusy(true); setError('');
  try {
   const result = await factoryRequest<FactoryValidation>('/validate', 'POST', resolveFactoryOverrides(version.definition, draft.overrides)); setValidation(result);
   if (!result.valid) return;
   await beforeChange();
   const current = await factoryRequest<TaskFactoryDetail>(`/tasks/${item.id}/configuration`);
   if (signature(current) !== draft.base) { setSaved(current); throw new Error('The saved task configuration changed. Your local edits have been retained; discard them to reload it.'); }
   const value = await factoryRequest<TaskFactoryDetail>(`/tasks/${item.id}/configuration`, 'PUT', { expectedRevision: current.projectRevision, pipelineVersionId: draft.versionId, overrides: draft.overrides });
   setSaved(value); setDraft({ ...draft, base: signature(value) }); await refresh(); toast.success('Task factory configuration saved');
  } catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 async function detach() {
  setBusy(true); setError('');
  try {
   await beforeChange();
   const current = await factoryRequest<TaskFactoryDetail>(`/tasks/${item.id}/configuration`);
   if (draft && signature(current) !== draft.base) { setSaved(current); throw new Error('The task attachment changed. Reload it before detaching.'); }
   const value = await factoryRequest<TaskFactoryDetail>(`/tasks/${item.id}/configuration?expectedRevision=${current.projectRevision}`, 'DELETE');
   setSaved(value); setDraft({ pipelineId: '', versionId: '', overrides: { steps: [] }, base: signature(value) }); setStepKey(''); setValidation(null); setDetaching(false); await refresh(); toast.success('Pipeline detached');
  } catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 function discard() { try { localStorage.removeItem(cacheKey); } catch { /* Reload the server state even if browser storage is unavailable. */ } setEpoch(value => value + 1); setValidation(null); }
 if (loading) return <div className="task-factory"><Loader2 size={16} className="spin"/>Loading task configuration…</div>;
 if (!saved || !draft || !list) return <div className="task-factory"><p role="alert" className="factory-error">{error || 'Factory configuration unavailable.'}</p><Button variant="secondary" onClick={() => setEpoch(value => value + 1)}>Retry</Button></div>;
 return <section className="task-factory" aria-label="Task factory configuration"><div className="factory-panel-heading"><h3><GitBranch size={16}/>Build with Factory</h3></div>
 {!saved.version ? <div className="factory-task-welcome"><span className="factory-eyebrow">FROM TASK TO VERIFIED RESULT</span><h4>Set up the factory to build this task.</h4><p>Connect the repository and add its build and test checks once. Every task in this project then uses that workflow.</p><Button disabled={busy} blockedReason={blockedBy([criteriaDirty, 'Save or discard your acceptance criteria first.'], [dirty || !!draft.pipelineId, 'Apply or discard your Advanced configuration first.'])} onClick={() => { void beforeChange().then(() => setSetupOpen(true)).catch(cause => setError((cause as Error).message)); }}>Set up factory<ArrowUpRight size={14}/></Button></div> : <><div className="factory-attached"><Check size={15}/><span>{saved.version.name}<small>{saved.inherited ? 'Your project workflow · change it in Project setup, or tailor this task under Advanced' : saved.archived ? 'Archived workflow · choose another under Advanced' : saved.updateAvailable ? 'This task uses its own workflow · a newer version is available under Advanced' : 'This task uses its own workflow · review criteria, then prepare your build'}</small></span></div>{saved.resolvedDefinition && <FactoryWorkflowPreview definition={saved.resolvedDefinition}/>}</>}
 {(error || pipelineError || stale) && <div role="alert" className="factory-error">{error || pipelineError || 'The server configuration changed. Your recovery copy has been retained. Discard it to reload the saved configuration.'}</div>}
 <details className="factory-advanced" open={stale || undefined}><summary>Advanced configuration</summary><p className="factory-help">Choose another published workflow or tailor individual steps. Existing work orders keep their captured configuration.</p><button type="button" className="text-button" onClick={() => { void beforeChange().then(() => { onOpenDesigner(draft.pipelineId || undefined); factoryDesignerRequested(item.projectId, draft.pipelineId || undefined); }).catch(cause => setError((cause as Error).message)); }}>Open advanced designer<ArrowUpRight size={13}/></button>
 <fieldset disabled={busy} className="factory-step-fields"><label>Pipeline<select value={draft.pipelineId} onChange={e => select({ pipelineId: e.target.value })}><option value="">Choose a pipeline</option>{list.pipelines.map(pipeline => <option key={pipeline.id} value={pipeline.id} disabled={pipeline.archived && pipeline.id !== saved.version?.pipelineId}>{pipeline.name}{pipeline.archived ? ' · Archived' : ''}</option>)}</select></label>
 {pipeline && <><label>Published version<select value={draft.versionId} onChange={e => select({ versionId: e.target.value })}><option value="">Choose a published version</option>{pipeline.versions.map(version => <option key={version.id} value={version.id}>Version {version.number} · {version.name}</option>)}</select></label>{!pipeline.versions.length && <p className="factory-help">This pipeline has no published version. Open the designer to validate and publish it.</p>}{saved.updateAvailable && pipeline.pipeline.id === saved.version?.pipelineId && draft.versionId !== pipeline.versions.at(-1)?.id && <button type="button" className="text-button" disabled={busy} onClick={() => select({ versionId: pipeline.versions.at(-1)?.id })}><RefreshCw size={13}/>Select latest published version</button>}{pipeline.pipeline.archived && <p className="factory-error">This pipeline is archived. Its existing attachment is retained; choose an active pipeline to change it.</p>}</>}
 {version && <><div className="factory-provenance"><small>Version source</small><p>{version.source}</p></div><label>Task-specific step settings<select aria-label="Step to override" value={stepKey} onChange={e => setStepKey(e.target.value)}><option value="">Choose a step to tailor</option>{steps.map(({ stage, step, key }) => <option key={key} value={key}>{stage.name} / {step.name}</option>)}</select></label>
 {selected && <fieldset className="factory-fieldset" disabled={pipeline?.pipeline.archived}><legend>{selected.step.name}</legend><p className="factory-help">Type: {selected.step.kind}{selected.step.role ? ` · ${selected.step.role} · ${selected.step.permission}` : ''}. Unchecked settings inherit the published version.</p>
 {(selected.step.kind === 'agent' || selected.step.kind === 'manual') && <><label className="factory-checkbox"><input type="checkbox" checked={override?.instructions !== undefined && override.instructions !== null} onChange={e => change({ instructions: e.target.checked ? selected.step.instructions ?? '' : null })}/>Override instructions</label>{override?.instructions !== undefined && override.instructions !== null && <label>Task instructions<textarea rows={5} value={override.instructions} onChange={e => change({ instructions: e.target.value })}/></label>}</>}
 {selected.step.kind === 'agent' && <><label>Agent override<select value={override?.provider ?? ''} onChange={e => change({ provider: e.target.value || null })}><option value="">Inherit stage agent</option><option value="codex">Codex</option><option value="claude">Claude</option><option value="gemini">Gemini</option></select></label>{(['model', 'effort'] as const).map(field => <div key={field}><label className="factory-checkbox"><input type="checkbox" checked={override?.[field] !== undefined && override[field] !== null} onChange={e => change({ [field]: e.target.checked ? selected.step[field] ?? '' : null })}/>Override {field}</label>{override?.[field] !== undefined && override[field] !== null && <label>Task {field}<input value={override[field]} placeholder="Leave blank for agent default" onChange={e => change({ [field]: e.target.value })}/></label>}</div>)}<label className="factory-checkbox"><input type="checkbox" checked={override?.skills !== undefined && override.skills !== null} onChange={e => change({ skills: e.target.checked ? structuredClone(selected.step.skills ?? []) : null })}/>Override skill references</label>{override?.skills !== undefined && override.skills !== null && <SkillFields projectId={item.projectId} skills={override.skills} onChange={skills => change({ skills })}/>}</>}
 {selected.step.kind === 'command' && <><label className="factory-checkbox"><input type="checkbox" checked={!!override?.command} onChange={e => change({ command: e.target.checked ? structuredClone(selected.step.command) : null })}/>Override command</label>{override?.command && <CommandFields command={override.command} onChange={command => change({ command })}/>}</>}
 <details className="factory-inherited"><summary>Published step settings</summary><pre>{JSON.stringify(selected.step, null, 2)}</pre></details></fieldset>}
 </>}
 </fieldset>
 {validation && <div className={validation.valid ? 'factory-validation valid' : 'factory-error'} aria-live="polite">{validation.valid ? 'Configuration valid. Execution readiness has not been checked.' : <ul>{validation.diagnostics.map((diagnostic, index) => <li key={index}>{diagnostic.message}</li>)}</ul>}</div>}
 <div className="factory-task-actions"><Button disabled={busy} blockedReason={blockedBy([stale, 'Discard your local copy to reload the saved configuration.'], [!draft.pipelineId, 'Choose a pipeline.'], [!version, 'Choose a published version.'], [!!pipeline?.pipeline.archived, 'This pipeline is archived. Choose an active one.'], [!dirty, 'Change a setting to apply it.'])} onClick={() => { void apply(); }}>{busy ? <Loader2 size={14} className="spin"/> : <Save size={14}/>}Apply configuration</Button>{saved.configuration && <Button variant="ghost" disabled={busy} onClick={() => setDetaching(true)}><Unplug size={14}/>Detach</Button>}{(dirty || draft.pipelineId !== ownPipelineId(saved) || stale) && <button type="button" className="text-button" disabled={busy} onClick={discard}>Discard local changes</button>}</div>
 </details>
 <TaskAcceptanceEditor key={item.id} itemId={item.id} beforeChange={beforeChange} onDirtyChange={setCriteriaDirty}/>
 {setupOpen && <FactorySetupDialog projectId={item.projectId} onClose={() => setSetupOpen(false)} onSaved={() => { setSetupOpen(false); discard(); }}/>}
 {waitingOn.length > 0 && <div className="factory-waiting" role="note"><strong>Waiting on {waitingOn.length === 1 ? 'a prerequisite' : `${waitingOn.length} prerequisites`}</strong><p>Finish {waitingOn.length === 1 ? 'this task' : 'these tasks'} (move {waitingOn.length === 1 ? 'it' : 'them'} to a completed column) before building this one.</p><ul>{waitingOn.map(other => <li key={other.id}>{onOpenTask ? <button type="button" className="text-button" onClick={() => onOpenTask(other.id)}>{other.title}<ArrowUpRight size={13}/></button> : other.title}</li>)}</ul></div>}
 <TaskFactoryRuns configured={!!saved.version} waitingReason={waitingOn.length ? `Finish ${waitingOn.length === 1 ? waitingOn[0].title : `its ${waitingOn.length} prerequisites`} first.` : undefined} itemId={item.id} onOpenRun={onOpenRun} dirty={criteriaDirty || dirty || draft.pipelineId !== ownPipelineId(saved) || stale} beforeChange={beforeChange}/>
 <Dialog open={!!choice} onOpenChange={open => { if (!open) setChoice(null); }} title="Reset task overrides?" description="Choosing another pipeline or version clears these task overrides in the local form. Your saved attachment stays unchanged until you apply it."><div className="dialog-footer"><Button variant="ghost" onClick={() => setChoice(null)}>Keep current selection</Button><Button onClick={() => { if (choice) applyChoice(choice); }}>Switch and clear overrides</Button></div></Dialog>
 <Dialog open={detaching} onOpenChange={setDetaching} title="Detach this pipeline?" description="The task, its status and all published pipeline versions will be retained."><div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setDetaching(false)}>Keep attachment</Button><Button disabled={busy} onClick={() => { void detach(); }}>Detach pipeline</Button></div></Dialog>
 </section>;
}
