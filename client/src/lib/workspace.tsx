import { createContext, useContext, useRef, useState, useCallback, useEffect } from 'react';
import type { ReactNode } from 'react';
import { toast } from 'sonner';
import { ApiError, request } from './api';
import { emptyWorkspace } from './types';
import type { Workspace } from './types';

type PlannerContext = {
 data: Workspace; loading: boolean; error: string | null; pending: number;
 refresh: () => Promise<void>;
 mutate: (path: string, method: string, body?: unknown, projectId?: string) => Promise<Workspace>;
};
const Context = createContext<PlannerContext | null>(null);
export function WorkspaceProvider({ children }: { children: ReactNode }) {
 const [data, setData] = useState(emptyWorkspace); const latest = useRef(data);
 const [loading, setLoading] = useState(true); const [error, setError] = useState<string | null>(null); const [pending, setPending] = useState(0);
 const queue = useRef<Promise<unknown>>(Promise.resolve());
 const apply = useCallback((next: Workspace) => { latest.current = next; setData(next); }, []);
 const refresh = useCallback(async () => { setError(null); try { apply(await request()); } catch (e) { setError((e as Error).message); } finally { setLoading(false); } }, [apply]);
 useEffect(() => { void refresh(); }, [refresh]);
 const mutate = useCallback((path: string, method: string, body?: unknown, projectId?: string): Promise<Workspace> => {
   setPending(n => n + 1);
   const operation = queue.current.catch(() => undefined).then(async () => {
     let url = path; let payload = body;
     if (projectId) {
       const project = latest.current.projects.find(p => p.id === projectId);
       if (!project) throw new Error('Project no longer exists.');
       if (method === 'DELETE') url += `${path.includes('?') ? '&' : '?'}expectedRevision=${project.revision}`;
       else payload = { ...(body as object), expectedRevision: project.revision };
     }
     try { const next = await request(url, method, payload); apply(next); return next; }
     catch (e) {
       toast.error((e as Error).message, { duration: 8000 });
       if (e instanceof ApiError && e.status === 409) await refresh();
       throw e;
     }
   }).finally(() => setPending(n => n - 1));
   queue.current = operation; return operation;
 }, [apply, refresh]);
 return <Context.Provider value={{ data, loading, error, pending, refresh, mutate }}>{children}</Context.Provider>;
}
export function useWorkspace() { const context = useContext(Context); if (!context) throw new Error('WorkspaceProvider is required.'); return context; }
