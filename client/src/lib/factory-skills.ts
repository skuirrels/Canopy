import type { FactorySkill } from './factory';
export type SkillEntry = { id: string; source: string; paths: string[]; hash: string | null; title: string | null; state: 'available' | 'unavailable' | 'ambiguous'; error: string | null; origins: string[] };
export type SkillScope = { source: string; path: string; kind: string; complete: boolean; error: string | null };
export type SkillCatalogue = { observedAt: string; fingerprint: string; complete: boolean; entries: SkillEntry[]; scopes: SkillScope[] };
export function skillKey(skill: FactorySkill) { return JSON.stringify([skill.id, skill.source]); }
export function skillMatches(entry: SkillEntry, search: string) { return [entry.id, entry.source, entry.title ?? '', ...entry.paths].join('\n').toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()); }
export function skillSelectionStatus(reference: FactorySkill, catalogue: SkillCatalogue | null) {
 if (!catalogue) return { state: 'unverified', error: 'Refresh the catalogue to observe this reference.' };
 const scope = catalogue.scopes.find(x => x.source === reference.source && !x.complete);
 if (scope) return { state: 'unavailable', error: scope.error ?? 'Source coverage is incomplete.' };
 return catalogue.entries.find(entry => skillKey(entry) === skillKey(reference)) ?? { state: 'unavailable', error: 'No matching installed reference was observed. Configure its exact source or retain it for another installation.' };
}
