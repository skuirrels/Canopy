import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../lib/api';
import { factoryRequest } from '../../lib/factory';
import { agentRequest } from '../../lib/agents';
import type { AgentProviders } from '../../lib/agents';
import { requireFactoryRunDetail } from '../../lib/factory-runs';
import type { FactoryRunDetail } from '../../lib/factory-runs';

export function useFactoryRun(id: string | null) {
 const [detail, setDetail] = useState<FactoryRunDetail | null>(null), [error, setError] = useState(''), [settingsError, setSettingsError] = useState('');
 const [interval, setInterval] = useState<number | null>(null), [epoch, setEpoch] = useState(0);
 const selected = useRef(id); selected.current = id;
 const seed = useCallback((value: FactoryRunDetail) => {
  const checked = requireFactoryRunDetail(value);
  setDetail(current => {
   if (current?.run.id !== checked.run.id) return checked;
   if (current.run.revision > checked.run.revision) return current;
   return { ...checked, run: { ...checked.run, snapshot: current.run.snapshot } };
  });
 }, []);
 const refresh = useCallback(() => setEpoch(value => value + 1), []);
 const update = useCallback((value: FactoryRunDetail) => {
  const checked = requireFactoryRunDetail(value);
  if (selected.current === checked.run.id) { seed(checked); refresh(); }
 }, [seed, refresh]);
 useEffect(() => {
  const controller = new AbortController();
  void agentRequest<AgentProviders>('/providers', 'GET', undefined, controller.signal).then(value => {
   if (controller.signal.aborted) return;
   if (!Number.isSafeInteger(value.pollIntervalMs) || value.pollIntervalMs <= 0) throw new Error('Run refresh settings are unavailable. Refresh after updating the backend.');
   setInterval(value.pollIntervalMs); setSettingsError('');
  }).catch(cause => { if (!controller.signal.aborted) setSettingsError((cause as Error).message); });
  return () => controller.abort();
 }, [epoch]);
 useEffect(() => {
  if (!id) { setDetail(null); return; }
  const controller = new AbortController(); let timer: ReturnType<typeof setTimeout>;
  setDetail(current => current?.run.id === id ? current : null); setError('');
  async function load() {
   try {
    const value = requireFactoryRunDetail(await factoryRequest<FactoryRunDetail>(`/runs/${id}`, 'GET', undefined, controller.signal));
    if (controller.signal.aborted) return;
    if (value.run.id !== id) throw new Error('The backend returned a different run. Refresh the selected work order.');
    seed(value); setError('');
    if (value.run.active && interval !== null) timer = setTimeout(() => { void load(); }, interval);
   } catch (cause) {
    if (controller.signal.aborted) return;
    setError(`Run refresh failed. Displaying the last saved state when available. ${(cause as Error).message}`);
    if (interval !== null && cause instanceof ApiError && (cause.status === 0 || cause.status >= 500)) timer = setTimeout(() => { void load(); }, interval);
   }
  }
  void load();
  return () => { controller.abort(); clearTimeout(timer); };
 }, [id, epoch, interval, seed]);
 return { detail: detail?.run.id === id ? detail : null, update, seed, error: error || settingsError, refresh };
}
