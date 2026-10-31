import type { AgentId } from './agents';

export type AgentModel = { id: string; name: string; lastRan?: string | null };
export type AgentModelCatalogue = { provider: AgentId; source: 'cli' | 'configuration' | 'none'; models: AgentModel[]; error: string | null; defaultLastRan?: string | null };
export type ModelSelection = { preset: string } | { model: string };

// Select values are namespaced so a model ID can never collide with a preset or the free-text option.
export function selectValue(selection: ModelSelection, models: AgentModel[], typing: boolean): string {
 if ('preset' in selection) return 'preset:' + selection.preset;
 return typing || !models.some(model => model.id === selection.model) ? 'other' : 'model:' + selection.model;
}
export function parseSelectValue(value: string): ModelSelection | 'other' {
 if (value === 'other') return 'other';
 return value.startsWith('preset:') ? { preset: value.slice(7) } : { model: value.slice(6) };
}
export function modelLabel(model: AgentModel): string {
 return (model.name === model.id ? model.id : `${model.name} (${model.id})`) + (model.lastRan ? ` · last ran as ${model.lastRan}` : '');
}
export function catalogueNote(catalogue: AgentModelCatalogue | null): string {
 if (!catalogue) return 'Loading models…';
 if (catalogue.error) return catalogue.error + ' Choose Other model to enter an ID.';
 if (catalogue.source === 'configuration') return 'Models from Canopy configuration. A “latest” alias always runs the newest model in that family; choose a full ID to pin a version. “Last ran as” shows the exact model a previous run used.';
 return catalogue.models.length ? '' : 'This agent does not list models. Choose Other model to enter an ID.';
}
