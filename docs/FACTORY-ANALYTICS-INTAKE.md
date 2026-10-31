# Analytics signal intake

This source extends explicit problem intake with configured, producer-authored
metric snapshots. It does not infer product goals, install telemetry or turn a
threshold crossing into a proven defect. Native DuckDB remains authoritative for
source configuration, coverage receipts, immutable observations and triage.

## Contract established before implementation

The administrator configures an empty-by-default `Intake.AnalyticsFeeds` catalogue.
Each feed has an ID/name, absolute complete-snapshot path, source/application/
environment identity, explicit age/window limits, allowed dimension names and
rules. Each rule identifies its metric, unit, statistic, comparison, exact decimal
threshold and minimum reported sample count. No metric names, thresholds, sample
requirements or production paths are shipped as defaults. UI source selection
chooses a feed and an explicit subset of its rules; it cannot supply file paths.

Version-1 snapshots contain a stable immutable `reportId`, source/application/
environment, generated/window timestamps, `complete: true`, `sanitized: true`, and
an explicit `metrics` array. Every metric has stable series/metric IDs, a name,
unit/statistic, exact decimal `value` (or null with zero samples), integer `samples`,
and an explicit list of `{name,value}` dimensions. Quantities are canonical decimal
strings, including sample counts, to preserve exact values through JavaScript and
JSON; exponent notation and values that would round in .NET decimal are rejected.
Metric aggregation and sample counts are upstream observations, not calculations
certified by Canopy. The source must document its aggregation provenance.

Unknown fields, source mismatches, duplicate series/dimensions, unconfigured metric
semantics, invalid quantities, partial/expired/oversized reports and conflicting
reuse of a report ID fail complete coverage. A native report-identity ledger keeps
that check across collection and restart. Complete empty arrays and explicit
zero-sample metrics retain coverage; missing measurements never become zero.

Candidate identities use configured scope, rule ID, metric/unit/statistic, upstream
series ID and canonical dimensions. Rebinding a rule to another metric or reusing a
series ID for another segment cannot overwrite the prior problem identity. Evidence
retains the exact value, samples, unit/statistic, dimensions, measurement window and
adopted rule. Fresh export metadata alone does not change unchanged metric evidence.
New measurements revoke verification. A no-longer-qualifying series becomes
unavailable, not fixed. Coverage receipts retain file hash/bytes, report identity,
window and observed/selected/qualifying series counts, including empty coverage.

Verification/file/link rereads the configured snapshot and matches current complete
coverage before the native revision check. Source saves/configuration changes revoke
old authority. Filing requires explicit operator verification, rationale and an
incomplete backlog column; it never starts a pipeline or agent. Linking and request
idempotency retain the existing durable task-link guarantees.

Acceptance tests must exercise real structured files and native persistence,
threshold boundaries, missing/zero samples, exact decimals, source/rule isolation,
conflicting reports across restart, no-result vs unavailable coverage, freshness,
concurrent retries and filing/linking. Preserve the GitHub/review/error-feed
regressions and inspect the compiled running UI before delivery.

## Operator setup

Configure each entry under `Intake.AnalyticsFeeds` (environment prefix
`Intake__AnalyticsFeeds__0__`). `Id`, `Name`, `SourceId`, `Application`, `Environment`,
`Path`, `MaxAgeSeconds` and `MaxWindowSeconds` identify the real producer and its
explicit export policy. `DimensionNames` is an allowlist; an empty list permits no
dimensions. Feed IDs and source/application/environment tuples must be distinct.

Each `Rules` entry contains `Id`, `Name`, and `Condition` with `MetricId`, `Unit`,
`Statistic`, `Comparison`, `Threshold` and `MinimumSamples`. Comparisons are
`above`, `atLeast`, `below` and `atMost`; threshold and sample requirements are
strings, explicitly configured even when the threshold is zero. Metric semantics
must match the producer exactly. Rule IDs must be unique. These are administrator
policies, not values supplied by the browser.

The producer atomically replaces one complete JSON file conforming to
[`analytics.snapshot.schema.json`](../postman/specs/analytics.snapshot.schema.json).
`value` is a fixed-point decimal string exactly representable by .NET decimal;
`samples` is a nonnegative signed-64-bit integer string. Zero samples require
`value: null`; a measured zero uses `value: "0"` with positive samples. Explicit
UTC offsets are required. Use a new immutable report ID for changed export content.
Keep series identities stable and include only sanitized allowlisted text.

`Intake.MaximumAnalyticsMetrics` bounds reported series;
`MaximumAnalyticsSignals` bounds qualifying rule/series pairs. Shared
`MaximumCollectionBytes` bounds both the input and aggregate captured evidence,
including rule fan-out. `CollectionTimeoutSeconds` and `MaximumFutureSkewSeconds`
retain the existing configured deadline and clock bounds. Exhaustion rejects the
whole collection, never truncates it. macOS/Linux use the shared nonblocking
regular-file adapter; other platforms report unsupported capability.

Restart after deployment configuration changes, explicitly save the source to
adopt its current binding, then collect again. Removed rules remain visible and
can be deselected; a source with unavailable selections can be disabled while
preserving its old binding and history. Nothing installs an exporter or connects
production telemetry by default. Receipts, qualifying observations and report
identity hashes remain in native DuckDB until project deletion; there is no
automatic retention purge or ingestion scheduler in this phase.
