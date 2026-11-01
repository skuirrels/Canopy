import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertCircle, Check, Loader2, Settings2 } from 'lucide-react';
import type { Item, Project } from '../../../lib/types';
import { useWorkspace } from '../../../lib/workspace';
import { factoryRequest } from '../../../lib/factory';
import type { ProjectSetup } from '../../../lib/factory-setup';
import type { SkillCatalogue } from '../../../lib/factory-skills';
import { needsSpecification } from '../../../lib/factory-readiness';
import { needsAttention, phaseLabel, projectBuilds } from '../../../lib/factory-builds';
import type { BuildListItem, BuildSummary } from '../../../lib/factory-builds';
import { Button } from '../../ui/button';
import { GuidedCode } from './GuidedCode';
import { GuidedTests } from './GuidedTests';
import { GuidedTasks } from './GuidedTasks';
import { GuidedBuild } from './GuidedBuild';
import { GuidedResult } from './GuidedResult';
import './guided.css';

const steps = ['Your code', 'How to test it', 'Choose a task', 'Build it', 'Check the result'];
const hasTests = (setup: ProjectSetup) => !!setup.version?.definition.stages.some(s => s.steps.some(x => x.kind === 'command'));

// The factory's main path: five numbered steps on the left, one at a time on the right.
export function GuidedFactory({ project, items, onOpenTask, onAddTask, onAdvanced }: { project: Project; items: Item[]; onOpenTask: (id: string) => void; onAddTask: () => void; onAdvanced: () => void }) {
 const { refresh } = useWorkspace();
 const [setup, setSetup] = useState<ProjectSetup | null>(null), [catalogue, setCatalogue] = useState<SkillCatalogue | null>(null), [builds, setBuilds] = useState<BuildListItem[]>([]);
 const [error, setError] = useState(''), [epoch, setEpoch] = useState(0), [buildsEpoch, setBuildsEpoch] = useState(0);
 const [step, setStep] = useState<number | null>(null), [taskId, setTaskId] = useState<string | null>(null), [runId, setRunId] = useState<string | null>(null);
 const opened = useRef(false);

 const loadSetup = useCallback(async (signal?: AbortSignal) => {
  const value = await factoryRequest<ProjectSetup>(`/projects/${project.id}/setup`, 'GET', undefined, signal);
  if (!signal?.aborted) setSetup(value);
  return value;
 }, [project.id]);
 useEffect(() => {
  const abort = new AbortController();
  void Promise.all([loadSetup(abort.signal), projectBuilds(project.id, abort.signal)]).then(([value, list]) => {
   if (abort.signal.aborted) return;
   setBuilds(list); setError('');
   // Open where the person left off: unfinished setup first, then a build that needs them, then the task list.
   if (opened.current) return;
   opened.current = true;
   const active = list.find(build => needsAttention(build.phase) && build.phase !== 'failed');
   if (!value.connection) setStep(0);
   else if (!hasTests(value)) setStep(1);
   else if (active) { setRunId(active.runId); setTaskId(active.itemId); setStep(4); }
   else setStep(2);
  }).catch(cause => { if (!abort.signal.aborted) setError((cause as Error).message); });
  return () => abort.abort();
 }, [project.id, project.revision, epoch, loadSetup]);
 useEffect(() => {
  if (!buildsEpoch) return;
  const abort = new AbortController();
  void projectBuilds(project.id, abort.signal).then(list => { if (!abort.signal.aborted) setBuilds(list); }).catch(() => { /* The list refreshes again on the next change. */ });
  return () => abort.abort();
 }, [project.id, buildsEpoch]);
 useEffect(() => {
  const abort = new AbortController();
  void factoryRequest<SkillCatalogue>(`/projects/${project.id}/skills`, 'GET', undefined, abort.signal).then(value => { if (!abort.signal.aborted) setCatalogue(value); }).catch(() => { /* Saving then keeps only stored skills. */ });
  return () => abort.abort();
 }, [project.id]);

 const buildChanged = useCallback(() => { setBuildsEpoch(x => x + 1); void refresh(); }, [refresh]);
 const follow = useCallback((id: string) => setRunId(id), []);
 const openBuild = (build: { runId: string; itemId: string }) => { setRunId(build.runId); setTaskId(build.itemId); setStep(4); };

 if (error && !setup) return <div className="guided-root"><p role="alert" className="guided-error"><AlertCircle size={15}/> {error} <button type="button" className="text-button" onClick={() => { setError(''); setEpoch(x => x + 1); }}>Try again</button></p></div>;
 if (!setup || step === null) return <div className="guided-root"><p className="guided-muted"><Loader2 size={15} className="spin"/> Loading…</p></div>;

 const provider = setup.providers.find(p => p.id === setup.connection?.provider);
 const codeReady = !!setup.connection && provider?.available !== false, testsReady = hasTests(setup);
 const item = items.find(x => x.id === taskId);
 const current = builds.find(build => build.runId === runId);
 const blocked = [undefined, !codeReady && 'Choose your code folder first.', !testsReady && 'Add a test command first.', !item && 'Choose a task first.', !runId && 'Build a task first.'];
 const done = [codeReady, testsReady, !!item, !!runId, current?.phase === 'kept'];
 const waiting = builds.filter(build => needsAttention(build.phase));

 return <div className="guided-root">
  <div className="guided">
   <nav className="guided-nav" aria-label="Build steps">
    <p className="guided-eyebrow">Build a task with {provider?.name ?? 'an agent'}</p>
    <ol>{steps.map((label, index) => {
     const reason = blocked.slice(0, index + 1).find(Boolean) || undefined, firstBlocked = blocked.findIndex(Boolean) === index;
     return <li key={label}>
      <button type="button" className={index === step ? 'active' : ''} aria-current={index === step ? 'step' : undefined} disabled={!!reason} onClick={() => setStep(index)}>
       <span className="guided-dot">{done[index] ? <Check size={13}/> : index + 1}</span><span>{label}{firstBlocked && <small>{reason}</small>}</span>
      </button>
     </li>;
    })}</ol>
    <p className="guided-muted">Steps 1 and 2 happen once per project.</p>
    {waiting.length > 0 && <div className="guided-waiting"><h3>Builds</h3><ul>{waiting.map(build => <li key={build.runId}>
     <button type="button" className={build.runId === runId && step === 4 ? 'active' : ''} onClick={() => openBuild(build)}><strong>{build.title}</strong><small data-phase={build.phase}>{phaseLabel[build.phase]}</small></button>
    </li>)}</ul></div>}
    <Button variant="ghost" size="sm" className="guided-advanced" onClick={onAdvanced}><Settings2 size={14}/>Advanced tools</Button>
   </nav>
   <section className="guided-panel" aria-live="polite">
    {step === 0 && <GuidedCode projectId={project.id} projectRevision={project.revision} setup={setup} onSaved={async () => { await loadSetup(); await refresh(); }} onNext={() => setStep(1)}/>}
    {step === 1 && <GuidedTests key={setup.version?.id ?? 'new'} projectId={project.id} setup={setup} catalogue={catalogue} onSaved={async () => { await loadSetup(); void refresh(); setStep(2); }}/>}
    {step === 2 && <GuidedTasks items={items} builds={builds} needsDescription={needsSpecification(setup.version?.definition)} onChoose={id => { setTaskId(id); setRunId(null); setStep(3); }} onOpenTask={onOpenTask} onAddTask={onAddTask}/>}
    {step === 3 && item && <GuidedBuild item={item} setup={setup} projectRevision={project.revision} onStarted={(build: BuildSummary) => { setBuildsEpoch(x => x + 1); openBuild(build); }} onChangeTests={() => setStep(1)} onChooseAnother={() => setStep(2)} onOpenTask={onOpenTask}/>}
    {step === 4 && runId && <GuidedResult runId={runId} setup={setup} projectRevision={project.revision} onFollow={follow} onChanged={buildChanged} onBuildAnother={() => { setTaskId(null); setRunId(null); setStep(2); }} onOpenTask={onOpenTask}/>}
   </section>
  </div>
 </div>;
}
