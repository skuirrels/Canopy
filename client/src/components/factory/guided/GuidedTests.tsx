import { useState } from 'react';
import { ArrowRight, Loader2, Plus, Trash2 } from 'lucide-react';
import type { FactorySkill } from '../../../lib/factory';
import { factoryRequest } from '../../../lib/factory';
import { checkCommand, checkProblem, commandDraft, timeoutLabel, workflowRoles } from '../../../lib/factory-setup';
import type { CheckDraft, ProjectSetup } from '../../../lib/factory-setup';
import type { SkillCatalogue } from '../../../lib/factory-skills';
import { skillSelectionStatus } from '../../../lib/factory-skills';
import { blockedBy } from '../../../lib/blocked';
import { Button } from '../../ui/button';

const savedDrafts = (setup: ProjectSetup): CheckDraft[] =>
 setup.version?.definition.stages.flatMap(s => s.steps.filter(x => x.kind === 'command' && x.command).map(x => commandDraft(x.command!))) ?? [];
const same = (a: CheckDraft[], b: CheckDraft[]) =>
 JSON.stringify(a.map(x => [x.line.trim(), x.directory.trim(), x.timeout])) === JSON.stringify(b.map(x => [x.line.trim(), x.directory.trim(), x.timeout]));

// Step 2: the commands that tell finished work from broken work. Saving them sets up the project's workflow.
export function GuidedTests({ projectId, setup, catalogue, onSaved }: { projectId: string; setup: ProjectSetup; catalogue: SkillCatalogue | null; onSaved: () => Promise<void> }) {
 const saved = savedDrafts(setup), detected = setup.suggestedChecks[0];
 const [drafts, setDrafts] = useState<CheckDraft[]>(() => saved.length ? saved : detected?.checks.map(c => ({ ...commandDraft(c), evidence: detected.evidence })) ?? []);
 const [busy, setBusy] = useState(false), [error, setError] = useState('');
 const timeout = String(setup.defaults.defaultCheckTimeoutSeconds);
 const template = setup.templates.find(t => t.id === (setup.setup?.templateId ?? setup.defaults.defaultTemplateId));
 const has = (line: string, directory = '.') => drafts.some(d => d.line.trim() === line && d.directory.trim() === directory);
 const found = setup.suggestedChecks.map(g => ({ label: `Found in ${g.evidence}`, commands: g.checks.map(c => commandDraft(c)).filter(c => !has(c.line, c.directory)), evidence: g.evidence })).filter(g => g.commands.length);
 // With nothing to detect, offer common commands: every stack until one is picked, then only the rest of that stack.
 const starters = setup.suggestedChecks.length ? [] : setup.defaults.starterChecks
  .filter(g => !drafts.length || g.commands.some(c => has(c)))
  .map(g => ({ label: g.ecosystem, commands: g.commands.filter(c => !has(c)) })).filter(g => g.commands.length);
 const problem = drafts.map((d, i) => ({ i, p: checkProblem(d) })).find(x => x.p);
 const changed = !same(drafts, saved);
 const blocked = blockedBy(
  [!template, 'This project’s workflow can’t be found. Open Advanced tools to choose one.'],
  [!drafts.length, 'Add at least one command, such as your test command.'],
  [problem, `Command ${(problem?.i ?? 0) + 1}: ${problem?.p}`]);
 const update = (index: number, change: Partial<CheckDraft>) => setDrafts(drafts.map((d, i) => i === index ? { ...d, ...change } : d));

 async function save() {
  if (!template) return;
  setBusy(true); setError('');
  try {
   const stored = setup.version?.definition;
   const skills: Record<string, FactorySkill[]> = Object.fromEntries(workflowRoles(template.definition).map(role => [role, stored
    ? stored.stages.flatMap(s => s.steps.filter(x => x.role === role).flatMap(x => x.skills ?? [])).filter((ref, i, all) => all.findIndex(x => x.id === ref.id && x.source === ref.source) === i)
    : (setup.defaults.suggestedSkills[role] ?? []).filter(ref => catalogue?.complete && skillSelectionStatus(ref, catalogue).state === 'available')]));
   await factoryRequest(`/projects/${projectId}/setup`, 'PUT', { expectedRevision: setup.projectRevision, templateId: template.id, name: setup.version?.name ?? template.name, commands: drafts.map(checkCommand), skills });
   await onSaved();
  } catch (cause) { setError((cause as Error).message); }
  finally { setBusy(false); }
 }

 return <>
  <h2>How to test it</h2>
  <p className="guided-lead">Canopy needs a way to tell finished work from broken work. After the agent writes code, Canopy runs these commands in its copy of your code. If one fails, the build stops and shows you why.</p>
  {!setup.suggestedChecks.length && !saved.length && <p className="guided-note"><strong>There’s no code to build or test in this folder yet.</strong> If your first tasks will create it, pick the commands it will use. They will fail until that code exists.</p>}
  <ul className="guided-tests">{drafts.map((draft, index) => {
   const issue = checkProblem(draft), settingsIssue = !!issue && !!draft.line.trim() && !/^(Use one|Close|Enter)/.test(issue);
   return <li key={index}>
    <div className="guided-test-line">
     <input aria-label={`Command ${index + 1}`} value={draft.line} placeholder="For example: npm test" onChange={e => update(index, { line: e.target.value })}/>
     <Button variant="ghost" size="sm" aria-label={`Remove command ${index + 1}`} onClick={() => setDrafts(drafts.filter((_, i) => i !== index))}><Trash2 size={14}/></Button>
    </div>
    {draft.evidence && <small className="guided-muted">Found in {draft.evidence}</small>}
    {issue && draft.line.trim() && <small className="blocked-reason" role="note">{issue}</small>}
    <details className="guided-test-options" open={settingsIssue || undefined}>
     <summary>Runs from {draft.directory.trim() === '.' ? 'the top folder' : draft.directory.trim() || 'no folder set'} · stops after {timeoutLabel(draft.timeout)}</summary>
     <div className="guided-test-fields">
      <label>Folder, relative to the top of the code<input value={draft.directory} placeholder=". for the top folder" onChange={e => update(index, { directory: e.target.value })}/></label>
      <label>Stop after (seconds)<input type="number" min={1} step={1} value={draft.timeout} onChange={e => update(index, { timeout: e.target.value })}/></label>
     </div>
    </details>
   </li>;
  })}</ul>
  {[...found, ...starters].map(group => <div className="guided-chips" key={group.label}>
   <span>{group.label}</span>
   {group.commands.map(command => typeof command === 'string'
    ? <button type="button" key={command} onClick={() => setDrafts([...drafts, { line: command, directory: '.', timeout }])}><Plus size={12}/><code>{command}</code></button>
    : <button type="button" key={command.line + command.directory} onClick={() => setDrafts([...drafts, { ...command, evidence: 'evidence' in group ? group.evidence : undefined }])}><Plus size={12}/><code>{command.line}</code></button>)}
  </div>)}
  <Button variant="secondary" size="sm" onClick={() => setDrafts([...drafts, { line: '', directory: '.', timeout }])}><Plus size={14}/>{drafts.length ? 'Add another command' : 'Type a command'}</Button>
  {error && <p role="alert" className="guided-error">{error}</p>}
  <div className="guided-actions">
   {changed || !saved.length
    ? <Button disabled={busy} blockedReason={blocked} onClick={() => { void save(); }}>{busy ? <Loader2 size={14} className="spin"/> : null}Save and continue<ArrowRight size={14}/></Button>
    : <Button onClick={() => { void onSaved(); }}>Next: choose a task<ArrowRight size={14}/></Button>}
  </div>
 </>;
}
