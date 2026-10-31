import type { ExecutionSnapshot, ExecutionBudget } from './factory-execution';
import type { RepairLineage } from './factory-repair';
import type { AcceptanceCriterion } from './factory-quality';
import type { FactoryDefinition, FactoryOverrides } from './factory';

export type FactoryRunState = 'prepared' | 'queued' | 'running' | 'waiting' | 'cancelling' | 'succeeded' | 'failed' | 'cancelled' | 'interrupted';
export type FactoryReadinessCheck = { code: string; severity: string; message: string; stageId?: string; stepId?: string };
export type FactoryEnvironment = { repositoryPath: string; baseRef: string; baseCommit: string; defaultProvider: string; projectInstructions: string; definition: FactoryDefinition; skills: { id: string; source: string; path: string; hash: string }[] };
export type FactoryReadiness = { projectRevision: number; canPrepare: boolean; executionReadiness: string; checkedAt: string; checks: FactoryReadinessCheck[]; environment: FactoryEnvironment | null };
export type FactoryRunSnapshot = { pipelineVersionId: string; versionNumber: number; pipelineName: string; source: string; title: string; specJson: string; projectRevision: number; overrides: FactoryOverrides; environment: FactoryEnvironment; checks: FactoryReadinessCheck[]; checkedAt: string; criteria?: AcceptanceCriterion[] | null; repair?: RepairLineage | null; execution?: ExecutionSnapshot | null };
export type FactoryRun = { id: string; itemId: string; projectId: string; state: FactoryRunState; revision: number; active: boolean; createdAt: string; startedAt: string | null; finishedAt: string | null; workspacePath: string | null; error: string | null; snapshot: FactoryRunSnapshot };
export type FactoryRunSummary = { id: string; itemId: string; state: FactoryRunState; createdAt: string; pipelineName: string; versionNumber: number; title: string; revision: number };
export type FactoryStageRun = { id: string; stageId: string; state: string; startedAt: string | null; finishedAt: string | null };
export type FactoryStepRun = { id: string; stageRunId: string; stepId: string; state: string; startedAt: string | null; finishedAt: string | null; agentRunId: string | null; decisionRequestId: string | null; decision: boolean | null; decisionNote: string | null; output: string; outputLimitReached: boolean; evidence: string; error: string | null; exitCode: number | null };
export type FactoryArtifact = { id: string; runId: string; stepRunId: string; name: string; kind: string; evidenceHash: string };
export type FactoryRunDetail = { run: FactoryRun; stages: FactoryStageRun[]; steps: FactoryStepRun[]; artifacts: FactoryArtifact[]; budget?: ExecutionBudget };

export function requireFactoryRunDetail(value: FactoryRunDetail): FactoryRunDetail {
 if (!value?.run?.id || !Number.isSafeInteger(value.run.revision) || typeof value.run.active !== 'boolean' || !Array.isArray(value.stages) || !Array.isArray(value.steps) || !Array.isArray(value.artifacts) || !Array.isArray(value.run.snapshot?.environment?.definition?.stages))
  throw new Error('This backend cannot supply factory runtime data. Restart the updated Canopy backend and refresh the run.');
 return value;
}

export function mergeFactoryRunHistory(received: FactoryRunSummary[], known: FactoryRunSummary[]) {
 const previous = new Map(known.map(run => [run.id, run]));
 return received.map(run => { const saved = previous.get(run.id); return saved && saved.revision > run.revision ? saved : run; });
}
