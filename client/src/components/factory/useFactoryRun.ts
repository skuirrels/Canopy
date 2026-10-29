import { useCallback, useEffect, useRef, useState } from 'react';
import { factoryRequest } from '../../lib/factory';
import { agentRequest } from '../../lib/agents';
import type { AgentProviders } from '../../lib/agents';
import { requireFactoryRunDetail } from '../../lib/factory-runs';
import type { FactoryRunDetail } from '../../lib/factory-runs';

export function useFactoryRun(id: string | null) {
 const [detail, setDetail] = useState<FactoryRunDetail | null>(null), [error, setError] = useState(''), [settingsError, setSettingsError] = useState('');
 const selected = useRef(id); selected.current = id;
 const seed = useCallback((value: FactoryRunDetail) => { const checked = requireFactoryRunDetail(value); setDetail(current => current?.run.id === checked.run.id ? { ...checked, run: { ...checked.run, snapshot: current.run.snapshot } } : checked); }, []);
 const update = useCallback((value: FactoryRunDetail) => { const checked = requireFactoryRunDetail(value); if (selected.current === checked.run.id) seed(checked); }, [seed]);
 const [interval, setInterval] = useState<number | null>(null), [epoch, setEpoch] = useState(0);
 useEffect(() => {
  const controller = new AbortController();
  void agentRequest<AgentProviders>('/providers', 'GET', undefined, controller.signal).then(value => { if (!controller.signal.aborted) { if (!Number.isSafeInteger(value.pollIntervalMs) || value.pollIntervalMs <= 0) throw new Error('Run refresh settings are unavailable. Refresh after updating the backend.'); setInterval(value.pollIntervalMs); setSettingsError(''); } }).catch(cause => { if (!controller.signal.aborted) setSettingsError((cause as Error).message); });
  return () => controller.abort();
 }, [epoch]);
 useEffect(() => {
  if (!id) { setDetail(null); return; }
  const controller = new AbortController(); setDetail(current => current?.run.id === id ? current : null); setError('');
  void factoryRequest<FactoryRunDetail>(`/runs/${id}`, 'GET', undefined, controller.signal).then(value => { if (!controller.signal.aborted) { update(value); setError(''); } }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); });
  return () => controller.abort();
 }, [id, epoch, update]);
 const active = detail?.run.id === id && detail.run.active;
 useEffect(() => {
  if (!id || !active || interval === null) return;
  const controller = new AbortController(); let timer: ReturnType<typeof setTimeout>;
  async function poll() {
   try { const value = await factoryRequest<FactoryRunDetail>(`/runs/${id}`, 'GET', undefined, controller.signal); if (!controller.signal.aborted) { update(value); setError(''); } }
   catch (cause) { if (!controller.signal.aborted) setError(`Run refresh failed. Displaying the last saved state. ${(cause as Error).message}`); }
   finally { if (!controller.signal.aborted) timer = setTimeout(() => { void poll(); }, interval!); }
  }
  timer = setTimeout(() => { void poll(); }, interval);
  return () => { controller.abort(); clearTimeout(timer); };
 }, [id, active, interval, epoch, update]);
 const refresh = useCallback(() => setEpoch(value => value + 1), []);
 return { detail: detail?.run.id === id ? detail : null, update, seed, error: error || settingsError, refresh };
}
