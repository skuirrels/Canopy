import { useState } from 'react';
import { ArrowRight, Plus } from 'lucide-react';
import type { Item } from '../../../lib/types';
import { useWorkspace } from '../../../lib/workspace';
import { taskReadiness } from '../../../lib/factory-readiness';
import type { BuildListItem } from '../../../lib/factory-builds';
import { needsAttention } from '../../../lib/factory-builds';
import { Button } from '../../ui/button';

// Step 3: tasks that can be built now, and what the others still need.
export function GuidedTasks({ items, builds, needsDescription, onChoose, onOpenTask, onAddTask }: { items: Item[]; builds: BuildListItem[]; needsDescription: boolean; onChoose: (id: string) => void; onOpenTask: (id: string) => void; onAddTask: () => void }) {
 const { data } = useWorkspace();
 const [showOthers, setShowOthers] = useState(false), [search, setSearch] = useState('');
 const parents = new Set(items.map(item => item.parentId).filter(Boolean));
 const busy = new Set(builds.filter(build => needsAttention(build.phase)).map(build => build.itemId));
 const tasks = items.filter(item => !parents.has(item.id) && !busy.has(item.id)).map(item => ({ item, readiness: taskReadiness(item, data, [], needsDescription), group: items.find(parent => parent.id === item.parentId)?.title }))
  .filter(task => task.readiness.state !== 'done' && task.item.title.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()));
 const ready = tasks.filter(task => task.readiness.state === 'ready'), others = tasks.filter(task => task.readiness.state !== 'ready');
 return <>
  <h2>Choose a task</h2>
  <p className="guided-lead">{ready.length ? `${ready.length === 1 ? 'One task is' : `${ready.length} tasks are`} ready. A task is ready when it has a description and isn’t waiting for another task to finish.` : items.length ? 'No task is ready yet. Each task below says what it needs.' : 'This project has no tasks yet. Add one, describe what it should do, then come back here.'}</p>
  {items.length > 8 && <input className="guided-search" type="search" aria-label="Find a task" placeholder="Find a task…" value={search} onChange={e => setSearch(e.target.value)}/>}
  {ready.length > 0 && <ul className="guided-tasks">{ready.map(task => <li key={task.item.id}>
   <button type="button" onClick={() => onChoose(task.item.id)}><span><strong>{task.item.title}</strong>{task.group && <small>{task.group}</small>}</span><ArrowRight size={14}/></button>
  </li>)}</ul>}
  {!items.length && <Button variant="secondary" onClick={onAddTask}><Plus size={14}/>Add a task</Button>}
  {others.length > 0 && <button type="button" className="text-button" aria-expanded={showOthers || !ready.length} onClick={() => setShowOthers(!showOthers)}>{showOthers || !ready.length ? 'Hide' : 'Show'} {others.length === 1 ? 'the task that isn’t' : `${others.length} tasks that aren’t`} ready</button>}
  {(showOthers || !ready.length) && others.length > 0 && <ul className="guided-tasks not-ready">{others.map(task => <li key={task.item.id}><div>
   <strong>{task.item.title}</strong>
   <small>{task.readiness.state === 'waiting' ? `Waiting for: ${task.readiness.waitingOn.map(other => other.title).join(', ')}` : task.readiness.state === 'needsSpecification' ? 'Needs a description of what to build.' : 'Being built.'}</small>
   {task.readiness.state === 'needsSpecification' && <button type="button" className="text-button" onClick={() => onOpenTask(task.item.id)}>Write a description</button>}
  </div></li>)}</ul>}
 </>;
}
