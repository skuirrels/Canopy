import { dayDate, todayDay } from './gantt.ts';
import type { Item } from './types';

// Allow planning beyond the old deadline without saving an inverted range.
export function changeStartDate(item: Pick<Item, 'startDate' | 'dueDate'>, startDate: string | null): Pick<Item, 'startDate' | 'dueDate'> {
 const dueDate = startDate && item.dueDate && startDate > item.dueDate ? startDate : item.dueDate;
 return { startDate, dueDate };
}

// Advance calendar days, not elapsed hours or UTC timestamps, across DST changes.
export function taskDateShortcut(offset: 0 | 1, now = new Date()) {
 return dayDate(todayDay(now) + offset);
}
export function dateWithinBounds(value: string, min?: string, max?: string) {
 return (!min || value >= min) && (!max || value <= max);
}
