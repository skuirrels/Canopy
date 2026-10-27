import type { AgentRun } from './agents';

export const BUILD_EXPERIENCE_KEY = 'sprout:build-experience';
export const BUILD_SOUND_KEY = 'sprout:build-sound';

export function readBuildPreference(storage: Pick<Storage, 'getItem'>, key = BUILD_EXPERIENCE_KEY): boolean {
 try { return storage.getItem(key) === 'on'; } catch { return false; }
}

export function writeBuildPreference(storage: Pick<Storage, 'setItem'>, enabled: boolean, key = BUILD_EXPERIENCE_KEY) {
 try { storage.setItem(key, enabled ? 'on' : 'off'); } catch { /* Keep the current choice when browser storage is unavailable. */ }
}

export function buildActivityKey(output: string): string {
 // The server retains a bounded tail, so length alone cannot identify new output.
 let hash = 2166136261;
 for (const character of output.slice(-4000)) hash = Math.imul(hash ^ character.charCodeAt(0), 16777619);
 return `${output.length}:${hash >>> 0}`;
}

export function describeBuild(run: AgentRun, disconnected = false) {
 const latestActivity = run.output.trim().split('\n').reverse().find(line => line.trim())?.trim() ?? '';
 const common = {
  activityKey: buildActivityKey(run.output), latestActivity,
  running: run.state === 'running' && !disconnected,
  succeeded: run.state === 'succeeded',
  attention: run.state === 'failed' || run.state === 'interrupted',
  evidence: [true, !!run.sessionId, !!run.workspacePath, !!run.result] as readonly boolean[],
 };
 if (disconnected && run.active) return { ...common, label: 'Waiting for connection', detail: 'Live updates are unavailable. The last recorded run state is shown.', running: false };
 switch (run.state) {
  case 'queued': return { ...common, label: 'Queued', detail: 'Waiting for the local worker to start this run.' };
  case 'running': return { ...common, label: run.sessionId ? 'Agent working' : 'Starting agent', detail: latestActivity || 'Waiting for agent output.' };
  case 'cancelling': return { ...common, label: 'Stopping build', detail: 'Waiting for the agent process to stop.' };
  case 'succeeded': return { ...common, label: 'Build finished', detail: 'Review the agent’s result, changes and verification evidence.' };
  case 'failed': return { ...common, label: 'Build failed', detail: run.error || 'The agent could not complete this run. Review its activity output.' };
  case 'cancelled': return { ...common, label: 'Build cancelled', detail: 'The run stopped. Review any retained changes.' };
  case 'interrupted': return { ...common, label: 'Build interrupted', detail: run.error || 'The worker stopped before this run finished.' };
 }
}

export type BuildPresentation = ReturnType<typeof describeBuild>;
