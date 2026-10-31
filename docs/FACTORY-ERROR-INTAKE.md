# Application-error intake from structured feeds

This phase extends optional problem intake with administrator-configured structured
error snapshots. It reuses native DuckDB candidates, complete collection receipts,
immutable observations and explicit verify/file/link actions. It grants no execution,
acceptance or merge authority.

## Contract

- `GET /api/factory/projects/{id}/intake/error-feeds` lists configured feed IDs,
  application/environment/source identity, levels and freshness/window limits.
  The browser cannot supply a filesystem path. No feeds or error events exist by default.
- Existing source APIs accept `kind: applicationErrors`, explicit `errorFeedId`,
  `levels`, and `minimumOccurrences >= 1`. Repository, labels and review categories
  must be empty. Source kind/feed ID are immutable; source saves adopt the current
  server binding and require a fresh complete collection.
- Configuration defines the absolute snapshot file, immutable source/application/
  environment identity, supported levels, maximum age and maximum window duration.
  Collection byte/event bounds and allowed future clock skew come from configuration.
- Snapshot schema version 1 requires source/application/environment, generated/window
  timestamps, explicit `complete: true`, `sanitized: true`, and groups. Each group
  carries its upstream stable ID, title, exception type, message template, optional
  code, structured frames and occurrences (stable ID, time, configured level and
  optional release). Unknown fields are rejected; arbitrary headers, request bodies
  and raw exception/context blobs are outside the contract. The producer owns
  sanitization; this flag does not certify that accepted text contains no sensitive data.
- Count distinct occurrence identities. Exact duplicate occurrences count once;
  conflicting IDs, IDs assigned to different groups and duplicate group identities
  fail the entire collection. A native identity ledger rejects changed occurrence facts
  or group ownership across collections and restarts, including events below the
  selected threshold. IDs identify individual immutable occurrences across the
  feed lifetime; an error code or .NET logging EventId is not an occurrence ID.
  Counts still come from the current snapshot, not the ledger. No semantic
  clustering or count is fabricated.
- Preserve a native coverage receipt even for an empty complete window: source scope,
  file SHA-256/bytes, generated/window times, total/distinct/selected event counts and
  eligible group count. Failed, missing, partial, oversized or expired snapshots
  cannot replace candidates or imply that no errors exist.
- Stable group fingerprints/evidence exclude volatile export metadata, so rereading
  unchanged events never creates a new problem or occurrence. New/changed evidence
  revokes verification. A group disappearing from a complete window becomes
  ineligible; it is never automatically marked fixed.
- Before verify/file/link, reread and validate the configured snapshot and match its
  hash to the latest persisted complete receipt. Changed file => collect again.
  Recheck source/current collection/revisions inside the native transaction. This is
  sampled external evidence, not an atomic lock against the exporter.
- Filing still needs an operator rationale, preview and explicitly chosen incomplete
  backlog column. The task retains the source summary and immutable evidence/collection
  references and hash; full event/frame history stays in intake rather than overflowing
  task specification limits. It creates no pipeline or agent run. Existing durable links and
  request IDs prevent duplicate filing, including after deletion of a linked task.

## Operator setup

The default `Intake.ErrorFeeds` list is empty. Configure each entry through the
backend configuration system (for example the `Intake__ErrorFeeds__0__` environment
variable prefix). Restart the backend to adopt configuration, then explicitly save
the corresponding intake source and collect its current complete snapshot.

| Configuration suffix | Required producer or operator value |
| --- | --- |
| `Id`, `Name` | Stable catalogue key and operator-facing feed name |
| `SourceId`, `Application`, `Environment` | Exact upstream scope; do not reuse it for a different event identity scheme |
| `Path` | Absolute regular JSON snapshot path owned by the configured producer |
| `Levels__0`, subsequent level indexes | Exact upstream level names; the UI chooses a subset |
| `MaxAgeSeconds` | Explicit positive export freshness limit |
| `MaxWindowSeconds` | Explicit positive coverage duration limit |

`Intake.MaximumCollectionBytes`, `MaximumErrorEvents`, `CollectionTimeoutSeconds`
and `MaximumFutureSkewSeconds` bound collection. Their shipped operational defaults
do not provide source identities or fabricated error data. Feed IDs and identity
tuples must be unique. File collection is supported on macOS and Linux; other
platforms expose unsupported capability and need a safe adapter.

The producer must atomically replace one complete file using the closed
[`application-errors.snapshot.schema.json`](../postman/specs/application-errors.snapshot.schema.json)
contract. Export only sanitized, allowlisted text and relative code frames; remove
credentials and personal request context before publishing. Keep event IDs stable
when windows overlap. Exact duplicates are accepted, conflicting IDs fail. Explicit
UTC offsets are required. Exporting an empty `groups` array asserts complete empty
coverage only for the stated window. It does not assert health or a repair.

No watcher or automatic exporter is installed. Collection and task filing are manual.
Receipts, observations and occurrence identity hashes persist in native DuckDB until
project deletion; there is no automatic retention purge in this phase. Choose window,
collection cadence and storage monitoring accordingly. No real feed is connected
unless the operator supplies its actual identity, file and freshness policy.

## Acceptance evidence to collect

Exercise public APIs against real temporary files and native DuckDB: no configured
feed; complete empty vs missing/partial/expired/oversized/conflicting evidence;
source/app/environment isolation; distinct IDs and rereads; freshness and binding
changes; triage/file/link idempotency; persistence across restart and cleanup.
Verify the running source editor, source/coverage history and candidate evidence,
including mobile layout. Preserve existing GitHub/review intake tests and their
Postman simulation. No production error files or live user tasks are test inputs.
