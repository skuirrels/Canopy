import { dayDate, todayDay } from './gantt.ts';

// Advance calendar days, not elapsed hours or UTC timestamps, across DST changes.
export function taskDateShortcut(offset: 0 | 1, now = new Date()) {
 return dayDate(todayDay(now) + offset);
}
export function dateWithinBounds(value: string, min?: string, max?: string) {
 return (!min || value >= min) && (!max || value <= max);
}
