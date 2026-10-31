import { useState } from 'react';
import { FileUp, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { parsePlan } from '../../../shared/plan-import.mjs';
import type { PlanDraft } from '../../../shared/plan-import.mjs';
import { parseMarkdown, MAX_SPEC_BYTES } from '../lib/markdown';
import { priorities } from '../lib/types';
import type { Project } from '../lib/types';
import { useWorkspace } from '../lib/workspace';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';
import './plan-import.css';

export function PlanImport({ project, onClose, onImported }: { project: Project; onClose: () => void; onImported: () => void }) {
 const { data, mutate, refresh } = useWorkspace();
 const [expanded, setExpanded] = useState<Set<string>>(new Set());
 const [source, setSource] = useState(''); const [markdown, setMarkdown] = useState('');
 const [draft, setDraft] = useState<PlanDraft[] | null>(null); const [warnings, setWarnings] = useState<string[]>([]);
 const [sourceHash, setSourceHash] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
 const projectItems = data.items.filter(i => i.projectId === project.id);
 const receipts = (data.planImports ?? []).filter(i => i.projectId === project.id && i.source === source.trim());
 const receiptFor = (key: string) => receipts.find(r => r.key === key);
 const isExisting = (item: PlanDraft) => !!receiptFor(item.key) || !!item.existingItemId;
 const newItems = draft?.filter(i => !isExisting(i)) ?? [];
 const changedSource = receipts.some(r => r.sourceHash !== sourceHash);
 function edit(key: string, changes: Partial<PlanDraft>) { setDraft(items => items!.map(item => item.key === key ? { ...item, ...changes } : item)); }
 async function preview() {
  setError(''); setBusy(true);
  try {
   if (!source.trim()) throw new Error('Give this plan a source name so later imports can recognise it.');
   if (new TextEncoder().encode(markdown).length > MAX_SPEC_BYTES) throw new Error('Choose a Markdown plan smaller than 2 MB.');
   parseMarkdown(markdown); // Validate the entire source, including content outside extracted tasks.
   const result = parsePlan(markdown, source.trim());
   const hash = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(markdown));
   setSourceHash(Array.from(new Uint8Array(hash), b => b.toString(16).padStart(2, '0')).join(''));
   setWarnings(result.warnings); setSource(source.trim());
   setDraft(result.items.map(item => {
    const candidates = projectItems.filter(existing => existing.title === item.title || (!item.key.includes(':') && existing.title.startsWith(item.key + ': ')));
    return candidates.length === 1 ? { ...item, existingItemId: candidates[0].id } : item;
   }));
  } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
 }
 async function apply() {
  if (!draft) return; setBusy(true); setError('');
  try {
   const items = draft.map((item, index) => ({ key: item.key, title: item.title, parentKey: item.parentKey,
    specJson: JSON.stringify(parseMarkdown(item.markdown + (item.sourcePriority ? `\n\nSource priority: ${item.sourcePriority}` : '') + (item.phase ? `\n\nDelivery phase: ${item.phase}` : '') + `\n\n## Acceptance criteria\n${item.acceptance || 'Not specified; clarification required.'}` + (index === 0 ? `\n\n## Imported source plan\n${markdown}` : ''))),
    priority: item.priority, dependencyKeys: item.dependencyKeys,
    criteria: item.parentKey === null ? [] : [{ id: crypto.randomUUID(), description: `Acceptance for ${item.key}`.slice(0, 200), expectedResult: item.acceptance, verificationMethod: item.verificationMethod ?? '', needsClarification: !item.acceptance.trim() || !item.verificationMethod?.trim() }],
    existingItemId: receiptFor(item.key)?.itemId ?? item.existingItemId ?? null }));
   const before = new Set(projectItems.map(i => i.id));
   const next = await mutate(`/api/projects/${project.id}/plan-imports`, 'POST', { source, sourceHash, items }, project.id);
   const created = next.items.filter(i => i.projectId === project.id && !before.has(i.id));
   toast.success(`Imported ${created.filter(i => i.parentId === null).length} components and ${created.filter(i => i.parentId !== null).length} tasks`);
   onImported();
  } catch (e) {
   setError(`${(e as Error).message} The workspace has been refreshed; review existing items before retrying.`);
   await refresh();
  } finally { setBusy(false); }
 }
 return <Dialog open wide title={`Import plan into ${project.name}`} description="Turn structured Markdown into components and tasks, then review the draft before creating it." className="plan-import-dialog" onOpenChange={open => { if (!open && !busy) onClose(); }}>
  {!draft ? <>
   <label>Markdown plan<input type="file" accept=".md,.markdown,text/markdown" disabled={busy} onChange={event => {
    const file = event.target.files?.[0]; if (!file) return;
    if (file.size > MAX_SPEC_BYTES) { setError('Choose a Markdown plan smaller than 2 MB.'); return; }
    setBusy(true); setError('');
    void file.arrayBuffer().then(buffer => { const content = new TextDecoder('utf-8', { fatal: true }).decode(buffer); setMarkdown(content); setSource(file.name); }).catch(() => setError('Choose a valid UTF-8 Markdown file.')).finally(() => setBusy(false));
   }}/></label>
   <label>Plan source name<input value={source} maxLength={200} disabled={busy} placeholder="A stable name for this plan" onChange={e => setSource(e.target.value)}/></label>
   <label>Plan contents<textarea rows={9} value={markdown} disabled={busy} onChange={e => setMarkdown(e.target.value)} placeholder="Paste Markdown with requirement tables, numbered steps or task checklists…"/></label>
   <p className="plan-import-note">Tables can include ID, Requirement, Acceptance, Priority, Phase and Dependencies columns. Numbered steps and checklists become tasks. Prose is preserved with the source plan. No dates or assignees are inferred.</p>
  </> : <>
   <div className="plan-import-summary"><strong>{newItems.filter(i => i.parentKey === null).length} new components · {newItems.filter(i => i.parentKey !== null).length} new tasks</strong><span>{draft.length - newItems.length} existing items kept · {newItems.reduce((n, i) => n + i.dependencyKeys.length, 0)} new dependency links</span></div>
   <details className="plan-import-notes"><summary>Extraction notes and coverage</summary>{warnings.map(note => <p key={note}>{note}</p>)}<p>Previously imported tasks keep their progress, specifications, criteria and dependencies. Source priority labels are preserved; choose an app priority in the preview. Verification methods remain open for clarification.</p>{changedSource && <p role="status">This source changed since its previous import. Existing items are kept; review their specifications separately.</p>}</details>
   <div className="plan-import-preview" aria-label="Plan backlog preview">
    {draft.map(item => <details key={item.key} onToggle={event => { const open = event.currentTarget.open; setExpanded(previous => { const next = new Set(previous); if (open) next.add(item.key); else next.delete(item.key); return next; }); }} className={item.parentKey === null ? 'plan-component' : 'plan-task'}>
     <summary><span>{item.title}</span><small>{isExisting(item) ? 'Keep existing' : item.parentKey === null ? 'New component' : 'New task'}{item.sourcePriority ? ` · ${item.sourcePriority}` : ''}</small></summary>
     {expanded.has(item.key) && <div className="plan-draft-fields"><p className="plan-import-note">{item.key} · source line {item.sourceLine || 'section'}</p>
      {!receiptFor(item.key) && <label>Existing item<select aria-label={`Existing item for ${item.key}`} value={item.existingItemId ?? ''} onChange={e => edit(item.key, { existingItemId: e.target.value || null })}><option value="">Create new</option>{projectItems.map(existing => <option key={existing.id} value={existing.id}>{existing.title}</option>)}</select></label>}
      {isExisting(item) ? <p className="plan-import-note">This item will be kept as it is. New tasks can depend on it.</p> : <>
       <label>Title<input aria-label={`Title for ${item.key}`} maxLength={200} value={item.title} onChange={e => edit(item.key, { title: e.target.value })}/></label>
       <div className="form-row"><label>Parent<select aria-label={`Parent for ${item.key}`} value={item.parentKey ?? ''} onChange={e => edit(item.key, { parentKey: e.target.value || null })}><option value="">Project root (component)</option>{draft.filter(i => i.key !== item.key).map(parent => <option key={parent.key} value={parent.key}>{parent.title}</option>)}</select></label><label>Priority<select aria-label={`Priority for ${item.key}`} value={item.priority} onChange={e => edit(item.key, { priority: e.target.value })}>{priorities.map(p => <option key={p} value={p}>{p}</option>)}</select></label></div>
       <label>Specification<textarea aria-label={`Specification for ${item.key}`} rows={4} value={item.markdown} onChange={e => edit(item.key, { markdown: e.target.value })}/></label>
       <label>Acceptance criteria<textarea aria-label={`Acceptance for ${item.key}`} rows={3} value={item.acceptance} onChange={e => edit(item.key, { acceptance: e.target.value })}/></label>
       <label>Verification method<input aria-label={`Verification for ${item.key}`} placeholder="How will you demonstrate acceptance?" value={item.verificationMethod ?? ''} onChange={e => edit(item.key, { verificationMethod: e.target.value })}/></label>
       <label>Dependencies<select aria-label={`Dependencies for ${item.key}`} multiple value={item.dependencyKeys} onChange={e => edit(item.key, { dependencyKeys: Array.from(e.target.selectedOptions, o => o.value) })}>{draft.filter(i => i.key !== item.key).map(target => <option key={target.key} value={target.key}>{target.title}</option>)}</select></label>
      </>}
     </div>}
    </details>)}
   </div>
  </>}
  {error && <p role="alert" className="agent-error">{error}</p>}
  <div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={onClose}>Cancel</Button>{draft && <Button variant="secondary" disabled={busy} onClick={() => { setDraft(null); setError(''); }}>Back to plan</Button>}<Button disabled={busy || (!draft && (!markdown.trim() || !source.trim()))} onClick={() => { void (draft ? apply() : preview()); }}>{busy ? <Loader2 size={15} className="spin"/> : <FileUp size={15}/>} {draft ? 'Import reviewed plan' : 'Preview backlog'}</Button></div>
 </Dialog>;
}
