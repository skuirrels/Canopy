import { ApiError, request } from './api';

export type FactorySkill = { id: string; source: string };
export type FactoryCommand = { executable: string; arguments: string[]; workingDirectory: string; timeoutSeconds: number; environmentReferences?: Record<string, string> | null };
export type FactoryStep = { id: string; name: string; kind: string; instructions?: string | null; role?: string | null; permission?: string | null; provider?: string | null; model?: string | null; effort?: string | null; skills?: FactorySkill[] | null; command?: FactoryCommand | null; consumes?: string[] | null; produces?: string[] | null };
export type FactoryStage = { id: string; name: string; group: string; dependsOn: string[]; steps: FactoryStep[] };
export type FactoryDefinition = { stages: FactoryStage[]; schemaVersion?: number };
export type FactoryTemplate = { id: string; name: string; description: string; source: string; definition: FactoryDefinition };
export type FactoryPipeline = { id: string; projectId: string; name: string; description: string; source: string; definition: FactoryDefinition; draftRevision: number; publishedDraftRevision: number; archived: boolean };
export type PipelineVersion = { id: string; pipelineId: string; projectId: string; number: number; name: string; description: string; source: string; definition: FactoryDefinition; publishedAt: string };
export type PipelineDetail = { projectRevision: number; pipeline: FactoryPipeline; versions: PipelineVersion[] };
export type PipelineList = { projectRevision: number; pipelines: FactoryPipeline[] };
export type StepOverride = Pick<FactoryStep, 'instructions' | 'provider' | 'model' | 'effort' | 'skills' | 'command'> & { stageId: string; stepId: string };
export type FactoryOverrides = { steps: StepOverride[] };
export type TaskFactoryDetail = { projectRevision: number; configuration: { id: string; projectId: string; pipelineVersionId: string; overrides: FactoryOverrides } | null; version: PipelineVersion | null; resolvedDefinition: FactoryDefinition | null; updateAvailable: boolean; archived: boolean; inherited?: boolean };
export type FactoryDiagnostic = { code: string; severity: string; message: string; field: string; stageId?: string | null; stepId?: string | null };
export type FactoryValidation = { valid: boolean; executionReadiness: string; diagnostics: FactoryDiagnostic[] };
export type PipelineDraft = Pick<FactoryPipeline, 'name' | 'description' | 'definition'>;
export type RecoveredDraft = PipelineDraft & { baseRevision: number };

export async function factoryRequest<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
 let response: Response;
 try { response = await fetch('/api/factory' + path, { method, headers: { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body), signal }); }
 catch (error) { if (signal?.aborted) throw error; throw new ApiError('Cannot reach the server. Check your connection and reload the saved configuration before retrying.', 0); }
 if (!response.ok) {
  const problem = await response.json().catch(() => ({}));
  throw new ApiError(problem.title || (response.status === 404 && path === '/templates' ? 'Software Factory is unavailable. Restart the updated backend.' : `Factory request failed (${response.status}).`), response.status);
 }
 return response.json() as Promise<T>;
}
export async function factoryProjectRevision(projectId: string) {
 const workspace = await request(); const project = workspace.projects.find(project => project.id === projectId);
 if (!project) throw new Error('This project no longer exists.');
 return project.revision;
}

export { stageWouldCycle, setStageDependency, removeFactoryStage, moveFactoryStep, identifiers, draftKey, draftFromPipeline, recoveredDraft, patchOverride, resolveFactoryOverrides, downloadFactoryDraft, configurationFingerprint, pipelineDraftState, taskFactorySignature } from './factory-definition';
