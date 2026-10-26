import type { Item, Status } from './types';

export type EpicTask = { item: Item; children: EpicTask[]; context: boolean };
export type EpicGroup = { item: Item; tasks: EpicTask[]; taskCount: number; completedCount: number; matchingTaskCount: number; context: boolean };

// Components are the immediate children of the current scope. Filtering keeps
// their ancestor paths, while progress always describes the full component.
export function epicGroups(allItems: Item[], matchingItems: Item[], statuses: Status[], focusId: string | null): EpicGroup[] {
 const matches = new Set(matchingItems.map(item => item.id));
 const completed = new Set(statuses.filter(status => status.isCompleted).map(status => status.id));
 const children = new Map<string | null, Item[]>();
 for (const item of allItems) { const siblings = children.get(item.parentId) ?? []; siblings.push(item); children.set(item.parentId, siblings); }
 for (const siblings of children.values()) siblings.sort(compareSiblings);
 function branch(item: Item, ancestors: Set<string>): { node: EpicTask; total: number; complete: number; matching: number } {
  const path = new Set(ancestors).add(item.id);
  const nested = (children.get(item.id) ?? []).filter(child => !path.has(child.id)).map(child => branch(child, path));
  return {
   node: { item, context: !matches.has(item.id), children: nested.filter(child => child.matching > 0).map(child => child.node) },
   total: 1 + nested.reduce((sum, child) => sum + child.total, 0),
   complete: Number(completed.has(item.statusId)) + nested.reduce((sum, child) => sum + child.complete, 0),
   matching: Number(matches.has(item.id)) + nested.reduce((sum, child) => sum + child.matching, 0),
  };
 }
 return (children.get(focusId) ?? []).map(item => {
  const value = branch(item, new Set());
  return { item, tasks: value.node.children, taskCount: value.total - 1, completedCount: value.complete - Number(completed.has(item.statusId)), matchingTaskCount: value.matching - Number(matches.has(item.id)), context: value.node.context };
 }).filter(group => !group.context || group.matchingTaskCount > 0);
}

export function compareSiblings(a: Item, b: Item): number { return a.siblingPosition - b.siblingPosition || a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id); }

export function componentDetails(allItems: Item[], matchingItems: Item[], statuses: Status[], componentId: string): EpicGroup | undefined {
 const component = allItems.find(item => item.id === componentId);
 // Keep the selected component's header even when only its descendants match.
 return component ? epicGroups(allItems, [component, ...matchingItems], statuses, component.parentId).find(group => group.item.id === componentId) : undefined;
}

// Keep filtered-out siblings in their existing slots. The API requires the full
// sibling set so a partial or stale order can never silently remove an item.
export function resequenceSiblings(items: Item[], parentId: string | null, visibleIds: string[], activeId: string, targetId: string): string[] | null {
 const siblings = items.filter(item => item.parentId === parentId).sort(compareSiblings);
 const siblingIds = new Set(siblings.map(item => item.id));
 if (new Set(visibleIds).size !== visibleIds.length || visibleIds.some(id => !siblingIds.has(id))) return null;
 const from = visibleIds.indexOf(activeId), to = visibleIds.indexOf(targetId);
 if (from < 0 || to < 0 || from === to) return null;
 const visible = [...visibleIds]; visible.splice(to, 0, visible.splice(from, 1)[0]);
 const selected = new Set(visibleIds); let index = 0;
 return siblings.map(item => selected.has(item.id) ? visible[index++] : item.id);
}

export const resequenceComponents = resequenceSiblings;

export function specificationText(specJson: string): string {
 try {
  const read = (node: unknown): string => {
   if (!node || typeof node !== 'object') return '';
   const value = node as { text?: unknown; content?: unknown };
   if (typeof value.text === 'string') return value.text;
   return Array.isArray(value.content) ? value.content.map(read).filter(Boolean).join(' ') : '';
  };
  return read(JSON.parse(specJson)).replace(/\s+/g, ' ').trim();
 } catch { return ''; }
}
