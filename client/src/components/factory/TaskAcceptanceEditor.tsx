import { useEffect, useId, useState } from 'react';
import { Plus, Save, Trash2 } from 'lucide-react';
import { factoryRequest } from '../../lib/factory';
import type { AcceptanceCriteriaDetail, AcceptanceCriterion } from '../../lib/factory-quality';
import { useWorkspace } from '../../lib/workspace';
import { BlockedNote, Button } from '../ui/button';
import { blockedBy } from '../../lib/blocked';

export function TaskAcceptanceEditor({ itemId, beforeChange, onDirtyChange }: { itemId: string; beforeChange: () => Promise<void>; onDirtyChange: (dirty: boolean) => void }) {
 const { refresh } = useWorkspace();
 const cacheKey = `canopy:acceptance-draft:${itemId}`;
 const [base, setBase] = useState('');
 const [saved, setSaved] = useState<AcceptanceCriteriaDetail | null>(null), [draft, setDraft] = useState<AcceptanceCriterion[]>([]), [error, setError] = useState(''), [busy, setBusy] = useState(false), [epoch, setEpoch] = useState(0);
 useEffect(() => {
  const controller = new AbortController(); setSaved(null); setError('');
  void factoryRequest<AcceptanceCriteriaDetail>(`/tasks/${itemId}/criteria`, 'GET', undefined, controller.signal).then(value => { if (!controller.signal.aborted) { setSaved(value);
    let recovered: { base: string; criteria: AcceptanceCriterion[] } | null = null;
    try {
     const parsed = JSON.parse(localStorage.getItem(cacheKey) || 'null');
     if (parsed && typeof parsed.base === 'string' && Array.isArray(parsed.criteria) && parsed.criteria.every((criterion: AcceptanceCriterion) => criterion && ['id', 'description', 'expectedResult', 'verificationMethod'].every(field => typeof criterion[field as keyof AcceptanceCriterion] === 'string') && typeof criterion.needsClarification === 'boolean')) recovered = parsed;
    } catch { /* No readable recovery copy. The server remains authoritative. */ }
    const signature = JSON.stringify(value.criteria);
    setDraft(recovered?.criteria ?? value.criteria); setBase(recovered && JSON.stringify(recovered.criteria) !== signature ? recovered.base : signature);
   } }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); });
  return () => controller.abort();
 }, [itemId, epoch, cacheKey]);
 const dirty = !!saved && JSON.stringify(draft) !== JSON.stringify(saved.criteria), reloadNote = useId();
 useEffect(() => { onDirtyChange(dirty || !saved); return () => onDirtyChange(false); }, [dirty, saved, onDirtyChange]);
 useEffect(() => {
  if (!saved) return;
  try { if (dirty) localStorage.setItem(cacheKey, JSON.stringify({ base, criteria: draft })); else localStorage.removeItem(cacheKey); }
  catch { setError('Browser draft recovery is unavailable. Save criteria before leaving this task.'); }
 }, [base, draft, saved, dirty, cacheKey]);
 function change(id: string, field: keyof AcceptanceCriterion, value: string | boolean) { setDraft(current => current.map(criterion => criterion.id === id ? { ...criterion, [field]: value } : criterion)); }
 async function save() {
  if (!saved) return; setBusy(true); setError('');
  try {
   await beforeChange();
   const current = await factoryRequest<AcceptanceCriteriaDetail>(`/tasks/${itemId}/criteria`);
   if (JSON.stringify(current.criteria) === JSON.stringify(draft)) {
    setSaved(current); setDraft(current.criteria); setBase(JSON.stringify(current.criteria)); await refresh(); return;
   }
   if (JSON.stringify(current.criteria) !== base) { setSaved(current); throw new Error('Saved criteria changed. Your edits are retained here; discard them to adopt the saved criteria.'); }
   const value = await factoryRequest<AcceptanceCriteriaDetail>(`/tasks/${itemId}/criteria`, 'PUT', { expectedRevision: current.projectRevision, criteria: draft });
   setSaved(value); setDraft(value.criteria); setBase(JSON.stringify(value.criteria)); await refresh();
  } catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 return <section className="factory-criteria" aria-label="Acceptance criteria"><h3>Acceptance criteria</h3><p className="factory-help">Define observable results and how to verify them. Save explicitly to adopt criteria. Prepared work orders keep their captured copy.</p>{saved && base !== JSON.stringify(saved.criteria) && <p className="factory-error">Saved criteria changed since this draft. Discard edits to reload the adopted criteria.</p>}{error && <p role="alert" className="factory-error">{error}</p>}
 {!saved ? <Button disabled={busy} variant="ghost" onClick={() => setEpoch(value => value + 1)}>Reload criteria</Button> : <>
 {!draft.length && <p className="factory-help">No criteria yet. Execution success alone cannot establish acceptance.</p>}
 <fieldset disabled={busy} className="factory-step-fields">{draft.map((criterion, index) => <fieldset key={criterion.id} className="factory-fieldset"><legend>Criterion {index + 1}</legend><label>Criterion description<input maxLength={200} value={criterion.description} onChange={event => change(criterion.id, 'description', event.target.value)}/></label><label>Observable expected result<textarea rows={2} value={criterion.expectedResult} onChange={event => change(criterion.id, 'expectedResult', event.target.value)}/></label><label>Verification method<textarea rows={2} value={criterion.verificationMethod} onChange={event => change(criterion.id, 'verificationMethod', event.target.value)}/></label><label className="factory-checkbox"><input type="checkbox" checked={criterion.needsClarification} onChange={event => change(criterion.id, 'needsClarification', event.target.checked)}/>Needs clarification (vague or unmeasurable)</label>{(criterion.needsClarification || !criterion.expectedResult.trim() || !criterion.verificationMethod.trim()) && <p className="factory-help">Review needed: clarify the expected result and verification method before QA.</p>}<small className="factory-help">Stable ID: {criterion.id}</small><button type="button" className="text-button" onClick={() => setDraft(current => current.filter(value => value.id !== criterion.id))}><Trash2 size={12}/>Remove criterion {index + 1}</button></fieldset>)}</fieldset>
 <div className="factory-task-actions"><Button variant="secondary" disabled={busy} blockedReason={draft.length >= 100 && 'A task can have up to 100 criteria. Remove one first.'} onClick={() => setDraft(current => [...current, { id: crypto.randomUUID(), description: '', expectedResult: '', verificationMethod: '', needsClarification: true }])}><Plus size={13}/>Add criterion</Button><Button disabled={busy} blockedReason={blockedBy([!dirty, 'No unsaved changes. Edit a criterion to save.'], [draft.some(value => !value.description.trim()), 'Describe every criterion before saving.'])} onClick={() => { void save(); }}><Save size={13}/>{busy ? 'Saving criteria…' : 'Save criteria'}</Button>{dirty && <button type="button" className="text-button" disabled={busy} onClick={() => { setDraft(saved.criteria); setBase(JSON.stringify(saved.criteria)); setError(''); }}>Discard criterion edits</button>}<button type="button" className="text-button" disabled={busy || dirty} aria-describedby={dirty ? reloadNote : undefined} onClick={() => setEpoch(value => value + 1)}>Reload criteria</button></div><BlockedNote id={reloadNote} reason={dirty && 'Save or discard your edits before reloading criteria.'}/>{dirty && <p role="status" className="factory-help">Unsaved criteria. Save before preparing work or leaving this task.</p>}
 </>}
 </section>;
}
