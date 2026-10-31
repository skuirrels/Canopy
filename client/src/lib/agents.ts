import type { AgentUsage } from './factory-execution';
export type AgentId = 'codex' | 'claude' | 'gemini';
export const agentNames: Record<AgentId, string> = { codex: 'Codex', claude: 'Claude', gemini: 'Gemini (Antigravity)' };
export type AgentPurpose = 'spec' | 'documentation' | 'implementation';
export type AgentExecutionSettings = { model: string | null; effort: string | null };
export type AgentDraft = { provider: AgentId | ''; purpose: AgentPurpose; instructions: string; previousId: string | null; modelMode: 'inherit' | 'default' | 'custom'; model: string; effort: string | null };
export type AgentConnection = { id: string; projectId: string; repositoryPath: string; baseRef: string; provider: AgentId; model: string | null; settings: Record<AgentId, AgentExecutionSettings>; instructions: string };
export type AgentRun = {
 id: string; requestId: string; projectId: string; itemId: string; previousRunId: string | null; provider: AgentId; purpose: AgentPurpose;
 state: 'queued' | 'running' | 'cancelling' | 'succeeded' | 'failed' | 'cancelled' | 'interrupted'; active: boolean;
 title: string; specJson: string; projectRevision: number; repositoryPath: string; baseRef: string; model: string | null;
 effort: string | null; requestedModel: string | null; requestedEffort: string | null;
 projectInstructions: string; instructions: string; sessionId: string | null; workspacePath: string | null; baseCommit: string | null;
 usage?: AgentUsage; output: string; result: string; diff: string; error: string | null; createdAt: string; startedAt: string | null; finishedAt: string | null;
};
export type AgentProviders = { providers: { id: AgentId; name: string; available: boolean; effortLevels: string[] }[]; pollIntervalMs: number; folderPicker?: boolean };
export async function agentRequest<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
 const response = await fetch('/api/agents' + path, { method, headers: { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body), signal });
 if (!response.ok) { const problem = await response.json().catch(() => ({})); throw new Error(problem.title || `Agent request failed (${response.status}).`); }
 const text = await response.text(); return text ? JSON.parse(text) as T : null as T;
}
export const purposeLabels: Record<AgentPurpose, string> = { spec: 'Draft / refine spec', documentation: 'Write documentation', implementation: 'Run implementation' };
export function downloadMarkdown(markdown: string) {
 const url = URL.createObjectURL(new Blob([markdown], { type: 'text/markdown;charset=utf-8' }));
 const link = document.createElement('a'); link.href = url; link.download = 'canopy-document.md'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}
