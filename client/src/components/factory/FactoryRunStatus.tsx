const labels: Record<string, string> = {
 prepared: 'Prepared', pending: 'Pending', queued: 'Queued', running: 'Running',
 waiting: 'Human decision', cancelling: 'Stopping', succeeded: 'Completed', failed: 'Failed',
 cancelled: 'Cancelled', interrupted: 'Interrupted', blocked: 'Not run', unavailable: 'Record unavailable',
};
export function factoryStateLabel(state: string) { return labels[state] ?? state; }
export function FactoryRunStatus({ state }: { state: string }) {
 return <span className="factory-runtime-status" data-state={state}><span aria-hidden="true"/>{factoryStateLabel(state)}</span>;
}
