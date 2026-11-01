// What a guided build is doing now, in the person's terms.
export type BuildPhase = 'starting' | 'writing' | 'testing' | 'checking' | 'yourTurn' | 'kept' | 'changesRequested' | 'finished' | 'failed' | 'stopped';
export type BuildFile = { path: string; added: number | null; removed: number | null; new: boolean };
export type BuildTest = { command: string; state: string; exitCode: number | null; output: string };
export type BuildSummary = {
 runId: string; itemId: string; title: string; revision: number; state: string; phase: BuildPhase;
 failedWhile: 'writing' | 'testing' | 'checking' | 'starting' | null; problem: string | null;
 agent: string; model: string | null;
 changes: { files: BuildFile[]; truncated: boolean } | null;
 builderReport: string | null; reviewReport: string | null; tests: BuildTest[];
 changeRequest: string | null; previousRunId: string | null; nextRunId: string | null;
 keptInto: string | null; keptCommit: string | null; taskDone: boolean;
 createdAt: string; finishedAt: string | null;
};
export type BuildListItem = { runId: string; itemId: string; title: string; phase: BuildPhase; createdAt: string };

export const workingPhases: ReadonlySet<BuildPhase> = new Set(['starting', 'writing', 'testing', 'checking']);
export const needsAttention = (phase: BuildPhase) => workingPhases.has(phase) || phase === 'yourTurn' || phase === 'failed';
export const phaseLabel: Record<BuildPhase, string> = {
 starting: 'Starting', writing: 'Writing the code', testing: 'Running your tests', checking: 'Checking the work',
 yourTurn: 'Your turn', kept: 'Kept', changesRequested: 'Changes asked for', finished: 'Finished', failed: 'Stopped with a problem', stopped: 'Stopped',
};
