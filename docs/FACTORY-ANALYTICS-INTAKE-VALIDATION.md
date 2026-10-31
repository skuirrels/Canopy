# Analytics signal intake validation

Public APIs were exercised against real temporary JSON snapshots and native DuckDB,
using the public EFCoreProvider. No production metric source, model call or live
user task was a test input. Source selection, rule adoption and filing remain
explicit actions.

## Verified behavior

`python3 scripts/factory-analytics-intake-regression.py --postman` passed 78 checks
and eight saved Postman assertions. The suite covers empty configuration, explicit
catalogue/rule selection, same-origin and closed-input checks, concurrent exact
collection and filing retries, immutable report/file provenance and native restart.

Threshold tests compare exact values below, equal to and above each of the four
configured comparisons. Values beyond JavaScript's safe integer range remain
strings in policy and observed evidence. Over-precise decimals, exponent notation,
wrong JSON types, invalid sample counts, missing measurements and unit/statistic/
metric/dimension mismatches cannot establish coverage. No rule or business quantity
is fabricated by a new deployment.

The suite rejects internally consistent report-ID rewrites after native restart.
Rule rebinding to another metric and reused series IDs with different dimensions
retain distinct candidate identities. Complete empty windows and zero-sample
metrics retain coverage without asserting health or marking problems fixed. New
measurements revoke verification; export metadata alone needs collection but does
not change unchanged metric/window evidence. Real elapsed receipt expiry revokes
current authority without a new collection.

Failure tests preserve persisted candidates, observations, decisions and receipts.
Partial, expired, malformed, duplicate-field, oversized, symlinked and FIFO inputs
fail complete coverage. Rule fan-out separately reaches the configured signal
limit and aggregate evidence byte limit; tests check the actual failure cause.
Concurrent filing creates one task in the chosen incomplete column. Explicit links
preserve task metadata, and deleted linked tasks cannot be filed again. Removed
rules/feeds remain disableable. Test projects are deleted through public APIs.

## Upgrade and regression coverage

`dotnet run --project tests/Planner.AnalyticsMigrationRegression` creates the prior
release's native schema with an error source, verified observation/decision, complete
file receipt and occurrence identity. The additive analytics migration preserves
those records, raw configuration, hashes and scope. New tables start empty; existing
sources remain editable across restart. Existing error-source migration coverage
also remains in CI.

The error-feed suite passed 56 checks plus its saved Postman assertions after the
shared strict snapshot parser/regular-file reader extraction and removed-level
fix. The GitHub/review suite passed 37 checks against its existing Postman simulation.
Client verification passed 99 tests and the production build. Backend build passed
without warnings/errors. New collection lint and OpenAPI syntax lint passed. Core
CI runs the upgrade check; providers runs the analytics API/Postman suite.

## Running interface

Used the isolated compiled frontend and actual backend to inspect configured rules,
edit the source, select two rules, save, observe stale authority and a disabled
verification action, collect, and verify with an explicit rationale. The exact
large decimal remained unchanged in the DOM. Opened a filing preview with a chosen
incomplete column and cancelled; preview created no task. Expanded actual dimension
metadata. Clearing all rule selections disabled Save. Source, evidence and editor
layouts had no document overflow at desktop and a 390-pixel viewport (433 CSS pixels
due to browser zoom). No console errors were reported. This is browser responsive
verification, not a physical-device claim.

## Review findings and limits

Review added incremental fan-out signal/byte limits and cancellation checks before
materializing excessive evidence, semantic metric/dimension fingerprints, strict
exact-decimal validation and visibility/disabling of unavailable rule/level
selections. Threshold decisions live in Domain; file reads are shared Infrastructure;
source and evidence coordination use IWorkspaceStore transactions.

The producer owns metric aggregation, sample counting and sanitization. A flag or
threshold crossing does not establish causality, correctness or a defect. Freshness
is sampled export evidence; it is not a lock against an external publisher. No real
analytics or error producer is configured by this phase. No automatic telemetry,
filing, remediation, acceptance or execution is claimed.
