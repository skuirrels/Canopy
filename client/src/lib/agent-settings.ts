import type { AgentConnection, AgentDraft, AgentExecutionSettings, AgentId, AgentRun } from './agents';

export function inheritedSettings(connection: AgentConnection, provider: AgentId | '', previous?: AgentRun): AgentExecutionSettings {
 if (previous) return { model: previous.model, effort: previous.effort };
 if (!provider) return { model: null, effort: null };
 // Older servers cannot supply the new contract; do not reuse another agent's model.
 return connection.settings?.[provider] ?? { model: provider === connection.provider ? connection.model : null, effort: null };
}
export function executionOverrides(draft: AgentDraft): { model: string | null; effort: string | null } {
 if (draft.modelMode === 'custom' && !draft.model.trim()) throw new Error('Enter a model ID or alias, or choose a default.');
 return { model: draft.modelMode === 'inherit' ? null : draft.modelMode === 'default' ? '' : draft.model.trim(), effort: draft.effort };
}
export function changeAgent(draft: AgentDraft, provider: AgentId | ''): AgentDraft {
 return { ...draft, provider, modelMode: 'inherit', model: '', effort: null };
}
