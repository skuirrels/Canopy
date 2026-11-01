import { useEffect, useState } from 'react';
import { Hammer, Loader2 } from 'lucide-react';
import type { Item } from '../../../lib/types';
import { factoryRequest } from '../../../lib/factory';
import type { ProjectSetup } from '../../../lib/factory-setup';
import { commandDraft } from '../../../lib/factory-setup';
import { specificationText } from '../../../lib/epics';
import { startBuild } from '../../../lib/factory-builds';
import type { BuildSummary } from '../../../lib/factory-builds';
import { Button } from '../../ui/button';

type Readiness = { projectRevision: number; canPrepare: boolean; checks: { code: string; severity: string; message: string }[] };

// Step 4: what pressing Build will do, and why it can't start yet when it can't.
export function GuidedBuild({ item, setup, projectRevision, onStarted, onChangeTests, onChooseAnother, onOpenTask }: { item: Item; setup: ProjectSetup; projectRevision: number; onStarted: (build: BuildSummary) => void; onChangeTests: () => void; onChooseAnother: () => void; onOpenTask: (id: string) => void }) {
 const [readiness, setReadiness] = useState<Readiness | null>(null), [busy, setBusy] = useState(false), [error, setError] = useState('');
 useEffect(() => {
  const abort = new AbortController(); setReadiness(null);
  void factoryRequest<Readiness>(`/tasks/${item.id}/readiness`, 'GET', undefined, abort.signal).then(value => { if (!abort.signal.aborted) setReadiness(value); }).catch(cause => { if (!abort.signal.aborted) setError((cause as Error).message); });
  return () => abort.abort();
 }, [item.id, projectRevision]);
 const agent = setup.providers.find(p => p.id === setup.connection?.provider)?.name ?? 'The agent';
 const tests = setup.version?.definition.stages.flatMap(s => s.steps.filter(x => x.kind === 'command' && x.command).map(x => commandDraft(x.command!).line)) ?? [];
 const description = specificationText(item.specJson).trim();
 const problem = readiness?.checks.find(check => check.severity === 'error')?.message;
 async function build() {
  setBusy(true); setError('');
  try { onStarted(await startBuild(item.id, readiness?.projectRevision ?? projectRevision)); }
  catch (cause) { setError((cause as Error).message); }
  finally { setBusy(false); }
 }
 return <>
  <h2>Build “{item.title}”</h2>
  {description ? <blockquote className="guided-description">{description.length > 600 ? `${description.slice(0, 600)}…` : description}</blockquote> : <p className="guided-note">This task has no description, so {agent} won’t know what to build. <button type="button" className="text-button" onClick={() => onOpenTask(item.id)}>Write a description</button></p>}
  <p className="guided-lead">When you press Build, {agent}:</p>
  <ol className="guided-steps">
   <li>Makes a separate copy of your code, so nothing you have is touched.</li>
   <li>Writes the code for this task, following its description.</li>
   <li>Runs your tests: {tests.map(test => <code key={test}>{test}</code>)} <button type="button" className="text-button" onClick={onChangeTests}>change</button></li>
   <li>Checks the work against the task description.</li>
   <li>Shows you the result. You keep it, or ask for changes.</li>
  </ol>
  <p className="guided-muted">If a test fails, the build stops and shows you which one and why.</p>
  {error && <p role="alert" className="guided-error">{error}</p>}
  <div className="guided-actions">
   <Button variant="ghost" onClick={onChooseAnother}>Choose another task</Button>
   <Button disabled={busy} blockedReason={!readiness && !error ? 'Checking the task…' : problem} onClick={() => { void build(); }}>{busy || !readiness ? <Loader2 size={14} className="spin"/> : <Hammer size={14}/>}Build</Button>
  </div>
 </>;
}
