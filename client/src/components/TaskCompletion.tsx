import { useRef, useState } from 'react';
import type { RefObject } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { Check } from 'lucide-react';
import { toast } from 'sonner';
import type { Item } from '../lib/types';
import { useWorkspace } from '../lib/workspace';
import { Button } from './ui/button';

// Without a completed column there is nowhere to move a task. The view showing the tick buttons renders this note once.
export const noCompletedStatus = 'No column counts as done. Mark a board column complete first.';
export const noCompletedStatusId = (projectId: string) => `no-completed-status-${projectId}`;

export function TaskCompletion({ item, returnFocusRef }: { item: Item; returnFocusRef: RefObject<HTMLButtonElement | null> }) {
 const { data, pending, mutate } = useWorkspace();
 const [open, setOpen] = useState(false);
 const [saving, setSaving] = useState(false);
 const submitting = useRef(false);
 const cancel = useRef<HTMLButtonElement>(null);
 const status = data.statuses.find(value => value.id === item.statusId);
 const completed = useRef(false);
 completed.current = !!status?.isCompleted;
 const completedStatuses = data.statuses.filter(value => value.projectId === item.projectId && value.isCompleted).sort((a, b) => a.position - b.position);
 const [selectedStatus, setSelectedStatus] = useState('');
 const target = completedStatuses.find(value => value.id === selectedStatus) ?? completedStatuses[0];
 const hasChildren = data.items.some(value => value.parentId === item.id);
 async function complete() {
  if (!target || pending || submitting.current) return;
  submitting.current = true; setSaving(true);
  try {
   await mutate(`/api/items/${item.id}/move`, 'POST', { parentId: item.parentId, statusId: target.id, beforeId: null }, item.projectId);
   setOpen(false);
   toast.success(`“${item.title}” marked as ${target.name}`);
  } catch { /* The workspace reports validation, revision and persistence errors. Keep confirmation open to retry. */ }
  finally { submitting.current = false; setSaving(false); }
 }
 if (status?.isCompleted) return <span className="task-completion task-completion-done" style={{ color: status.color }} aria-label={`${item.title}: ${status.name}`}><span className="task-completion-circle"><Check size={13} strokeWidth={2.5}/></span></span>;
 return <Dialog.Root open={open} onOpenChange={value => { if (!submitting.current) setOpen(value); }}>
  <Dialog.Trigger asChild><button type="button" className="task-completion" aria-label={`Mark ${item.title} as done`} data-tooltip={target ? 'Mark as done' : noCompletedStatus} aria-describedby={target ? undefined : noCompletedStatusId(item.projectId)} disabled={pending > 0 || !target}><span className="task-completion-circle"><Check size={13} strokeWidth={2.5}/></span></button></Dialog.Trigger>
  <Dialog.Portal><Dialog.Overlay className="dialog-overlay"/><Dialog.Content className="dialog-content task-completion-dialog" onOpenAutoFocus={event => { event.preventDefault(); cancel.current?.focus(); }} onCloseAutoFocus={event => {
   // A successful move removes the trigger; keep keyboard users in this task row.
   if (completed.current) { event.preventDefault(); returnFocusRef.current?.focus(); }
  }}>
   <Dialog.Title>Mark as done?</Dialog.Title>
   <Dialog.Description>“{item.title}”{hasChildren && <span className="task-completion-note">Subtasks will keep their current statuses.</span>}</Dialog.Description>
   {completedStatuses.length > 1 && <label>Completed status<select value={target?.id ?? ''} onChange={event => setSelectedStatus(event.target.value)} disabled={saving}>{completedStatuses.map(value => <option key={value.id} value={value.id}>{value.name}</option>)}</select></label>}
   <div className="dialog-footer"><button ref={cancel} type="button" className="button button-ghost button-sm" disabled={saving} onClick={() => setOpen(false)}>Cancel</button><Button size="sm" disabled={saving || pending > 0} blockedReason={!target && noCompletedStatus} onClick={() => void complete()}>{saving ? 'Saving…' : 'Mark as done'}</Button></div>
  </Dialog.Content></Dialog.Portal>
 </Dialog.Root>;
}
