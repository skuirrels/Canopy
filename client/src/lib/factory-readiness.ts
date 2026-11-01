import type { Item, Workspace } from './types';
import type { FactoryDefinition } from './factory';
import type { FactoryRunSummary } from './factory-runs';
import { specificationText } from './epics.ts';

export type TaskReadinessState = 'ready' | 'waiting' | 'needsSpecification' | 'building' | 'done';
export type TaskReadiness = { state: TaskReadinessState; label: string; waitingOn: Item[] };
type Graph = Pick<Workspace, 'items' | 'statuses' | 'dependencies'>;
const building = new Set(['prepared', 'queued', 'running', 'waiting', 'cancelling']);

export function openPrerequisites(item: Item, graph: Graph): Item[] {
 const done = new Set(graph.statuses.filter(status => status.isCompleted).map(status => status.id));
 const ids = new Set(graph.dependencies.filter(dependency => dependency.itemId === item.id).map(dependency => dependency.dependsOnId));
 return graph.items.filter(other => ids.has(other.id) && !done.has(other.statusId));
}
export function needsSpecification(definition: FactoryDefinition | null | undefined) {
 return !!definition?.stages.some(stage => stage.steps.some(step => step.kind === 'agent' && step.role === 'implementation'));
}
// Planning-level readiness from data already on hand. The backend's readiness check stays authoritative when preparing.
export function taskReadiness(item: Item, graph: Graph, runs: FactoryRunSummary[], specificationRequired: boolean): TaskReadiness {
 if (graph.statuses.find(status => status.id === item.statusId)?.isCompleted) return { state: 'done', label: 'Done', waitingOn: [] };
 const latest = runs.filter(run => run.itemId === item.id).sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];
 if (latest && building.has(latest.state)) return { state: 'building', label: latest.state === 'prepared' ? 'Prepared · ready to start' : 'Building', waitingOn: [] };
 const waitingOn = openPrerequisites(item, graph);
 if (waitingOn.length) return { state: 'waiting', label: `Waiting on ${waitingOn.length === 1 ? '1 task' : `${waitingOn.length} tasks`}`, waitingOn };
 if (specificationRequired && !specificationText(item.specJson)) return { state: 'needsSpecification', label: 'Needs a specification', waitingOn: [] };
 return { state: 'ready', label: 'Ready to build', waitingOn: [] };
}
export const readinessFilters: { state: TaskReadinessState | 'all'; label: string }[] = [
 { state: 'ready', label: 'Ready' }, { state: 'building', label: 'Building' }, { state: 'waiting', label: 'Waiting' },
 { state: 'needsSpecification', label: 'Needs a specification' }, { state: 'done', label: 'Done' }, { state: 'all', label: 'All' },
];
