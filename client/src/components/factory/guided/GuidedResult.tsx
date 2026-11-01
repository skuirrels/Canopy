import { useEffect, useRef, useState } from 'react';
import { ArrowRight, Check, Circle, Loader2, X } from 'lucide-react';
import type { ProjectSetup } from '../../../lib/factory-setup';
import { askForChanges, buildAgain, keepBuild, readBuild, startBuild, workingPhases } from '../../../lib/factory-builds';
import type { BuildPhase, BuildSummary } from '../../../lib/factory-builds';
import { Button } from '../../ui/button';

const progress: { phase: BuildPhase; label: string; detail: string }[] = [
 { phase: 'starting', label: 'Copy your code', detail: 'Making a separate copy, so nothing you have is touched.' },
 { phase: 'writing', label: 'Write the code', detail: 'Following the task description.' },
 { phase: 'testing', label: 'Run your tests', detail: 'Running the commands from step 2.' },
 { phase: 'checking', label: 'Check the work', detail: 'Reading the changes against the task description.' },
 { phase: 'yourTurn', label: 'Your turn', detail: 'Look at the result, then keep it or ask for changes.' },
];
const order = ['starting', 'writing', 'testing', 'checking', 'yourTurn'];

function Progress({ build, agent }: { build: BuildSummary; agent: string }) {
 const at = build.phase === 'failed' ? order.indexOf(build.failedWhile ?? 'starting') : build.phase === 'kept' ? order.length : order.indexOf(build.phase);
 return <ol className="guided-progress">{progress.map(step => {
  const index = order.indexOf(step.phase);
  const state = index < at ? 'done' : index === at ? (build.phase === 'failed' ? 'failed' : 'now') : 'next';
  return <li key={step.phase} className={state}>
   {state === 'done' ? <Check size={15}/> : state === 'failed' ? <X size={15}/> : state === 'now' && step.phase !== 'yourTurn' ? <Loader2 size={15} className="spin"/> : <Circle size={15}/>}
   <div><strong>{step.phase === 'writing' || step.phase === 'checking' ? `${agent}: ${step.label.toLowerCase()}` : step.label}</strong>{state === 'now' && <small>{step.detail}</small>}</div>
  </li>;
 })}</ol>;
}

