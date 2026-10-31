import { useEffect, useState } from 'react';
import type { FactoryRunDetail } from '../../lib/factory-runs';
import { agentRequest } from '../../lib/agents';
import type { AgentRun } from '../../lib/agents';

export function FactoryExecutionEvidence({ detail }: { detail: FactoryRunDetail }) {
 const capture = detail.run.snapshot.execution, budget = detail.budget;
 if (!capture) return <p className="factory-help">No project execution policy captured for this work order. Backend step time limits still apply.</p>;
 return <section aria-label="Captured execution budget"><h4>Captured execution budget</h4><dl className="factory-runtime-facts"><div><dt>Agent reservations</dt><dd>{budget?.reservedAgentInvocations ?? 'Unavailable'} / {capture.policy.maxAgentInvocations}</dd></div><div><dt>Command reservations</dt><dd>{budget?.reservedCommandInvocations ?? 'Unavailable'} / {capture.policy.maxCommandInvocations}</dd></div><div><dt>Elapsed limit</dt><dd>{capture.policy.maxElapsedSeconds} seconds, including manual waits</dd></div><div><dt>Deadline</dt><dd>{budget?.deadline ? new Date(budget.deadline).toLocaleString() : 'Starts with execution'}</dd></div></dl>{budget?.blockedReason && <p role="status" className="factory-error">{budget.blockedReason}</p>}<p className="factory-help">Policy SHA-256: <code>{capture.policyHash}</code></p><p className="factory-help">Model usage and cost can be unknown. These CLI runners enforce invocation and elapsed-time limits; they cannot enforce hard token or currency caps.</p></section>;
}
export function FactoryUsageEvidence({ agentRunId, active }: { agentRunId: string | null; active: boolean }) {
 const [agent, setAgent] = useState<AgentRun | null>(null), [error, setError] = useState('');
 useEffect(() => {
  if (!agentRunId) return;
  const controller = new AbortController();
  const load = () => { void agentRequest<AgentRun>(`/runs/${agentRunId}`, 'GET', undefined, controller.signal).then(value => { if (!controller.signal.aborted) { setAgent(value); setError(''); } }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); }); };
  load(); const timer = active ? setInterval(load, 2000) : null;
  return () => { controller.abort(); if (timer) clearInterval(timer); };
 }, [agentRunId, active]);
 if (!agentRunId) return null;
 return <section aria-label="Provider usage observations"><h4>Provider usage · {agent?.usage?.status ?? 'unknown'}</h4>{error && <p role="alert" className="factory-error">{error}</p>}<p className="factory-help">Completion counters retain provider semantics and may cover different scopes. Missing reports remain unknown. They establish no billing total or spending cap.</p>{agent?.usage?.retentionLimitReached && <p className="factory-error">Usage retention limit reached. Observations are incomplete.</p>}{agent?.usage?.observations.map((usage, i) => <details key={i}><summary>{usage.provider} · {usage.eventType} · {usage.status} · input {usage.inputTokens ?? 'unknown'}, output {usage.outputTokens ?? 'unknown'}</summary><p>{new Date(usage.observedAt).toLocaleString()}</p><pre>{usage.payload}</pre><small>SHA-256: {usage.hash}</small></details>)}</section>;
}
