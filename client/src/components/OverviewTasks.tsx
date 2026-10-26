import { useEffect, useMemo, useState } from 'react';
import { ArrowUpRight, CalendarClock, CalendarDays, CheckCheck, CheckCircle2, ChevronDown, CircleAlert, FolderOpen, Link2, Users } from 'lucide-react';
import type { Item } from '../lib/types';
import { useWorkspace } from '../lib/workspace';
import { overviewTasks } from '../lib/overview';
import type { AttentionTask } from '../lib/overview';
import { calendarLabel, dateDay, dayDate, todayDay } from '../lib/gantt';
import { Avatar, PriorityIcon } from './TaskCard';
import { TaskEstimate } from './TaskEstimate';
import './overview-tasks.css';

type Kind = 'today' | 'tomorrow' | 'overdue' | 'undated';
function AttentionRow({ task, kind, onOpen }: { task: AttentionTask; kind: Kind; onOpen: (item: Item) => void }) {
 return <li><button className="attention-task" onClick={() => onOpen(task.item)} aria-label={`Open ${task.item.title}`}>
  <div className="attention-task-context"><span className="attention-project-dot" style={{ background: task.project?.color }}/><span>{task.project?.name ?? 'Project unavailable'}</span><span aria-hidden="true">/</span><span data-tooltip={task.component?.title}>{task.component?.title ?? 'Component unavailable'}</span><ArrowUpRight size={15}/></div>
  <h4>{task.item.title}</h4>
  <div className="attention-task-details"><span className="attention-task-status"><span className="status-dot" style={{ background: task.status?.color }}/>{task.status?.name ?? 'Status unavailable'}</span>{task.item.priority !== 'none' && <span><PriorityIcon priority={task.item.priority}/>{task.item.priority[0].toUpperCase() + task.item.priority.slice(1)}</span>}<TaskEstimate hours={task.item.estimatedHours}/>{task.waitingOn > 0 && <span className="attention-waiting"><Link2 size={13}/>Waiting on {task.waitingOn}</span>}</div>
  <div className="attention-task-bottom"><span className={`attention-due attention-due-${kind}`}><CalendarDays size={14}/>{kind === 'today' ? 'Due today' : kind === 'tomorrow' ? 'Due tomorrow' : kind === 'overdue' ? `${calendarLabel(dateDay(task.item.dueDate!), { day: 'numeric', month: 'short', year: 'numeric' })} · ${task.daysOverdue} ${task.daysOverdue === 1 ? 'day' : 'days'} overdue` : 'No due date'}</span><span className="attention-assignees">{task.assignees.length ? task.assignees.map(({ id, person }) => <span key={id}>{person ? <Avatar small name={person.name}/> : <CircleAlert size={14}/>}<span className="attention-assignee-name">{person?.name ?? 'Assignee unavailable'}</span></span>) : <span className="attention-unassigned"><Users size={14}/>Unassigned</span>}</span></div>
 </button></li>;
}
function TaskLane({ tasks, kind, onOpen }: { tasks: AttentionTask[]; kind: Exclude<Kind, 'undated'>; onOpen: (item: Item) => void }) {
 const title = kind === 'today' ? 'Due today' : kind === 'tomorrow' ? 'Due tomorrow' : 'Overdue';
 return <section className={`attention-lane attention-lane-${kind}`} aria-label={kind === 'overdue' ? 'Overdue tasks' : `Tasks due ${kind}`}>
  <header><span className="attention-lane-icon">{kind === 'overdue' ? <CircleAlert size={19}/> : <CalendarClock size={19}/>}</span><h3>{title}</h3><span className="attention-count">{tasks.length}</span></header>
  {tasks.length ? <ul>{tasks.map(task => <AttentionRow key={task.item.id} task={task} kind={kind} onOpen={onOpen}/>)}</ul> : <div className="attention-empty"><CheckCircle2 size={23}/><strong>{kind === 'overdue' ? 'No overdue tasks' : `No tasks due ${kind}`}</strong><span>{kind === 'overdue' ? 'You’re up to date on scheduled work.' : kind === 'tomorrow' ? 'Tomorrow’s deadlines will appear here when scheduled.' : 'Your next deadlines will appear here when they’re due.'}</span></div>}
 </section>;
}

export function OverviewTasks({ onOpen }: { onOpen: (item: Item) => void }) {
 const { data } = useWorkspace(); const [today, setToday] = useState(() => dayDate(todayDay()));
 useEffect(() => {
  const update = () => setToday(dayDate(todayDay()));
  const timer = window.setInterval(update, 60_000);
  window.addEventListener('focus', update); document.addEventListener('visibilitychange', update);
  return () => { clearInterval(timer); window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update); };
 }, []);
 const attention = useMemo(() => overviewTasks(data, today), [data, today]);
 const count = attention.dueToday.length + attention.dueTomorrow.length + attention.overdue.length;
 return <section className="overview-attention" aria-labelledby="attention-heading">
  <div className="overview-workspace-summary"><span><FolderOpen size={16}/><strong>{data.projects.length}</strong> projects</span><span><strong>{attention.openCount}</strong> open tasks</span><span><CheckCheck size={17}/><strong>{attention.completedCount}</strong> completed</span></div>
  <div className="attention-heading"><div><h2 id="attention-heading">Needs attention<span>{count}</span></h2><p>Outstanding tasks by due date, across all projects.</p></div><span className="attention-calendar"><CalendarDays size={16}/>{calendarLabel(dateDay(today), { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}</span></div>
  {attention.unassignedAttention > 0 && <p className="attention-owner-note"><Users size={14}/>{attention.unassignedAttention} {attention.unassignedAttention === 1 ? 'task needs' : 'tasks need'} an assignee</p>}
  <div className="attention-lanes"><TaskLane tasks={attention.dueToday} kind="today" onOpen={onOpen}/><TaskLane tasks={attention.overdue} kind="overdue" onOpen={onOpen}/><TaskLane tasks={attention.dueTomorrow} kind="tomorrow" onOpen={onOpen}/></div>
  {attention.withoutDueDate.length > 0 && <details className="attention-undated"><summary><CalendarDays size={16}/><span>{attention.withoutDueDate.length} open {attention.withoutDueDate.length === 1 ? 'task has' : 'tasks have'} no due date</span><ChevronDown size={16}/></summary><p>Set a due date in task details to include these in your daily priorities.</p><ul>{attention.withoutDueDate.map(task => <AttentionRow key={task.item.id} task={task} kind="undated" onOpen={onOpen}/>)}</ul></details>}
 </section>;
}
