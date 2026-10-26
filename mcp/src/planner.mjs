export class PlannerError extends Error {
 constructor(message, status = 0) { super(message); this.status = status; }
}
export class Planner {
 constructor(config) { this.config = config; }
 async request(path = 'workspace', method = 'GET', body) {
  let response;
  try {
   response = await fetch(new URL(`api/${path}`, this.config.baseUrl), { method, headers: { 'Content-Type':'application/json' }, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(this.config.timeoutMs), redirect: 'error' });
  } catch (error) {
   throw new PlannerError(`Canopy could not be reached at ${this.config.baseUrl}. Check that the app is running. ${error.name === 'TimeoutError' ? 'The request timed out.' : 'The connection failed.'} A failed response does not prove a write was rolled back; read the project before retrying.`);
  }
  let value;
  try { value = await response.json(); } catch { throw new PlannerError(`Canopy returned an invalid response (HTTP ${response.status}). Read the project before retrying a write.`, response.status); }
  if (!response.ok) throw new PlannerError(value.title ?? `Canopy rejected the request (HTTP ${response.status}).`, response.status);
  if (!Array.isArray(value.projects) || !Array.isArray(value.items) || !Array.isArray(value.statuses) || !Array.isArray(value.people) || !Array.isArray(value.dependencies)) throw new PlannerError('The response is not a Canopy workspace. Check PLANNER_URL.');
  return value;
 }
 project(state, id, revision) {
  const project = state.projects.find(p => p.id === id);
  if (!project) throw new PlannerError('Project not found.',404);
  if (revision !== undefined && project.revision !== revision) throw new PlannerError(`Project changed. Expected revision ${revision}, current revision ${project.revision}. Read the project/task and reapply only the intended changes.`,409);
  return project;
 }
 task(state, id, revision) {
  const item = state.items.find(i => i.id === id);
  if (!item) throw new PlannerError('Task not found.',404);
  this.project(state,item.projectId,revision); return item;
 }
 result(state, taskId) { const task = this.task(state,taskId); return { task, revision:this.project(state,task.projectId).revision }; }
 async editTask(taskId, expectedRevision, changes) {
  const state = await this.request(); const item = this.task(state,taskId,expectedRevision);
  const fields = Object.fromEntries(['title','specJson','statusId','priority','estimatedHours','startDate','dueDate'].map(k => [k,item[k]]));
  const dependencyIds = state.dependencies.filter(d => d.itemId === taskId).map(d => d.dependsOnId);
  const assigneeIds = assignedIds(item);
  const assignments = Object.hasOwn(changes,'assigneeIds') ? changes.assigneeIds : Object.hasOwn(changes,'assigneeId') ? (changes.assigneeId ? [changes.assigneeId] : []) : assigneeIds;
  const {assigneeId: legacyAssigneeId, ...updates} = changes;
  if (Object.hasOwn(changes,'assigneeIds') && legacyAssigneeId != null && changes.assigneeIds[0] !== legacyAssigneeId) throw new PlannerError('Supply assigneeIds or the legacy assigneeId, not conflicting values.',400);
  const next = await this.request(`items/${taskId}`,'PATCH',{...fields,dependencyIds,...updates,assigneeIds:assignments,expectedRevision});
  return this.result(next,taskId);
 }
}
export function taskSummary(item) {
 const { specJson, ...summary } = item; return { ...summary, hasSpecification: !!JSON.parse(specJson).content?.some(n => n.type !== 'paragraph' || n.content?.length) };
}
export function paginate(values, offset, limit) { return { total:values.length, offset, limit, nextOffset:offset + limit < values.length ? offset + limit : null, items:values.slice(offset,offset+limit) }; }

export function assignedIds(item) { return item.assigneeIds ?? (item.assigneeId ? [item.assigneeId] : []); }
