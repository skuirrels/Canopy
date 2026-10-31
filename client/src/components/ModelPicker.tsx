import { useEffect, useState } from 'react';
import { catalogueNote, parseSelectValue, selectValue } from '../lib/agent-models';
import type { AgentModelCatalogue, ModelSelection } from '../lib/agent-models';
import { agentRequest } from '../lib/agents';
import type { AgentId } from '../lib/agents';

const catalogues = new Map<AgentId, Promise<AgentModelCatalogue>>();
function loadModels(provider: AgentId): Promise<AgentModelCatalogue> {
 let pending = catalogues.get(provider);
 if (!pending) {
  pending = agentRequest<AgentModelCatalogue>(`/providers/${provider}/models`);
  catalogues.set(provider, pending);
  // A failed or empty listing is retried next time, for example after signing in to the CLI.
  void pending.then(value => { if (!value.models.length) catalogues.delete(provider); }, () => catalogues.delete(provider));
 }
 return pending;
}

export function ModelPicker({ provider, label, presets, selection, onChange, disabled }: { provider: AgentId | ''; label: string; presets: { key: string; label: string }[]; selection: ModelSelection; onChange: (selection: ModelSelection) => void; disabled?: boolean }) {
 const [catalogue, setCatalogue] = useState<AgentModelCatalogue | null>(null);
 const [typing, setTyping] = useState(false);
 useEffect(() => {
  setCatalogue(null); if (!provider) return;
  let current = true;
  loadModels(provider).then(value => { if (current) setCatalogue(value); }, (e: Error) => { if (current) setCatalogue({ provider, source: 'none', models: [], error: e.message }); });
  return () => { current = false; };
 }, [provider]);
 // Until the catalogue arrives, keep a saved model selected instead of flashing the free-text field.
 const models = catalogue?.models ?? ('model' in selection && selection.model ? [{ id: selection.model, name: selection.model }] : []);
 const value = selectValue(selection, models, typing);
 const note = provider ? catalogueNote(catalogue) : '';
 return <div className="model-picker">
  <label>{label}<select aria-label={label} disabled={disabled} value={value} onChange={e => { const next = parseSelectValue(e.target.value); setTyping(next === 'other'); onChange(next === 'other' ? { model: '' } : next); }}>
   {presets.map(preset => <option key={preset.key} value={'preset:' + preset.key}>{preset.label}</option>)}
   {models.length > 0 && <optgroup label={catalogue?.source === 'configuration' ? 'Configured aliases' : 'Available models'}>{models.map(model => <option key={model.id} value={'model:' + model.id}>{model.name === model.id ? model.id : `${model.name} (${model.id})`}</option>)}</optgroup>}
   <option value="other">Other model…</option>
  </select></label>
  {value === 'other' && <label>{label} ID or alias<input aria-label={`${label} ID or alias`} required maxLength={200} disabled={disabled} value={'model' in selection ? selection.model : ''} onChange={e => onChange({ model: e.target.value })} placeholder="Enter a model available to your account" autoFocus={typing}/></label>}
  {note && <small className="model-picker-note">{note}</small>}
 </div>;
}
