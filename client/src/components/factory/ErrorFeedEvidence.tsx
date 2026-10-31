import type { ErrorFeedReceipt, ErrorGroupEvidence } from '../../lib/factory-intake';
export function ErrorFeedCoverage({ receipt }: { receipt: ErrorFeedReceipt | undefined }) {
 if (!receipt) return null;
 const c = receipt.coverage;
 return <section className="error-feed-coverage" aria-label="Complete error-feed coverage"><h4>{c.identity.application} · {c.identity.environment}</h4><p>Complete window: {new Date(c.windowStart).toLocaleString()} – {new Date(c.windowEnd).toLocaleString()}</p><p>Generated: {new Date(c.generatedAt).toLocaleString()}</p><p>{c.rawEvents} supplied events · {c.distinctEvents} distinct · {c.selectedEvents} selected · {c.eligibleGroups} eligible {c.eligibleGroups === 1 ? 'group' : 'groups'}</p>{c.eligibleGroups === 0 && <p className="factory-help">No groups matched the selected filters in this captured window. This does not certify application health.</p>}<details><summary>Snapshot provenance</summary><p>Source: {c.identity.sourceId} · Feed: {c.feedId}</p><p>{c.bytes} bytes</p><code>SHA-256: {c.fileHash}</code></details></section>;
}
export function ApplicationErrorEvidence({ json }: { json: string }) {
 let evidence: ErrorGroupEvidence;
 try { evidence = JSON.parse(json) as ErrorGroupEvidence; } catch { return null; }
 if (evidence?.kind !== 'applicationErrors') return null;
 const g = evidence.group;
 return <section className="error-group-evidence" aria-label="Application-error evidence"><h4>{g.exceptionType}{g.code ? ` · ${g.code}` : ''}</h4><p>{evidence.identity.application} · {evidence.identity.environment} · {evidence.identity.sourceId}</p><p>Upstream error ID: <code>{g.id}</code></p><pre>{g.messageTemplate}</pre><details><summary>Structured code frames · {g.frames.length}</summary>{g.frames.map((frame, i) => <p key={i}><code>{frame.path}{frame.line !== null ? `:${frame.line}` : ''}</code> · {frame.symbol}</p>)}{!g.frames.length && <p>No code frames reported.</p>}</details><details><summary>Distinct selected events · {g.occurrences.length}</summary><p>Repeated reads and duplicate IDs never add an occurrence.</p>{g.occurrences.map(event => <p key={event.id}><code>{event.id}</code> · {new Date(event.occurredAt).toLocaleString()} · {event.level} · {event.release ? `Release ${event.release}` : 'Release not reported'}</p>)}</details><p className="factory-help">The producer marks this snapshot sanitized; Canopy does not certify that supplied text is free of sensitive data. Verify reproduction and relevance before filing.</p></section>;
}
