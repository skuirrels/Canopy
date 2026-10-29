import type { FactoryDefinition, FactoryStage, PipelineDraft, FactoryPipeline, RecoveredDraft, FactoryOverrides, StepOverride, TaskFactoryDetail } from './factory';

export function stageWouldCycle(definition: FactoryDefinition, prerequisite: string, dependent: string): boolean {
 if (prerequisite === dependent || !definition.stages.some(s => s.id === prerequisite) || !definition.stages.some(s => s.id === dependent)) return true;
 const seen = new Set<string>(); const remaining = [prerequisite];
 while (remaining.length) {
  const id = remaining.pop()!;
  if (id === dependent) return true;
  if (seen.has(id)) continue;
  seen.add(id); remaining.push(...(definition.stages.find(s => s.id === id)?.dependsOn ?? []));
 }
 return false;
}
export function setStageDependency(definition: FactoryDefinition, prerequisite: string, dependent: string, connected: boolean): FactoryDefinition {
 if (connected && stageWouldCycle(definition, prerequisite, dependent)) throw new Error('That dependency would create a cycle.');
 return { ...definition, stages: definition.stages.map(stage => stage.id !== dependent ? stage : { ...stage, dependsOn: connected ? [...new Set([...stage.dependsOn, prerequisite])] : stage.dependsOn.filter(id => id !== prerequisite) }) };
}
export function removeFactoryStage(definition: FactoryDefinition, id: string): FactoryDefinition {
 // Outputs are deliberately not rewired: server validation exposes missing artifacts.
 return { ...definition, stages: definition.stages.filter(stage => stage.id !== id).map(stage => ({ ...stage, dependsOn: stage.dependsOn.filter(dep => dep !== id) })) };
}
export function moveFactoryStep(stage: FactoryStage, id: string, delta: number): FactoryStage {
 const steps = [...stage.steps], index = steps.findIndex(step => step.id === id), next = index + delta;
 if (index < 0 || next < 0 || next >= steps.length) return stage;
 [steps[index], steps[next]] = [steps[next], steps[index]];
 return { ...stage, steps };
}
export function identifiers(value: string): string[] { return value.trim() ? value.split(',').map(v => v.trim()) : []; }
export function draftKey(projectId: string, pipelineId: string) { return `canopy:factory-draft:${projectId}:${pipelineId}`; }
export function draftFromPipeline(pipeline: FactoryPipeline): PipelineDraft { return { name: pipeline.name, description: pipeline.description, definition: pipeline.definition }; }
export function recoveredDraft(value: string | null): RecoveredDraft | null {
 try {
  const draft = JSON.parse(value || 'null');
  if (!draft || typeof draft.name !== 'string' || typeof draft.description !== 'string' || !Number.isSafeInteger(draft.baseRevision) || !Array.isArray(draft.definition?.stages)) return null;
  if (!draft.definition.stages.every((stage: FactoryStage) => stage && typeof stage.id === 'string' && typeof stage.name === 'string' && typeof stage.group === 'string' && Array.isArray(stage.dependsOn) && stage.dependsOn.every(id => typeof id === 'string') && Array.isArray(stage.steps) && stage.steps.every(step => step && typeof step.id === 'string' && typeof step.name === 'string' && typeof step.kind === 'string'))) return null;
  return draft;
 } catch { return null; }
}
export function patchOverride(overrides: FactoryOverrides, stageId: string, stepId: string, change: Partial<StepOverride>): FactoryOverrides {
 const existing = overrides.steps.find(step => step.stageId === stageId && step.stepId === stepId);
 const next = { ...existing, stageId, stepId, ...change };
 const steps = overrides.steps.filter(step => step.stageId !== stageId || step.stepId !== stepId);
 if (Object.entries(next).some(([key, value]) => key !== 'stageId' && key !== 'stepId' && value !== undefined && value !== null)) steps.push(next);
 return { steps };
}
export function resolveFactoryOverrides(definition: FactoryDefinition, overrides: FactoryOverrides): FactoryDefinition {
 return { ...definition, stages: definition.stages.map(stage => ({ ...stage, steps: stage.steps.map(step => {
  const override = overrides.steps.find(value => value.stageId === stage.id && value.stepId === step.id);
  if (!override) return step;
  return { ...step, ...Object.fromEntries(Object.entries(override).filter(([key, value]) => key !== 'stageId' && key !== 'stepId' && value !== null && value !== undefined)) };
 }) })) };
}
export function downloadFactoryDraft(draft: PipelineDraft) {
 const url = URL.createObjectURL(new Blob([JSON.stringify(draft, null, 2)], { type: 'application/json' }));
 const link = document.createElement('a'); link.href = url; link.download = 'pipeline-draft.json'; link.click(); URL.revokeObjectURL(url);
}

// Optional omitted fields and their server-serialized nulls have identical
// inheritance semantics. Object property order is not a configuration change.
export function configurationFingerprint(value: unknown): string {
 const normalize = (input: unknown): unknown => Array.isArray(input) ? input.map(normalize) : input && typeof input === 'object' ? Object.fromEntries(Object.entries(input).filter(([, value]) => value !== null && value !== undefined).sort(([a], [b]) => a.localeCompare(b)).map(([key, value]) => [key, normalize(value)])) : input;
 return JSON.stringify(normalize(value)) ?? '';
}
export function pipelineDraftState(pipeline: FactoryPipeline, recovery: RecoveredDraft | null): { draft: PipelineDraft; baseRevision: number } {
 const saved = draftFromPipeline(pipeline);
 const recovered = recovery ? { name: recovery.name, description: recovery.description, definition: recovery.definition } : null;
 return recovered && configurationFingerprint(recovered) !== configurationFingerprint(saved)
  ? { draft: recovered, baseRevision: recovery!.baseRevision }
  : { draft: saved, baseRevision: pipeline.draftRevision };
}
export function taskFactorySignature(value: TaskFactoryDetail): string { return configurationFingerprint({ versionId: value.configuration?.pipelineVersionId ?? '', overrides: value.configuration?.overrides ?? { steps: [] } }); }
