import { factoryRequest } from './factory';

export * from './factory-build-phases';
import type { BuildListItem, BuildSummary } from './factory-build-phases';

export const startBuild = (taskId: string, projectRevision: number) =>
 factoryRequest<BuildSummary>(`/tasks/${taskId}/build`, 'POST', { requestId: crypto.randomUUID(), expectedRevision: projectRevision });
export const readBuild = (runId: string, signal?: AbortSignal) => factoryRequest<BuildSummary>(`/builds/${runId}`, 'GET', undefined, signal);
export const projectBuilds = (projectId: string, signal?: AbortSignal) => factoryRequest<BuildListItem[]>(`/projects/${projectId}/builds`, 'GET', undefined, signal);
export const keepBuild = (build: BuildSummary) =>
 factoryRequest<BuildSummary>(`/builds/${build.runId}/keep`, 'POST', { requestId: crypto.randomUUID(), expectedRevision: build.revision });
export const askForChanges = (build: BuildSummary, note: string) =>
 factoryRequest<BuildSummary>(`/builds/${build.runId}/changes`, 'POST', { requestId: crypto.randomUUID(), expectedRevision: build.revision, note });
export const buildAgain = (build: BuildSummary) =>
 factoryRequest<BuildSummary>(`/builds/${build.runId}/again`, 'POST', { requestId: crypto.randomUUID(), expectedRevision: build.revision });
