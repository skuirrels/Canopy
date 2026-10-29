import { useEffect, useState } from 'react';
import { ArrowUpRight, Check, GitBranch, Loader2, RefreshCw, Save, Unplug } from 'lucide-react';
import { toast } from 'sonner';
import type { Item } from '../../lib/types';
import { useWorkspace } from '../../lib/workspace';
import { factoryRequest, patchOverride, resolveFactoryOverrides, taskFactorySignature, configurationFingerprint } from '../../lib/factory';
import type { TaskFactoryDetail, PipelineDetail, PipelineList, FactoryOverrides, StepOverride, FactoryValidation } from '../../lib/factory';
import { Button } from '../ui/button';
import { Dialog } from '../ui/dialog';
import { CommandFields, SkillFields } from './FactoryStepFields';
import './factory.css';

type TaskDraft = { pipelineId: string; versionId: string; overrides: FactoryOverrides; base: string };
const signature = taskFactorySignature;

export function TaskFactory({ item, beforeChange, onOpenDesigner }: { item: Item; beforeChange: () => Promise<void>; onOpenDesigner: (pipelineId?: string) => void }) {
 const { refresh } = useWorkspace();
 const [saved, setSaved] = useState<TaskFactoryDetail | null>(null), [list, setList] = useState<PipelineList | null>(null), [pipeline, setPipeline] = useState<PipelineDetail | null>(null);
 const [draft, setDraft] = useState<TaskDraft | null>(null), [stepKey, setStepKey] = useState(''), [validation, setValidation] = useState<FactoryValidation | null>(null);
 const [busy, setBusy] = useState(false), [loading, setLoading] = useState(true), [error, setError] = useState(''), [pipelineError, setPipelineError] = useState(''), [epoch, setEpoch] = useState(0);
 const [choice, setChoice] = useState<{ pipelineId?: string; versionId?: string } | null>(null), [detaching, setDetaching] = useState(false);
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
   setDraft(recovery ?? { pipelineId: configuration.version?.pipelineId ?? '', versionId: configuration.configuration?.pipelineVersionId ?? '', overrides: configuration.configuration?.overrides ?? { steps: [] }, base: signature(configuration) });
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
  try { if (dirty || draft.pipelineId !== (saved.version?.pipelineId ?? '')) localStorage.setItem(cacheKey, JSON.stringify(draft)); else localStorage.removeItem(cacheKey); }
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
 return <section className="task-factory" aria-label="Task factory configuration"><div className="factory-panel-heading"><h3><GitBranch size={16}/>Software Factory</h3><button type="button" className="text-button" onClick={() => { void beforeChange().then(() => onOpenDesigner(draft.pipelineId || undefined)).catch(cause => setError((cause as Error).message)); }}>Open designer<ArrowUpRight size={13}/></button></div><p className="factory-help">Attach a published pipeline and tailor this task. Saving does not run it or change its status.</p>
 {saved.version && <div className="factory-attached"><Check size={15}/><span>{saved.version.name}<small>Attached version {saved.version.number}{saved.archived ? ' · Archived' : saved.updateAvailable ? ' · Update available' : ''}</small></span></div>}
 {(error || pipelineError || stale) && <div role="alert" className="factory-error">{error || pipelineError || 'The server configuration changed. Your recovery copy has been retained. Discard it to reload the saved configuration.'}</div>}
 <fieldset disabled={busy} className="factory-step-fields"><label>Pipeline<select value={draft.pipelineId} onChange={e => select({ pipelineId: e.target.value })}><option value="">Choose a pipeline</option>{list.pipelines.map(pipeline => <option key={pipeline.id} value={pipeline.id} disabled={pipeline.archived && pipeline.id !== saved.version?.pipelineId}>{pipeline.name}{pipeline.archived ? ' · Archived' : ''}</option>)}</select></label>
 {pipeline && <><label>Published version<select value={draft.versionId} onChange={e => select({ versionId: e.target.value })}><option value="">Choose a published version</option>{pipeline.versions.map(version => <option key={version.id} value={version.id}>Version {version.number} · {version.name}</option>)}</select></label>{!pipeline.versions.length && <p className="factory-help">This pipeline has no published version. Open the designer to validate and publish it.</p>}{saved.updateAvailable && pipeline.pipeline.id === saved.version?.pipelineId && <button type="button" className="text-button" disabled={busy || draft.versionId === pipeline.versions.at(-1)?.id} onClick={() => select({ versionId: pipeline.versions.at(-1)?.id })}><RefreshCw size={13}/>Select latest published version</button>}{pipeline.pipeline.archived && <p className="factory-error">This pipeline is archived. Its existing attachment is retained; choose an active pipeline to change it.</p>}</>}
 {version && <><div className="factory-provenance"><small>Version source</small><p>{version.source}</p></div><label>Task-specific step settings<select aria-label="Step to override" value={stepKey} onChange={e => setStepKey(e.target.value)}><option value="">Choose a step to tailor</option>{steps.map(({ stage, step, key }) => <option key={key} value={key}>{stage.name} / {step.name}</option>)}</select></label>
 {selected && <fieldset className="factory-fieldset" disabled={pipeline?.pipeline.archived}><legend>{selected.step.name}</legend><p className="factory-help">Type: {selected.step.kind}{selected.step.role ? ` · ${selected.step.role} · ${selected.step.permission}` : ''}. Unchecked settings inherit the published version.</p>
 {(selected.step.kind === 'agent' || selected.step.kind === 'manual') && <><label className="factory-checkbox"><input type="checkbox" checked={override?.instructions !== undefined && override.instructions !== null} onChange={e => change({ instructions: e.target.checked ? selected.step.instructions ?? '' : null })}/>Override instructions</label>{override?.instructions !== undefined && override.instructions !== null && <label>Task instructions<textarea rows={5} value={override.instructions} onChange={e => change({ instructions: e.target.value })}/></label>}</>}
 {selected.step.kind === 'agent' && <><label>Agent override<select value={override?.provider ?? ''} onChange={e => change({ provider: e.target.value || null })}><option value="">Inherit stage agent</option><option value="codex">Codex</option><option value="claude">Claude</option></select></label>{(['model', 'effort'] as const).map(field => <div key={field}><label className="factory-checkbox"><input type="checkbox" checked={override?.[field] !== undefined && override[field] !== null} onChange={e => change({ [field]: e.target.checked ? selected.step[field] ?? '' : null })}/>Override {field}</label>{override?.[field] !== undefined && override[field] !== null && <label>Task {field}<input value={override[field]} placeholder="Leave blank for agent default" onChange={e => change({ [field]: e.target.value })}/></label>}</div>)}<label className="factory-checkbox"><input type="checkbox" checked={override?.skills !== undefined && override.skills !== null} onChange={e => change({ skills: e.target.checked ? structuredClone(selected.step.skills ?? []) : null })}/>Override skill references</label>{override?.skills !== undefined && override.skills !== null && <SkillFields skills={override.skills} onChange={skills => change({ skills })}/>}</>}
 {selected.step.kind === 'command' && <><label className="factory-checkbox"><input type="checkbox" checked={!!override?.command} onChange={e => change({ command: e.target.checked ? structuredClone(selected.step.command) : null })}/>Override command</label>{override?.command && <CommandFields command={override.command} onChange={command => change({ command })}/>}</>}
 <details className="factory-inherited"><summary>Published step settings</summary><pre>{JSON.stringify(selected.step, null, 2)}</pre></details></fieldset>}
 </>}
 </fieldset>
 {validation && <div className={validation.valid ? 'factory-validation valid' : 'factory-error'} aria-live="polite">{validation.valid ? 'Configuration valid. Execution readiness has not been checked.' : <ul>{validation.diagnostics.map((diagnostic, index) => <li key={index}>{diagnostic.message}</li>)}</ul>}</div>}
 <div className="factory-task-actions"><Button disabled={busy || !version || !dirty || stale || !!pipeline?.pipeline.archived} onClick={() => { void apply(); }}>{busy ? <Loader2 size={14} className="spin"/> : <Save size={14}/>}Apply configuration</Button>{saved.configuration && <Button variant="ghost" disabled={busy} onClick={() => setDetaching(true)}><Unplug size={14}/>Detach</Button>}{(dirty || draft.pipelineId !== (saved.version?.pipelineId ?? '') || stale) && <button type="button" className="text-button" disabled={busy} onClick={discard}>Discard local changes</button>}</div>
 <Dialog open={!!choice} onOpenChange={open => { if (!open) setChoice(null); }} title="Reset task overrides?" description="Choosing another pipeline or version clears these task overrides in the local form. Your saved attachment stays unchanged until you apply it."><div className="dialog-footer"><Button variant="ghost" onClick={() => setChoice(null)}>Keep current selection</Button><Button onClick={() => { if (choice) applyChoice(choice); }}>Switch and clear overrides</Button></div></Dialog>
 <Dialog open={detaching} onOpenChange={setDetaching} title="Detach this pipeline?" description="The task, its status and all published pipeline versions will be retained."><div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={() => setDetaching(false)}>Keep attachment</Button><Button disabled={busy} onClick={() => { void detach(); }}>Detach pipeline</Button></div></Dialog>
 </section>;
}