// Agent notes are Markdown. Headings, bold and inline code are shown as such; everything else stays as written.
const inline = (line: string) => line.split(/(`[^`]+`|\*\*[^*]+\*\*)/g).map((part, index) =>
 part.startsWith('`') && part.endsWith('`') && part.length > 2 ? <code key={index}>{part.slice(1, -1)}</code>
 : part.startsWith('**') && part.endsWith('**') && part.length > 4 ? <strong key={index}>{part.slice(2, -2)}</strong> : part);
function Report({ text }: { text: string }) {
 return <div className="guided-report">{text.trim().split('\n').map((line, index) => /^#{1,6}\s/.test(line) ? <strong className="heading" key={index}>{line.replace(/^#+\s*/, '')}</strong> : <span key={index}>{inline(line)}{'\n'}</span>)}</div>;
}

function Files({ build }: { build: BuildSummary }) {
 const files = build.changes?.files ?? [];
 if (!build.changes) return null;
 return <section><h3>Files changed</h3>{files.length ? <ul className="guided-files">{files.map(file => <li key={file.path}><code>{file.path}</code><span>{file.new ? 'new' : ''}{file.added !== null && <span className="added"> +{file.added}</span>}{file.removed !== null && !file.new && <span className="removed"> −{file.removed}</span>}</span></li>)}</ul> : <p className="guided-muted">No files changed.</p>}{build.changes.truncated && <p className="guided-muted">Only the first {files.length} files are listed.</p>}</section>;
}

// Failures are reported at the end of test output, so it opens scrolled to the bottom.
const atEnd = (element: HTMLElement | null) => { if (element) element.scrollTop = element.scrollHeight; };

function Tests({ build }: { build: BuildSummary }) {
 if (!build.tests.length) return null;
 return <section><h3>Tests</h3><ul className="guided-test-results">{build.tests.map((test, index) => <li key={index} data-state={test.state}>
  <details open={test.state === 'failed' || undefined}>
   <summary><code>{test.command}</code><span>{test.state === 'succeeded' ? <><Check size={13}/>Passed</> : test.state === 'failed' ? <><X size={13}/>Failed{test.exitCode !== null ? ` (exit code ${test.exitCode})` : ''}</> : test.state === 'running' ? <><Loader2 size={13} className="spin"/>Running</> : 'Not run'}</span></summary>
   {test.output ? <pre ref={atEnd}>{test.output.replace(/^\[(stdout|stderr)\] ?/gm, '')}</pre> : <p className="guided-muted">No output.</p>}
  </details>
 </li>)}</ul></section>;
}

// Step 5: live progress, then the result with Keep or Ask for changes.
export function GuidedResult({ runId, setup, projectRevision, onFollow, onChanged, onBuildAnother, onOpenTask }: { runId: string; setup: ProjectSetup; projectRevision: number; onFollow: (runId: string) => void; onChanged: () => void; onBuildAnother: () => void; onOpenTask: (id: string) => void }) {
 const [build, setBuild] = useState<BuildSummary | null>(null), [error, setError] = useState(''), [actionError, setActionError] = useState('');
 const [keepFailed, setKeepFailed] = useState(false), [tick, setTick] = useState(0), [busy, setBusy] = useState(false), [asking, setAsking] = useState(false), [note, setNote] = useState('');
 const shown = useRef<BuildPhase | null>(null);
 const agent = build?.agent ?? setup.providers.find(p => p.id === setup.connection?.provider)?.name ?? 'The agent';
 const working = !build || workingPhases.has(build.phase);
 useEffect(() => {
  const abort = new AbortController(); let timer: number | undefined;
  const load = () => readBuild(runId, abort.signal).then(value => {
   if (abort.signal.aborted) return;
   if (shown.current && shown.current !== value.phase) onChanged();
   shown.current = value.phase; setBuild(value); setError('');
   if (value.phase === 'changesRequested' && value.nextRunId) { onFollow(value.nextRunId); return; }
   if (workingPhases.has(value.phase)) timer = window.setTimeout(() => { void load(); }, 2000);
  }).catch(cause => { if (abort.signal.aborted) return; setError((cause as Error).message); timer = window.setTimeout(() => { void load(); }, 5000); });
  void load();
  return () => { abort.abort(); window.clearTimeout(timer); };
 }, [runId, tick, onFollow, onChanged]);
 useEffect(() => { setBuild(null); setAsking(false); setNote(''); setActionError(''); setKeepFailed(false); }, [runId]);

 async function act(action: () => Promise<BuildSummary>) {
  setBusy(true); setActionError('');
  try { const value = await action(); onChanged(); if (value.runId !== runId) onFollow(value.runId); else { shown.current = value.phase; setBuild(value); if (workingPhases.has(value.phase)) setTick(x => x + 1); } setAsking(false); setNote(''); return true; }
  catch (cause) { setActionError((cause as Error).message); return false; }
  finally { setBusy(false); }
 }
 if (!build) return error ? <p role="alert" className="guided-error">{error}</p> : <p className="guided-muted"><Loader2 size={14} className="spin"/> Loading the build…</p>;
 const connection = setup.connection;
 const heading = build.phase === 'kept' ? `Kept “${build.title}”` : build.phase === 'yourTurn' ? 'Ready for you to check' : build.phase === 'failed' ? 'The build stopped' : build.phase === 'stopped' ? 'The build was stopped' : working ? `Building “${build.title}”` : build.title;
 const failedTest = build.failedWhile === 'testing' ? build.tests.find(test => test.state === 'failed') : undefined;
 const tryAgain = () => act(() => startBuild(build.itemId, projectRevision));

 return <>
  <h2>{heading}</h2>
  {build.changeRequest && <p className="guided-note">You asked for changes: “{build.changeRequest}”</p>}
  {error && <p role="alert" className="guided-error">Couldn’t refresh: {error}</p>}
  {build.phase !== 'kept' && build.phase !== 'stopped' && <Progress build={build} agent={agent}/>}
  {working && <p className="guided-muted">This takes a few minutes. You can leave this page. The build carries on, and the steps list shows when it’s your turn.</p>}

  {build.phase === 'kept' && <>
   <p className="guided-lead">The changes are merged into <code>{build.keptInto}</code>{build.keptCommit && <> as <code>{build.keptCommit.slice(0, 7)}</code></>}{connection && <> in <code>{connection.repositoryPath}</code></>}{build.taskDone ? ', and the task is marked done.' : '.'}</p>
   <div className="guided-actions"><Button onClick={onBuildAnother}>Build another task<ArrowRight size={14}/></Button></div>
  </>}

  {build.phase === 'failed' && <div className="guided-problem" role="alert">
   <strong>{build.failedWhile === 'testing' ? 'A test failed.' : build.failedWhile === 'writing' ? `${agent} couldn’t finish writing the code.` : build.failedWhile === 'checking' ? 'The check of the work couldn’t finish.' : 'The build couldn’t start.'}</strong>
   {failedTest ? <p><code>{failedTest.command}</code> failed. Its output is below.</p> : build.problem && <p>{build.problem}</p>}
  </div>}
  {build.phase === 'stopped' && <p className="guided-lead">{build.problem ?? 'This build was stopped before it finished.'} Nothing was changed in your code.</p>}

  {(build.phase === 'yourTurn' || build.phase === 'failed') && build.builderReport && <section><h3>What {agent} says it did</h3><Report text={build.builderReport}/></section>}
  {!working && build.phase !== 'kept' && <Files build={build}/>}
  {(build.phase === 'failed' || !working) && build.phase !== 'kept' && <Tests build={build}/>}
  {build.phase === 'yourTurn' && build.reviewReport && <section><h3>Notes from the check</h3><Report text={build.reviewReport}/></section>}

  {(build.phase === 'yourTurn' || build.phase === 'failed') && (asking ? <div className="guided-ask">
   <label>What should change?<textarea autoFocus value={note} placeholder={build.phase === 'failed' ? 'For example: fix the failing test' : 'For example: also handle negative numbers'} onChange={e => setNote(e.target.value)}/></label>
   <p className="guided-muted">{agent} starts again from this result, with your note.</p>
   {actionError && <p role="alert" className="guided-error">{actionError}</p>}
   <div className="guided-actions"><Button variant="ghost" disabled={busy} onClick={() => setAsking(false)}>Cancel</Button><Button disabled={busy} blockedReason={!note.trim() && 'Say what should change.'} onClick={() => { void act(() => askForChanges(build, note.trim())); }}>{busy && <Loader2 size={14} className="spin"/>}Send back</Button></div>
  </div> : <>
   {build.phase === 'yourTurn' && connection && <p className="guided-muted guided-keep-note">Keeping merges this into <code>{connection.baseRef}</code> in <code>{connection.repositoryPath}</code> and marks the task done.</p>}
   {actionError && <p role="alert" className="guided-error">{actionError}</p>}
   <div className="guided-actions">
    {build.phase === 'failed' && <Button variant="ghost" onClick={() => onOpenTask(build.itemId)}>Edit the task</Button>}
    {build.phase === 'failed' && <Button variant="secondary" disabled={busy} onClick={() => { void tryAgain(); }}>Try again</Button>}
    <Button variant={build.phase === 'failed' ? 'default' : 'secondary'} disabled={busy} onClick={() => setAsking(true)}>Ask for changes</Button>
    {build.phase === 'yourTurn' && keepFailed && <Button variant="secondary" disabled={busy} onClick={() => { void act(() => buildAgain(build)); }}>Build again from the latest {connection?.baseRef ?? 'version'}</Button>}
    {build.phase === 'yourTurn' && <Button disabled={busy} onClick={() => { void act(() => keepBuild(build)).then(kept => setKeepFailed(!kept)); }}>{busy ? <Loader2 size={14} className="spin"/> : <Check size={14}/>}Keep the changes</Button>}
   </div>
  </>)}
  {build.phase === 'stopped' && <div className="guided-actions"><Button disabled={busy} onClick={() => { void tryAgain(); }}>Build it again</Button></div>}
 </>;
}
