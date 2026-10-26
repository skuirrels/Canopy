import type { Person } from './types';

export function assigneeSuggestions(people: Person[], query: string) {
 const name = query.trim();
 const folded = name.toLowerCase();
 return {
  name,
  matches: people.filter(person => person.name.toLowerCase().includes(folded)).sort((a, b) => a.name.localeCompare(b.name)),
  canCreate: !!name && !people.some(person => person.name.toLowerCase() === folded),
 };
}
export function toggleAssignee(ids: string[], id: string) {
 return ids.includes(id) ? ids.filter(value => value !== id) : [...ids, id];
}
export function createdAssignee(before: Person[], after: Person[], name: string): Person {
 const known = new Set(before.map(person => person.id));
 const matches = after.filter(person => !known.has(person.id) && person.name === name.trim());
 if (matches.length !== 1) throw new Error('The new assignee could not be identified. Refresh the workspace before assigning them.');
 return matches[0];
}
