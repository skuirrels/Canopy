import type { Workspace } from './types';
export class ApiError extends Error { constructor(message: string, public status: number) { super(message); } }
export async function request(path = '/api/workspace', method = 'GET', body?: unknown): Promise<Workspace> {
  let response: Response;
  try { response = await fetch(path, { method, headers: { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) }); }
  catch { throw new ApiError('Cannot reach the server. Your changes have not been saved.', 0); }
  if (!response.ok) { const problem = await response.json().catch(() => ({})); throw new ApiError(problem.title || `Request failed (${response.status}).`, response.status); }
  const value: Workspace = await response.json();
  if (value.items.some(item => !Array.isArray(item.assigneeIds) || !Object.hasOwn(item, 'estimatedHours'))) throw new ApiError('This Canopy server needs to be restarted to support the latest task fields. Your changes have not been saved.', 0);
  return value;
}
