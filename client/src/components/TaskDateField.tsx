import { useEffect, useId, useState } from 'react';
import { CalendarDays } from 'lucide-react';
import { dateWithinBounds, taskDateShortcut } from '../lib/task-dates';
import { BlockedNote } from './ui/button';

export function TaskDateField({ label, value, min, max, hint, onChange }: { label: string; value: string | null; min?: string; max?: string; hint?: string; onChange: (value: string | null) => void }) {
 const id = useId(); const [today, setToday] = useState(() => taskDateShortcut(0));
 useEffect(() => {
  const update = () => setToday(taskDateShortcut(0));
  const timer = window.setInterval(update, 60_000);
  window.addEventListener('focus', update); document.addEventListener('visibilitychange', update);
  return () => { clearInterval(timer); window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update); };
 }, []);
 // Re-rendering at midnight refreshes enabled states. Clicking always uses the current day.
 const shortcuts = (['Today', 'Tomorrow'] as const).map((name, offset) => {
  const target = offset === 0 ? today : taskDateShortcut(1);
  const why = dateWithinBounds(target, min, max) ? '' : min && target < min ? `before the start date (${min})` : `after the due date (${max})`;
  return { name, offset: offset as 0 | 1, target, why };
 });
 // Say once which shortcuts are out of range and why, so the fix (change the other date) is visible.
 const blocked = [...new Set(shortcuts.map(s => s.why).filter(Boolean))].map(why => { const names = shortcuts.filter(s => s.why === why).map(s => s.name); return `${names.join(' and ')} ${names.length > 1 ? 'are' : 'is'} ${why}.`; }).join(' ');
 const blockedId = `${id}-blocked`;
 return <div className="task-date-property"><label htmlFor={id}><CalendarDays size={14}/>{label}</label><div className="task-date-control">
  <input id={id} aria-label={label} aria-describedby={hint ? `${id}-hint` : undefined} type="date" value={value || ''} min={min} max={max} onChange={event => onChange(event.target.value || null)}/>
  <div className="task-date-shortcuts" role="group" aria-label={`Quick ${label.toLowerCase()}`}>
   {shortcuts.map(({ name, offset, target, why }) => <button type="button" key={name} aria-label={`Set ${label.toLowerCase()} to ${name.toLowerCase()}`} aria-describedby={why ? blockedId : undefined} data-tooltip={why ? `${name} is ${why}.` : `Set ${label.toLowerCase()} to ${target}`} disabled={!!why} onClick={() => { const next = taskDateShortcut(offset); setToday(taskDateShortcut(0)); if (dateWithinBounds(next, min, max)) onChange(next); }}>{name}</button>)}
  </div>
  <BlockedNote id={blockedId} reason={blocked}/>
  {hint && <p className="task-date-help" id={`${id}-hint`}>{hint}</p>}
 </div></div>;
}
