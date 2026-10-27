import { useEffect, useId, useState } from 'react';
import { CalendarDays } from 'lucide-react';
import { dateWithinBounds, taskDateShortcut } from '../lib/task-dates';

export function TaskDateField({ label, value, min, max, hint, onChange }: { label: string; value: string | null; min?: string; max?: string; hint?: string; onChange: (value: string | null) => void }) {
 const id = useId(); const [today, setToday] = useState(() => taskDateShortcut(0));
 useEffect(() => {
  const update = () => setToday(taskDateShortcut(0));
  const timer = window.setInterval(update, 60_000);
  window.addEventListener('focus', update); document.addEventListener('visibilitychange', update);
  return () => { clearInterval(timer); window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update); };
 }, []);
 return <div className="task-date-property"><label htmlFor={id}><CalendarDays size={14}/>{label}</label><div className="task-date-control">
  <input id={id} aria-label={label} aria-describedby={hint ? `${id}-hint` : undefined} type="date" value={value || ''} min={min} max={max} onChange={event => onChange(event.target.value || null)}/>
  <div className="task-date-shortcuts" role="group" aria-label={`Quick ${label.toLowerCase()}`}>
   {(['Today', 'Tomorrow'] as const).map((name, index) => {
    const offset = index as 0 | 1;
    // Re-rendering at midnight refreshes enabled states. Clicking always uses the current day.
    const target = offset === 0 ? today : taskDateShortcut(1);
    const allowed = dateWithinBounds(target, min, max);
    const reason = min && target < min ? `Must be on or after the start date (${min}).` : `Must be on or before the due date (${max}).`;
    return <button type="button" key={name} aria-label={`Set ${label.toLowerCase()} to ${name.toLowerCase()}`} data-tooltip={allowed ? `Set ${label.toLowerCase()} to ${target}` : reason} disabled={!allowed} onClick={() => { const next = taskDateShortcut(offset); setToday(taskDateShortcut(0)); if (dateWithinBounds(next, min, max)) onChange(next); }}>{name}</button>;
   })}
  </div>
  {hint && <p className="task-date-help" id={`${id}-hint`}>{hint}</p>}
 </div></div>;
}
