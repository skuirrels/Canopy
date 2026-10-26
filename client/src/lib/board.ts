import type { Item } from './types';

// A component is the task's top-level ancestor, independent of its workflow stage.
export function componentForTask(items: Item[], item: Item): Item | undefined {
 if (!item.parentId) return undefined;
 const byId = new Map(items.map(value => [value.id, value]));
 const seen = new Set([item.id]);
 let current = item;
 while (current.parentId) {
  const parent = byId.get(current.parentId);
  if (!parent || parent.projectId !== item.projectId || seen.has(parent.id)) return undefined;
  seen.add(parent.id); current = parent;
 }
 return current;
}
