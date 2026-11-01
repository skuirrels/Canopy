import { useId, useState } from 'react';
import { factoryRequest } from '../../lib/factory';
import type { AnalyticsCatalogue, ErrorFeedCatalogue, IntakeConfiguration, IntakeSource } from '../../lib/factory-intake';
import { BlockedNote, Button } from '../ui/button';
import { blockedBy } from '../../lib/blocked';
import { AnalyticsSourceFields } from './AnalyticsFeedEvidence';
import { Dialog } from '../ui/dialog';
const categories = ['defect', 'missingBehavior', 'verificationGap', 'scopeDrift', 'improvement'];
export function IntakeSourceEditor({ source, revision, projectId, catalogue, analyticsCatalogue, onSaved, onClose }: { source: IntakeSource | null; revision: number; projectId: string; catalogue: ErrorFeedCatalogue; analyticsCatalogue: AnalyticsCatalogue; onSaved: () => Promise<void>; onClose: () => void }) {
 const [config, setConfig] = useState<IntakeConfiguration>(() => source?.configuration ?? { name: '', kind: 'githubIssues', enabled: false, repository: '', labels: [], minimumOccurrences: 0, categories: [] });
 const [labels, setLabels] = useState(config.labels.join(', ')), [busy, setBusy] = useState(false), [error, setError] = useState('');
 const feed = catalogue?.feeds.find(value => value.id === config.errorFeedId);
 const analytics = config.kind === 'analytics', errors = config.kind === 'applicationErrors', analyticsFeed = !!analyticsCatalogue.feeds.find(x => x.id === config.analyticsFeedId), lockedNote = useId();
 const saveBlocked = blockedBy(
  [analytics && !config.analyticsFeedId, 'Choose an analytics feed.'],
  [analytics && !config.analyticsRuleIds?.length, 'Choose at least one metric rule.'],
  [analytics && !analyticsFeed && !source, 'Choose an analytics feed that is still configured.'],
  [analytics && !analyticsFeed && config.enabled, 'This feed is no longer configured. Turn off manual collection to save.'],
  [errors && !config.errorFeedId, 'Choose a configured error feed.'],
  [errors && !feed && !source, 'Choose an error feed that is still configured.'],
  [errors && !feed && config.enabled, 'This feed is no longer configured. Turn off manual collection to save.'],
  [errors && !config.levels?.length, 'Choose at least one error level.'],
  [errors && !(config.minimumOccurrences >= 1), 'Enter a minimum of at least one error event.']);
 async function save(event: React.FormEvent) {
  event.preventDefault(); setBusy(true); setError('');
  try { await factoryRequest(source ? `/intake/sources/${source.id}` : `/projects/${projectId}/intake/sources`, source ? 'PUT' : 'POST', { expectedRevision: revision, ...(source ? { expectedSourceRevision: source.revision } : {}), configuration: { ...config, labels: config.kind === 'githubIssues' ? labels.split(',').map(x => x.trim()).filter(Boolean) : [] } }); await onSaved(); onClose(); }
  catch (cause) { setError((cause as Error).message); } finally { setBusy(false); }
 }
 function kindChanged(kind: IntakeConfiguration['kind']) {
  setConfig({ ...config, kind, repository: '', labels: [], categories: [], minimumOccurrences: kind === 'reviewFindings' ? 2 : 0, errorFeedId: kind === 'applicationErrors' ? '' : null, levels: kind === 'applicationErrors' ? [] : null, analyticsFeedId: kind === 'analytics' ? '' : null, analyticsRuleIds: kind === 'analytics' ? [] : null }); setLabels('');
 }
 return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }} title={source ? 'Edit intake source' : 'Configure intake source'} description="Collection reads evidence into a preview. Filing and execution require separate actions." className="factory-dialog"><form onSubmit={event => { void save(event); }}><fieldset disabled={busy} className="factory-step-fields">
  <label>Source name<input required autoFocus maxLength={200} value={config.name} onChange={e => setConfig({ ...config, name: e.target.value })}/></label>
  <label>Source type<select value={config.kind} disabled={!!source} aria-describedby={source ? lockedNote : undefined} onChange={e => kindChanged(e.target.value as IntakeConfiguration['kind'])}><option value="githubIssues">GitHub issues</option><option value="reviewFindings">Recurring review findings</option><option value="applicationErrors">Application errors · structured log feed</option><option value="analytics">Analytics · metric snapshot</option></select></label>
  <BlockedNote id={lockedNote} reason={source && 'Saved sources keep their type and target. Add a new source to change them.'}/>
  {config.kind === 'githubIssues' ? <><label>Repository<input required placeholder="owner/repository" readOnly={!!source} aria-describedby={source ? lockedNote : undefined} value={config.repository} onChange={e => setConfig({ ...config, repository: e.target.value })}/></label><label>Required labels, separated by commas<input value={labels} onChange={e => setLabels(e.target.value)}/></label><p className="factory-help">All specified labels must match. An empty list reads all issues. Pull requests are excluded. GitHub access uses the configured server credentials. Saving explicitly adopts the current server provider configuration.</p></> : config.kind === 'reviewFindings' ? <><label>Minimum distinct tasks<input type="number" required min={2} value={config.minimumOccurrences} onChange={e => setConfig({ ...config, minimumOccurrences: Number(e.target.value) })}/></label><fieldset><legend>Finding categories</legend>{categories.map(category => <label className="intake-check" key={category}><input type="checkbox" checked={config.categories.includes(category)} onChange={e => setConfig({ ...config, categories: e.target.checked ? [...config.categories, category] : config.categories.filter(x => x !== category) })}/>{category}</label>)}</fieldset><p className="factory-help">Groups submitted unresolved findings by title, category and code paths across distinct tasks. Rechecking one finding does not create another occurrence.</p></> : config.kind === 'analytics' ? <AnalyticsSourceFields config={config} catalogue={analyticsCatalogue} existing={!!source} lockedNoteId={source ? lockedNote : undefined} onChange={setConfig}/> : <>
   {!catalogue?.supported && <p role="alert" className="factory-error">This backend cannot safely read structured snapshot files on its current platform.</p>}
   {!catalogue?.feeds.length && <p role="status" className="intake-notice">No structured log feeds configured on this backend. Configure a snapshot source before adding it here.</p>}
   <label>Configured error feed<select required disabled={!!source} aria-describedby={source ? lockedNote : undefined} value={config.errorFeedId ?? ''} onChange={e => setConfig({ ...config, errorFeedId: e.target.value, levels: [] })}><option value="">Choose application and environment</option>{source && !feed && <option value={config.errorFeedId ?? ''}>{config.errorFeedId} · no longer configured</option>}{catalogue?.feeds.map(value => <option key={value.id} value={value.id}>{value.name} · {value.identity.application} · {value.identity.environment}</option>)}</select></label>
   {feed && <p className="factory-help">Source identity: {feed.identity.sourceId}. Maximum snapshot age: {feed.maxAgeSeconds} seconds. Maximum coverage window: {feed.maxWindowSeconds} seconds.</p>}
   <label>Minimum distinct error events<input type="number" required min={1} max={2147483647} value={config.minimumOccurrences || ''} onChange={e => setConfig({ ...config, minimumOccurrences: Number(e.target.value) })}/></label>
   <fieldset><legend>Included error levels</legend>{([...new Set([...(feed?.levels ?? []), ...(config.levels ?? [])])]).map(level => <label className="intake-check" key={level}><input type="checkbox" checked={config.levels?.includes(level) ?? false} onChange={e => setConfig({ ...config, levels: e.target.checked ? [...(config.levels ?? []), level] : config.levels?.filter(x => x !== level) })}/>{level}{feed && !feed.levels.includes(level) ? " · no longer configured" : ""}</label>)}</fieldset>
   <p className="factory-help">The producer must supply stable error/event IDs and complete, sanitized coverage. Repeated event IDs count once. Missing, stale or partial snapshots block triage. Saving adopts the current feed configuration; collect again afterwards.</p>
  </>}
  <label className="intake-check"><input type="checkbox" checked={config.enabled} onChange={e => setConfig({ ...config, enabled: e.target.checked })}/>Enable manual collection and triage</label>
 </fieldset>{error && <p role="alert" className="factory-error">{error}</p>}<div className="dialog-footer"><Button type="button" variant="ghost" disabled={busy} onClick={onClose}>Cancel</Button><Button disabled={busy} blockedReason={saveBlocked}>{busy ? 'Saving…' : 'Save source'}</Button></div></form></Dialog>;
}
